using System.Text.Json;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record SupplyPositionChoice(string Label, Recipes.RecipePositionSelection? Selection);
public sealed record SupplyPositionOption(string Path, string Name, IReadOnlyList<SupplyPositionChoice> Choices);

public sealed partial class MealPlanningService
{
    public async Task<IReadOnlyList<SupplyPositionOption>?> GetSupplyPositionsAsync(Guid campId, Guid recipeRevisionId, CancellationToken ct = default)
    {
        var camp = await campPlanning.GetPlanningDataAsync(campId, ct);
        if (camp is null || supplyReferences is null ||
            !await store.AreRecipeRevisionsAccessibleAsync(campId, camp.TenantId, [recipeRevisionId], ct)) return null;
        var result = new List<SupplyPositionOption>();
        Visit(recipeRevisionId, recipeRevisionId.ToString("N"), []);
        return result;

        void Visit(Guid revisionId, string path, HashSet<Guid> ancestors)
        {
            ct.ThrowIfCancellationRequested();
            if (!ancestors.Add(revisionId)) throw new InvalidOperationException("Recipe cycle.");
            var recipe = supplyReferences.GetRevision(revisionId);
            foreach (var position in recipe.IngredientPositions)
            {
                var choices = new List<SupplyPositionChoice> { new("Original", null) };
                AddVariants(position.Ingredient.IngredientRevisionId, null, position.Ingredient.Name);
                foreach (var replacement in position.Replacements)
                {
                    choices.Add(new(replacement.Ingredient.Name, new(replacement.Id)));
                    AddVariants(replacement.Ingredient.IngredientRevisionId, replacement.Id, replacement.Ingredient.Name);
                }
                result.Add(new(path + "/" + position.Id.ToString("N"), position.Ingredient.Name, choices));
                void AddVariants(Guid ingredient, Guid? replacement, string name)
                {
                    foreach (var variant in (supplyReferences.Ingredient(ingredient).Variants ?? []).Where(value => value.IsActive))
                        choices.Add(new(name + " · " + variant.Name, new(replacement, variant.VariantKey)));
                }
            }
            foreach (var child in recipe.SubrecipePositions)
            {
                var childPath = path + "/" + child.Id.ToString("N");
                result.Add(new(childPath, supplyReferences.GetRevision(child.RecipeRevisionId).Name,
                    new[] { new SupplyPositionChoice("Original", null) }.Concat(child.Replacements.Select(value =>
                        new SupplyPositionChoice(supplyReferences.GetRevision(value.RecipeRevisionId).Name, new(value.Id)))).ToArray()));
                Visit(child.RecipeRevisionId, childPath + "/" + child.RecipeRevisionId.ToString("N"), new(ancestors));
            }
        }
    }

    public async Task<MealPlanningMutationResult> VerifySupplyAsync(Guid campId, Guid cookingUnitId, Guid mealId,
        Guid actorId, VerifyMealRequest request, CancellationToken ct = default)
    {
        if (actorId == Guid.Empty || !Enum.IsDefined(request.Kind) || request.Reason?.Length > 2000 ||
            request.Kind == MealVerificationKind.VerifiedWithDeviation && string.IsNullOrWhiteSpace(request.Reason))
            return Invalid("verification_reason_required", "Für eine Abweichung ist eine Begründung erforderlich (maximal 2000 Zeichen).");
        var overview = await GetOverviewAsync(campId, ct);
        var current = overview?.OperationalMeals.SingleOrDefault(value => value.CookingUnitId == cookingUnitId && value.CampMealId == mealId);
        if (current is null) return NotFound();
        if (current.SupplyVersion != request.ExpectedVersion) return new(MealPlanningMutationStatus.Conflict, "supply_version_conflict");
        if (current.Supply is null || current.Status == OperationalMealPlanStatus.Stale)
            return Invalid("recalculate_before_verification", "Bitte den aktuellen Versorgungsstand zuerst neu berechnen.");
        if (request.Kind == MealVerificationKind.Clean && (current.Status != OperationalMealPlanStatus.Current ||
            current.Supply.Problems.Count > 0 || current.ParticipantProblems?.Count > 0 || current.UnassignedParticipantIds?.Count > 0))
            return Invalid("clean_verification_blocked", "Offene Probleme oder unvollständige Daten verhindern eine Verifikation ohne Abweichung.");
        var data = await store.LoadAsync(campId, ct);
        var state = data.MealStates.Single(value => value.Id == current.Id);
        if (state.SupplyVersion != request.ExpectedVersion) return new(MealPlanningMutationStatus.Conflict, "supply_version_conflict");
        var snapshot = new MealVerificationSnapshot(cookingUnitId, mealId, state.MealPlanId, state.MealPlanVersion,
            state.MealPlanSnapshotId, state.DemandBasis, state.EffectiveDemand, current.RequirementGroups ?? [], current.Supply,
            request.Kind, request.Kind == MealVerificationKind.Clean ? null : request.Reason!.Trim(), actorId, timeProvider.GetUtcNow());
        return await store.SaveSupplyStateAsync(campId, cookingUnitId, mealId, request.ExpectedVersion, null,
            JsonSerializer.Serialize(snapshot, JsonOptions), state.SourceFingerprint, ct);
    }

