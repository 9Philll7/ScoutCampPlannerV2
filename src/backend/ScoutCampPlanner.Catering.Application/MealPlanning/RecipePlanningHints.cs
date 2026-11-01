using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record RecipeHintDefinition(Guid Id, string Code, string Name);
public sealed record RecipeHintEvidence(Guid IngredientRevisionId, string IngredientName, string PositionPath);
public sealed record RecipePlanningHint(string Kind, Guid ReferenceId, string Code, string Name, string State,
    string Explanation, IReadOnlyList<RecipeHintEvidence> Evidence, string Adjustment = "Unknown");
public sealed record RecipePlanningHints(Guid RecipeRevisionId, string Basis, DateTimeOffset EvaluatedAtUtc,
    bool HasUnknownData, IReadOnlyList<RecipePlanningHint> Hints);

public interface IRecipePlanningHintsReader
{
    Task<RecipePlanningHints?> ReadAsync(Guid campId, Guid tenantId, Guid revisionId, CancellationToken ct);
}

/// <summary>Context-free observations of the pinned standard recipe. Never a participant clearance.</summary>
public sealed class RecipePlanningHintsProjector(ISupplySuitabilityReferences source)
{
    public RecipePlanningHints Build(Guid revisionId, IReadOnlyList<RecipeHintDefinition> allergens,
        IReadOnlyList<RecipeHintDefinition> diets, IReadOnlyList<RecipeHintDefinition> substances, DateTimeOffset evaluatedAt,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var references = new CachedReferences(source);
        var recipe = references.GetRevision(revisionId);
        decimal servings = recipe.Reference.StandardServings ?? throw new InvalidOperationException("Portion-based revision required.");
        var ingredients = new RecipeCalculator(references, references).Calculate(new(revisionId, servings, servings)).Ingredients;
        var solution = new SupplySolution(Guid.NewGuid(), revisionId, null, servings,
            new Dictionary<string, RecipePositionSelection>(), [], SupplyDecisionSource.Automatic);
        var evaluator = new SupplySuitabilityEvaluator(references);
        var resolver = new SupplySolutionResolver(references);
        using var adjustmentBudget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        adjustmentBudget.CancelAfter(TimeSpan.FromSeconds(3));
        var hints = new List<RecipePlanningHint>();
        bool unknownData = ingredients.Count == 0 || ingredients.Any(position => {
            var ingredient = references.Ingredient(position.IngredientId);
            return ingredient.AllergenReviewState != IngredientPropertyReviewState.Reviewed ||
                ingredient.IntoleranceReviewState != IngredientPropertyReviewState.Reviewed ||
                ingredient.OriginReviewState != IngredientPropertyReviewState.Reviewed;
        });
        foreach (var definition in allergens)
        {
            var problems = evaluator.Evaluate(solution, new RequirementSignature(null, [definition.Id], [])).Problems;
            unknownData |= problems.Any(problem => problem.Category == MealPlanningProblemCategory.DataQuality);
            string state = problems.Any(problem => problem.Code == MealPlanningProblemCode.AllergenConflict) ? "Contains"
                : problems.Count > 0 ? "Unknown" : "NotContained";
            hints.Add(new("Allergen", definition.Id, definition.Code, definition.Name, state,
                state == "Contains" ? "Im Standardrezept enthalten; bei entsprechender Anforderung kann eine Anpassung nötig sein."
                : state == "Unknown" ? "Angaben fehlen, sind ungeprüft oder lassen ein mögliches Vorkommen offen."
                : "Nach den geprüften Zutatenangaben im Standardzustand nicht enthalten. Keine individuelle Freigabe.",
                Evidence(problems), state == "Contains" ? Adjustment(new RequirementSignature(null, [definition.Id], []))
                    : state == "NotContained" ? "NotNeeded" : "Unknown"));
        }
        foreach (var definition in diets)
        {
            var problems = evaluator.Evaluate(solution, new RequirementSignature(definition.Id, [], [])).Problems;
            unknownData |= problems.Any(problem => problem.Category == MealPlanningProblemCategory.DataQuality);
            string state = problems.Any(problem => problem.Code == MealPlanningProblemCode.DietTypeExcludedOrigin) ? "Excluded"
                : problems.Count > 0 ? "Unknown" : "Compatible";
            hints.Add(new("Diet", definition.Id, definition.Code, definition.Name, state,
                state == "Compatible" ? "Die Standardzutaten passen zu den aktuell hinterlegten Herkunftsregeln dieser Ernährungsform."
                : state == "Excluded" ? "Mindestens eine Standardzutat ist nach den hinterlegten Herkunftsregeln ausgeschlossen."
                : "Herkunftsangaben oder Regeln sind unvollständig; keine Eignungsaussage möglich.", Evidence(problems),
                state == "Excluded" ? Adjustment(new RequirementSignature(definition.Id, [], []))
                    : state == "Compatible" ? "NotNeeded" : "Unknown"));
        }
        foreach (var definition in substances)
        {
            var affected = new List<RecipeHintEvidence>();
            bool unknown = ingredients.Count == 0, quantityDependent = false, contained = false;
            bool quantitativeAllowed = references.IsQuantitativeSubstance(definition.Id);
            foreach (var position in ingredients)
            {
                var ingredient = references.Ingredient(position.IngredientId);
                var profile = IngredientSubstanceProfile.Resolve(definition.Id, ingredient.SubstanceContents, ingredient.Intolerances);
                bool knownQuantitative = quantitativeAllowed && profile.Content?.ReviewState == IngredientSubstanceContentReviewState.Reviewed &&
                    references.Unit(profile.Content.AmountUnitId).Dimension == MeasurementDimension.Mass &&
                    references.Unit(profile.Content.ReferenceUnitId).Dimension == references.Unit(ingredient.BaseUnitId).Dimension;
                bool knownQualitative = profile.Mode == IngredientSubstanceAssessmentMode.Qualitative &&
                    ingredient.IntoleranceReviewState == IngredientPropertyReviewState.Reviewed;
                bool missing = !knownQuantitative && !knownQualitative;
                bool positive = knownQuantitative ? profile.Content!.Amount > 0 : knownQualitative && profile.Contained == true;
                unknown |= missing; quantityDependent |= positive && knownQuantitative; contained |= positive;
                if (positive || missing) affected.Add(ToEvidence(position));
            }
            // Mixed known presence and data gaps remain visibly incomplete rather than gaining a clearance.
            unknownData |= unknown;
            string state = quantityDependent ? "QuantityDependent" : contained ? "Contains" : unknown ? "Unknown" : "NotContained";
            hints.Add(new("Substance", definition.Id, definition.Code, definition.Name, state,
                unknown ? (contained ? "Stoffdaten sind teilweise vorhanden; weitere Zutatenangaben fehlen oder sind ungeprüft. Mengenabhängige Eignung ist offen."
                    : "Stoffdaten fehlen oder sind ungeprüft. Daraus darf keine Stofffreiheit abgeleitet werden.")
                : quantityDependent ? "Quantitative Stoffdaten liegen vor. Die Eignung hängt von Portionsmengen und individuellen Grenzwerten ab."
                : contained ? "Nach den qualitativen Zutatenangaben enthalten. Bei entsprechender Anforderung ist eine Prüfung erforderlich."
                : "Nach den vorhandenen geprüften Zutatenangaben nicht enthalten. Keine pauschale Frei-von-Kennzeichnung.", affected,
                state == "Contains" && !quantitativeAllowed && !unknown ? Adjustment(new RequirementSignature(null, [], [new(definition.Id, null)]))
                    : state == "NotContained" ? "NotNeeded" : "Unknown"));
        }
        return new(revisionId, "PublishedStandard", evaluatedAt, unknownData || hints.Any(hint => hint.State == "Unknown"), hints);

        IReadOnlyList<RecipeHintEvidence> Evidence(IReadOnlyList<MealPlanningProblem> problems) =>
            ingredients.Where(position => problems.Count == 0 || problems.Any(problem => problem.PositionPath == position.PositionPath))
                .Select(ToEvidence).Distinct().ToArray();

        string Adjustment(RequirementSignature requirement)
        {
            ct.ThrowIfCancellationRequested();
            if (adjustmentBudget.IsCancellationRequested) return "Unknown";
            try
            {
                // No other offer recipes: only explicit ingredient variants and replacement decisions.
                var result = resolver.Resolve(solution, requirement, [], adjustmentBudget.Token);
                bool complete = result.Stage == SupplyResolutionStage.Direct && result.Selected?.PositionSelections.Count > 0 ||
                    result.Problems.Any(problem => problem.Code == MealPlanningProblemCode.MultipleDirectSolutions);
                return complete ? "Available" : "Unavailable";
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return "Unknown"; }
        }
    }

