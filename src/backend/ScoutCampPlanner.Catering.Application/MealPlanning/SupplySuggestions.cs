using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record SupplySuggestion(string Id, string Scope, string Label, string RecipeName, bool Preferred, SupplySolution Solution);
public sealed record SupplySuggestionOffer(Guid? OfferGroupId, string Name, CookingTogetherPreference Preference, decimal MissingPersons, decimal CoveredPersons, IReadOnlyList<SupplySuggestion> Options);
public sealed record SupplySuggestionDocument(long ExpectedVersion, IReadOnlyList<SupplySuggestionOffer> Offers, string? UnavailableReason = null);
public sealed record SupplyPreviewItem(Guid SolutionId, IReadOnlyList<MealPlanningProblem> Problems, string? Error = null);
public sealed record ApplySupplySuggestionRequest(long ExpectedVersion, string SuggestionId);

public sealed class SupplySuggestionBuilder(ISupplySuitabilityReferences references)
{
    public SupplySuggestionDocument Build(CookingUnitMealDocument state, CookingTogetherPreference defaultPreference,
        IReadOnlyList<MealPlanningRecipeOption> recipes, IReadOnlyDictionary<Guid, string> offerNames, CancellationToken ct = default)
    {
        if (state.Supply is null || state.Status == OperationalMealPlanStatus.Stale || state.SubscriptionState == MealPlanSubscriptionState.NoSupplyRequired)
            return new(state.SupplyVersion, [], "recalculate_required");
        var result = new List<SupplySuggestionOffer>();
        var resolver = new SupplySolutionResolver(references);
        var requirements = state.Supply.Requirements ?? [];
        foreach (var offerId in state.Supply.Problems.Where(value => value.Code == MealPlanningProblemCode.RequirementGroupUndercovered && value.RequirementGroupKey is not null)
            .Select(value => value.ReferenceId).Distinct())
        {
            ct.ThrowIfCancellationRequested();
            var supplied = state.Supply.Solutions.Where(value => value.OfferGroupId == offerId).SelectMany(value => value.Allocations)
                .GroupBy(value => value.RequirementGroupKey).ToDictionary(value => value.Key, value => value.Sum(item => item.Portions));
            var missing = requirements.Select(value => value with { Portions = Math.Max(0, value.Portions - supplied.GetValueOrDefault(value.Requirements.Key)) })
                .Where(value => value.Portions > 0 && state.Supply.Problems.Any(problem => problem.Code == MealPlanningProblemCode.RequirementGroupUndercovered && problem.ReferenceId == offerId && problem.RequirementGroupKey == value.Requirements.Key)).ToArray();
            if (missing.Length == 0) continue;
            var preference = state.ManualScenarioSelections?.SingleOrDefault(value => value.OfferGroupId == offerId)?.Preference ?? defaultPreference;
            var scopes = new List<(string Kind, string Label, SupplyRequirement[] Groups)>();
            if (preference == CookingTogetherPreference.AlwaysTogether)
                scopes.Add(("whole", "Gemeinsame Versorgung", requirements.Where(value => value.Portions > 0).ToArray()));
            scopes.Add(("missing", "Alle offenen Personen versorgen", missing));
            if (preference != CookingTogetherPreference.AlwaysTogether && missing.Length > 1)
                scopes.AddRange(missing.Select(value => ("single:" + value.Requirements.Key, "Einzelnen offenen Bedarf versorgen", new[] { value })));
            var options = new List<SupplySuggestion>();
            foreach (var scope in scopes)
                foreach (var recipe in recipes.Where(value => value.IsPortionBased).OrderBy(value => value.Name).ThenBy(value => value.RevisionId))
                {
                    ct.ThrowIfCancellationRequested();
                    var template = new SupplySolution(Guid.NewGuid(), recipe.RevisionId, offerId, scope.Groups.Sum(value => value.Portions),
                        new Dictionary<string, Recipes.RecipePositionSelection>(), scope.Groups.Select(value => new SupplyGroupAllocation(value.Requirements.Key, value.Portions)).ToArray(), SupplyDecisionSource.Automatic);
                    // Invalid catalogue revisions cannot become suitable suggestions; do not hide infrastructure failures.
                    IReadOnlyList<SupplySolution> suitable;
                    try { suitable = resolver.FindSuitable(template, scope.Groups.Select(value => value.Requirements).ToArray(), ct); }
                    catch (ArgumentException) { continue; }
                    catch (InvalidOperationException) { continue; }
                    foreach (var solution in suitable.OrderBy(value => value.PositionSelections.Count))
                    {
                        var identity = JsonSerializer.Serialize(new { scope.Kind, solution.MergeKey, solution.Portions, solution.Allocations });
                        string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
                        options.Add(new(id, scope.Kind, scope.Label, recipe.Name, preference == CookingTogetherPreference.AlwaysTogether ? scope.Kind == "whole" : scope.Kind == "missing", solution));
                    }
                }
            result.Add(new(offerId, offerId.HasValue ? offerNames.GetValueOrDefault(offerId.Value, "Komponente") : "Eigenes Angebot", preference,
                missing.Sum(value => value.Portions), supplied.Values.Sum(), options));
        }
        return new(state.SupplyVersion, result);
    }
}

