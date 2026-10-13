using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public enum SupplyResolutionStage { Direct, OfferAlternative, Manual }
public sealed record SupplyResolution(SupplyResolutionStage Stage, SupplySolution? Selected,
    IReadOnlyList<SupplySolution> Candidates, IReadOnlyList<MealPlanningProblem> Problems);

/// <summary>Only explicit recipe decisions and the supplied concrete offer group are searched.</summary>
public sealed class SupplySolutionResolver(ISupplySuitabilityReferences references)
{
    private readonly SupplySuitabilityEvaluator evaluator = new(references);

    public SupplyResolution Resolve(SupplySolution standard, RequirementSignature requirements,
        IReadOnlyList<Guid> offerAlternativeRevisionIds, CancellationToken cancellationToken = default)
        => ResolveTogether(standard, [requirements], offerAlternativeRevisionIds, cancellationToken);

    public SupplyResolution ResolveTogether(SupplySolution standard, IReadOnlyList<RequirementSignature> requirements,
        IReadOnlyList<Guid> offerAlternativeRevisionIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var original = Assess(standard);
        if (standard.Source == SupplyDecisionSource.Manual)
            return new(SupplyResolutionStage.Manual, standard, [], original.IsSuitable ? [] :
                original.Problems.Concat(requirements.Select(group => new MealPlanningProblem(MealPlanningProblemCategory.ManualDecision,
                    MealPlanningProblemCode.ManualDecisionNoLongerSuitable, group.Key, standard.Id))).ToArray());
        if (original.IsSuitable) return new(SupplyResolutionStage.Direct, standard, [], []);

        var matching = new List<SupplySolution>();
        var partial = new List<SupplySolution>();
        foreach (var selection in Decisions(standard.RecipeRevisionId, standard.RecipeRevisionId.ToString("N"), requirements, [], cancellationToken))
        {
            if (selection.Count == 0) continue;
            var candidate = standard with { PositionSelections = selection };
            var assessment = Assess(candidate);
            if (assessment.IsSuitable) matching.Add(candidate);
            else if (original.Problems.Any(problem => !assessment.Problems.Contains(problem))) partial.Add(candidate);
            // Two are enough to prove ambiguity; never pick based on ordering.
            if (matching.Count == 2) break;
        }
        if (matching.Count == 1) return new(SupplyResolutionStage.Direct, matching[0], [], []);

        var alternatives = offerAlternativeRevisionIds.Distinct().Where(id => id != standard.RecipeRevisionId)
            .Select(id => standard with { RecipeRevisionId = id, PositionSelections = new Dictionary<string, RecipePositionSelection>() })
            .Where(value => {
                cancellationToken.ThrowIfCancellationRequested();
                return Assess(value).IsSuitable;
            }).ToArray();
        if (alternatives.Length == 1) return new(SupplyResolutionStage.OfferAlternative, alternatives[0], [], []);

        var problems = original.Problems.ToList();
        foreach (var group in requirements)
        {
        problems.Add(new(MealPlanningProblemCategory.Suitability, matching.Count > 1
            ? MealPlanningProblemCode.MultipleDirectSolutions : partial.Count > 0
                ? MealPlanningProblemCode.PartialDirectResolution : MealPlanningProblemCode.NoMatchingDirectSolution,
            group.Key));
        problems.Add(new(MealPlanningProblemCategory.Suitability, alternatives.Length > 1
            ? MealPlanningProblemCode.MultipleMatchingOfferAlternatives : MealPlanningProblemCode.NoMatchingOfferAlternative,
            group.Key));
        }
        return new(SupplyResolutionStage.Manual, null, matching.Concat(alternatives).Concat(partial).ToArray(), problems);

        SupplySuitabilityResult Assess(SupplySolution solution) => new([], requirements
            .SelectMany(group => evaluator.Evaluate(solution, group).Problems).Distinct().ToArray());
    }

    private IEnumerable<Dictionary<string, RecipePositionSelection>> Decisions(Guid revisionId, string path,
        IReadOnlyList<RequirementSignature> requirements, HashSet<Guid> ancestors, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ancestors.Add(revisionId)) throw new InvalidOperationException("Recipe cycle in supply decisions.");
        var recipe = references.GetRevision(revisionId);
        var options = new List<IReadOnlyList<Dictionary<string, RecipePositionSelection>>>();
        foreach (var position in recipe.IngredientPositions)
        {
            string key = path + "/" + position.Id.ToString("N");
            var choices = new List<Dictionary<string, RecipePositionSelection>> { new() };
            AddVariants(position.Ingredient.IngredientRevisionId, null);
            foreach (var replacement in position.Replacements.Where(value => requirements.Any(group => Applicable(value.ApplicableConflicts, group))))
            {
                choices.Add(new() { [key] = new(replacement.Id) });
                AddVariants(replacement.Ingredient.IngredientRevisionId, replacement.Id);
            }
            options.Add(choices);

            void AddVariants(Guid ingredientId, Guid? replacementId)
            {
                foreach (var variant in (references.Ingredient(ingredientId).Variants ?? []).Where(value => value.IsActive))
                    choices.Add(new() { [key] = new(replacementId, variant.VariantKey) });
            }
        }
        foreach (var position in recipe.SubrecipePositions)
        {
            string key = path + "/" + position.Id.ToString("N");
            var choices = Decisions(position.RecipeRevisionId, key + "/" + position.RecipeRevisionId.ToString("N"),
                requirements, new HashSet<Guid>(ancestors), cancellationToken).ToList();
            // Replacement subtree is evaluated as-is; do not search its replacement rules again.
            foreach (var replacement in position.Replacements.Where(value => requirements.Any(group => Applicable(value.ApplicableConflicts, group))))
                choices.Add(new() { [key] = new(replacement.Id) });
            options.Add(choices);
        }
        return Combine(options, 0, new(), cancellationToken);
    }

    private static IEnumerable<Dictionary<string, RecipePositionSelection>> Combine(
        IReadOnlyList<IReadOnlyList<Dictionary<string, RecipePositionSelection>>> options, int index,
        Dictionary<string, RecipePositionSelection> current, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (index == options.Count) { yield return current; yield break; }
        foreach (var option in options[index])
        {
            var next = new Dictionary<string, RecipePositionSelection>(current, StringComparer.Ordinal);
            foreach (var value in option) next.Add(value.Key, value.Value);
            foreach (var combined in Combine(options, index + 1, next, cancellationToken)) yield return combined;
        }
    }

    private static bool Applicable(IReadOnlyList<ConflictReference> conflicts, RequirementSignature requirements) =>
        conflicts.Any(value => value.Type switch {
            ConflictType.Allergen => requirements.AllergenIds.Contains(value.Id),
            ConflictType.Intolerance => requirements.Intolerances.Any(item => item.SubstanceId == value.Id),
            ConflictType.DietaryRequirement => requirements.DietTypeId == value.Id,
            _ => false
        });
}