    private static T? ReadCalculation<T>(CookingUnitMealState state, string property)
    {
        if (state.CalculationSnapshotJson is null) return default;
        using var json = JsonDocument.Parse(state.CalculationSnapshotJson);
        return json.RootElement.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.Deserialize<T>(JsonOptions) : default;
    }

    public static IReadOnlyList<SupplySolution> ReadManualSupply(CookingUnitMealState? state) => state is null ? [] :
        JsonSerializer.Deserialize<SupplySolution[]>(state.ManualSupplySolutionsJson, JsonOptions) ?? [];

    private object BuildSupplyFingerprint(CookingUnit unit, CampMeal meal, MealPlanSubscriptionState subscription,
        MealPlan? plan, MealPlanningData data, OperationalParticipantDemand demand)
    {
        var state = data.MealStates.SingleOrDefault(value => value.CookingUnitId == unit.Id && value.CampMealId == meal.Id);
        var groups = data.OfferGroups.Where(value => value.MealPlanId == plan?.Id && value.CampMealId == meal.Id)
            .Select(value => value.Id).Order().ToArray();
        var entries = data.Entries.Where(value => groups.Contains(value.OfferGroupId))
            .OrderBy(value => value.Id).Select(value => new { value.OfferGroupId, value.RecipeRevisionId, value.IsStandard }).ToArray();
        var choices = data.RecipeChoices.Where(value => value.CookingUnitMealStateId == state?.Id && subscription == MealPlanSubscriptionState.Custom)
            .OrderBy(value => value.Id).Select(value => new { value.RecipeRevisionId, value.OfferGroupId }).ToArray();
        var manual = ReadManualSupply(state);
        var origins = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        if (supplyReferences is not null && demand.RequirementGroups.Any(value => value.Requirements.DietTypeId.HasValue))
            foreach (Guid id in entries.Select(value => value.RecipeRevisionId).Concat(choices.Select(value => value.RecipeRevisionId))
                .Concat(manual.Select(value => value.RecipeRevisionId)).Distinct()) Visit(id);
        return new {
            unit.TogetherPreference, state?.DemandOverride, entries, choices,
            manual = manual.OrderBy(value => value.Id).Select(value => new {
                value.Id, value.RecipeRevisionId, value.OfferGroupId, value.Portions, value.Source,
                positions = value.PositionSelections.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
                allocations = value.Allocations.OrderBy(item => item.RequirementGroupKey, StringComparer.Ordinal).ToArray()
            }).ToArray(),
            targets = data.OfferTargets.Where(value => value.CookingUnitMealStateId == state?.Id)
                .OrderBy(value => value.OfferGroupId).Select(value => new { value.OfferGroupId, value.TargetOverride }).ToArray(),
            dietRules = demand.RequirementGroups.Select(value => value.Requirements.DietTypeId).OfType<Guid>().Distinct().Order()
                .SelectMany(diet => origins.Order().Select(origin => new { diet, origin, decision = supplyReferences!.DietDecision(diet, origin) })).ToArray()
        };

        void Visit(Guid revisionId)
        {
            if (!visited.Add(revisionId)) return;
            var recipe = supplyReferences!.GetRevision(revisionId);
            foreach (var position in recipe.IngredientPositions)
                foreach (Guid ingredientId in position.Replacements.Select(value => value.Ingredient.IngredientRevisionId)
                    .Append(position.Ingredient.IngredientRevisionId))
                {
                    var ingredient = supplyReferences.Ingredient(ingredientId);
                    foreach (var origin in ingredient.Origins.Concat((ingredient.Variants ?? [])
                        .Where(value => value.IsActive).SelectMany(value => value.OriginOverrides)))
                        if (origin.State == IngredientPropertyState.Contains && supplyReferences.MainOrigins.Any(value => value.Id == origin.PropertyId))
                            origins.Add(origin.PropertyId);
                }
            foreach (var child in recipe.SubrecipePositions)
            {
                Visit(child.RecipeRevisionId);
                foreach (var replacement in child.Replacements) Visit(replacement.RecipeRevisionId);
            }
        }
    }