public sealed partial class MealPlanningService
{
    public async Task<IReadOnlyList<SupplyPreviewItem>?> PreviewSupplyAsync(Guid campId, Guid unitId, Guid mealId,
        SaveSupplyRequest request, CancellationToken ct = default)
    {
        if (supplyReferences is null || request.Solutions is null || request.Solutions.Any(value => value is null)) return null;
        var camp = await campPlanning.GetPlanningDataAsync(campId, ct);
        if (camp is null || !await store.AreRecipeRevisionsAccessibleAsync(campId, camp.TenantId,
            request.Solutions.Select(value => value.RecipeRevisionId).Distinct().ToArray(), ct)) return null;
        var overview = await GetOverviewAsync(campId, ct);
        var current = overview?.OperationalMeals.SingleOrDefault(value => value.CookingUnitId == unitId && value.CampMealId == mealId);
        if (current is null) return null;
        var requirements = current.Supply?.Requirements ?? [];
        var evaluator = new SupplySuitabilityEvaluator(supplyReferences);
        return request.Solutions.Select(solution => {
            try
            {
                var problems = solution.Allocations.SelectMany(allocation => {
                    var group = requirements.SingleOrDefault(value => value.Requirements.Key == allocation.RequirementGroupKey);
                    return group is null ? new[] { new MealPlanningProblem(MealPlanningProblemCategory.Coverage,
                        MealPlanningProblemCode.UnassignedPlannedPortions, allocation.RequirementGroupKey) } : evaluator.Evaluate(solution, group.Requirements).Problems;
                }).Distinct().ToArray();
                return new SupplyPreviewItem(solution.Id, problems);
            }
            catch (ArgumentException) { return new SupplyPreviewItem(solution.Id, [], "Personenzahl oder Zutatenentscheidung prüfen."); }
            catch (InvalidOperationException) { return new SupplyPreviewItem(solution.Id, [], "Rezeptposition oder Variante ist nicht verfügbar."); }
        }).ToArray();
    }

    public async Task<SupplySuggestionDocument?> GetSupplySuggestionsAsync(Guid campId, Guid unitId, Guid mealId, CancellationToken ct = default)
    {
        if (supplyReferences is null) return null;
        var overview = await GetOverviewAsync(campId, ct);
        var state = overview?.OperationalMeals.SingleOrDefault(value => value.CookingUnitId == unitId && value.CampMealId == mealId);
        var unit = overview?.CookingUnits.SingleOrDefault(value => value.Id == unitId);
        if (state is null || unit is null) return null;
        var names = overview!.MealPlanDocuments.SelectMany(value => value.OfferGroups).GroupBy(value => value.Id).ToDictionary(value => value.Key, value => value.First().Name ?? "Komponente");
        return new SupplySuggestionBuilder(supplyReferences).Build(state, unit.TogetherPreference, overview.RecipeOptions, names, ct);
    }

    public async Task<MealPlanningMutationResult> ApplySupplySuggestionAsync(Guid campId, Guid unitId, Guid mealId, ApplySupplySuggestionRequest request, CancellationToken ct = default)
    {
        // Rebuild against current requirements and accessible revisions: never trust client suitability or allocations.
        var suggestions = await GetSupplySuggestionsAsync(campId, unitId, mealId, ct);
        if (suggestions is null) return NotFound();
        if (suggestions.ExpectedVersion != request.ExpectedVersion) return new(MealPlanningMutationStatus.Conflict, "supply_version_conflict");
        var selected = suggestions.Offers.SelectMany(value => value.Options).SingleOrDefault(value => value.Id == request.SuggestionId);
        if (suggestions.UnavailableReason is not null || selected is null) return new(MealPlanningMutationStatus.Conflict, "supply_suggestion_changed", "Vorschlag ist nicht mehr aktuell. Bitte neu laden.");
        var overview = await GetOverviewAsync(campId, ct);
        var state = overview!.OperationalMeals.Single(value => value.CookingUnitId == unitId && value.CampMealId == mealId);
        if (state.SupplyVersion != request.ExpectedVersion || state.Status == OperationalMealPlanStatus.Stale) return new(MealPlanningMutationStatus.Conflict, "supply_version_conflict");
        var solution = selected.Solution;
        var preserved = state.Supply!.Solutions.Where(value => selected.Scope == "whole"
            ? value.OfferGroupId != solution.OfferGroupId && value.Source == SupplyDecisionSource.Manual
            : value.Source == SupplyDecisionSource.Manual || value.OfferGroupId == solution.OfferGroupId && value.Allocations.Any(item => solution.Allocations.Any(next => next.RequirementGroupKey == item.RequirementGroupKey)));
        var manual = SupplySolutionCoverage.MergeIdentical(preserved.Append(solution).Select(value => value with { Source = SupplyDecisionSource.Manual }));
        return await SaveSupplyAsync(campId, unitId, mealId, new(request.ExpectedVersion, manual, state.ManualScenarioSelections, state.CompleteOwnSupply), ct);
    }
}