    private static RecipeHintEvidence ToEvidence(CalculatedIngredient position) =>
        new(position.IngredientId, position.IngredientName, position.PositionPath);

    // Request-scoped memoization: each pinned revision, ingredient and policy is resolved once.
    private sealed class CachedReferences(ISupplySuitabilityReferences source) : ISupplySuitabilityReferences
    {
        private readonly Dictionary<Guid, RecipeSnapshot> recipes = [];
        private readonly Dictionary<Guid, IngredientRevisionDraftContent> ingredients = [];
        private readonly Dictionary<Guid, MeasurementUnit> units = [];
        private readonly Dictionary<(Guid, Guid), DietaryOriginDecision?> decisions = [];
        private readonly Dictionary<Guid, bool> quantitative = [];
        private readonly Dictionary<(Guid, string, Guid), (IngredientSnapshotSource Ingredient, IngredientUnitSnapshot Unit)> variants = [];
        private readonly IReadOnlyList<IngredientPropertyDefinition> allergens = source.Allergens;
        private readonly IReadOnlyList<IngredientPropertyDefinition> origins = source.MainOrigins;
        public RecipeSnapshot GetRevision(Guid id) => recipes.TryGetValue(id, out var value) ? value : recipes[id] = source.GetRevision(id);
        public IngredientRevisionDraftContent Ingredient(Guid id) => ingredients.TryGetValue(id, out var value) ? value : ingredients[id] = source.Ingredient(id);
        public MeasurementUnit Unit(Guid id) => units.TryGetValue(id, out var value) ? value : units[id] = source.Unit(id);
        public IReadOnlyList<IngredientPropertyDefinition> Allergens => allergens;
        public IReadOnlyList<IngredientPropertyDefinition> MainOrigins => origins;
        public bool IsQuantitativeSubstance(Guid id) => quantitative.TryGetValue(id, out var value) ? value : quantitative[id] = source.IsQuantitativeSubstance(id);
        public DietaryOriginDecision? DietDecision(Guid diet, Guid origin) => decisions.TryGetValue((diet, origin), out var value) ? value : decisions[(diet, origin)] = source.DietDecision(diet, origin);
        public (IngredientSnapshotSource Ingredient, IngredientUnitSnapshot Unit) ResolveVariant(Guid id, string key, Guid unit) =>
            variants.TryGetValue((id, key, unit), out var value) ? value : variants[(id, key, unit)] = source.ResolveVariant(id, key, unit);
    }
}