    public async Task<MealPlanningMutationResult> SaveSupplyAsync(Guid campId, Guid cookingUnitId, Guid mealId,
        SaveSupplyRequest request, CancellationToken ct = default)
    {
        var camp = await campPlanning.GetPlanningDataAsync(campId, ct);
        if (camp is null || supplyReferences is null) return NotFound();
        if (request.Solutions is null || request.Solutions.Any(value => value is null) || request.Solutions.Select(value => value.Id).Distinct().Count() != request.Solutions.Count)
            return Invalid("invalid_supply", "Versorgungslösungen sind ungültig.");
        try
        {
            foreach (var solution in request.Solutions)
            {
                solution.Validate();
                if (solution.Source != SupplyDecisionSource.Manual) return Invalid("invalid_supply_source", "Manuelle Entscheidung erforderlich.");
            }
            var decisionKeys = request.Solutions.Select(solution => JsonSerializer.Serialize(new {
                solution.MergeKey, solution.Portions,
                allocations = solution.Allocations.OrderBy(value => value.RequirementGroupKey, StringComparer.Ordinal).ToArray()
            })).ToArray();
            if (decisionKeys.Distinct(StringComparer.Ordinal).Count() != decisionKeys.Length)
                return Invalid("duplicate_supply_decision", "Diese Versorgungslösung und Gruppenzuordnung wurde bereits gewählt.");
        }
        catch (ArgumentException) { return Invalid("invalid_supply", "Ungültige Portions- oder Positionsentscheidung."); }
        if (!await store.AreRecipeRevisionsAccessibleAsync(campId, camp.TenantId,
            request.Solutions.Select(value => value.RecipeRevisionId).Distinct().ToArray(), ct)) return NotFound();
        try
        {
            foreach (var solution in request.Solutions)
                _ = new SupplySuitabilityEvaluator(supplyReferences).Evaluate(solution, new(null, [], []));
        }
        catch (ArgumentException) { return Invalid("invalid_supply", "Ungültige Portions- oder Positionsentscheidung."); }
        catch (InvalidOperationException) { return Invalid("invalid_supply_reference", "Rezeptposition oder Variante ist nicht verfügbar."); }
        var data = await store.LoadAsync(campId, ct);
        if (request.Solutions.Any(value => value.OfferGroupId.HasValue && !data.OfferGroups.Any(group =>
                group.Id == value.OfferGroupId && group.CampMealId == mealId)))
            return Invalid("invalid_supply_offer", "Angebotsgruppe gehört nicht zu dieser Mahlzeit.");
        return await store.SaveSupplyStateAsync(campId, cookingUnitId, mealId, request.ExpectedVersion,
            JsonSerializer.Serialize(request.Solutions, JsonOptions), null, null, ct);
    }
}
