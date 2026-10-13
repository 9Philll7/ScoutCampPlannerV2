using System.Text.Json;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public enum SupplyDecisionSource { Automatic, Manual }
public sealed record SupplyGroupAllocation(string RequirementGroupKey, decimal Portions);
public sealed record SupplySolution(Guid Id, Guid RecipeRevisionId, Guid? OfferGroupId,
    decimal Portions, IReadOnlyDictionary<string, RecipePositionSelection> PositionSelections,
    IReadOnlyList<SupplyGroupAllocation> Allocations, SupplyDecisionSource Source)
{
    public void Validate()
    {
        if (Allocations is null || PositionSelections is null || Allocations.Any(value => value is null) ||
            PositionSelections.Any(value => value.Value is null)) throw new ArgumentException("Missing supply decisions.");
        if (Id == Guid.Empty || RecipeRevisionId == Guid.Empty || OfferGroupId == Guid.Empty || Portions <= 0 ||
            !Enum.IsDefined(Source)) throw new ArgumentException("Invalid supply solution.");
        if (Allocations.Any(value => string.IsNullOrWhiteSpace(value.RequirementGroupKey) || value.Portions < 0) ||
            Allocations.Select(value => value.RequirementGroupKey).Distinct().Count() != Allocations.Count)
            throw new ArgumentException("Invalid or duplicate group allocation.");
        foreach (var (path, choice) in PositionSelections)
        {
            string[] segments = path.Split('/');
            if (segments.Length < 2 || segments.Any(segment => !Guid.TryParseExact(segment, "N", out var id) || id == Guid.Empty) ||
                segments[0] != RecipeRevisionId.ToString("N") || choice.ReplacementRuleId == Guid.Empty ||
                choice.ReplacementRuleId is null && string.IsNullOrWhiteSpace(choice.VariantKey))
                throw new ArgumentException("Invalid occurrence decision.");
        }
    }

    // Group allocation is deliberately excluded; provenance and offer context are not.
    public string MergeKey => JsonSerializer.Serialize(new {
        RecipeRevisionId, OfferGroupId, Source,
        Decisions = PositionSelections.OrderBy(value => value.Key, StringComparer.Ordinal).ToArray()
    });
}

public sealed record SupplyCoverageTarget(Guid? OfferGroupId, string RequirementGroupKey, decimal Portions);

public static class SupplySolutionCoverage
{
    public static IReadOnlyList<SupplySolution> MergeIdentical(IEnumerable<SupplySolution> solutions) =>
        solutions.GroupBy(value => value.MergeKey).Select(group => group.First() with {
            Portions = group.Sum(value => value.Portions),
            Allocations = group.SelectMany(value => value.Allocations).GroupBy(value => value.RequirementGroupKey)
                .Select(values => new SupplyGroupAllocation(values.Key, values.Sum(value => value.Portions))).ToArray()
        }).ToArray();

    public static IReadOnlyList<MealPlanningProblem> Evaluate(
        IReadOnlyList<SupplySolution> solutions, IReadOnlyList<SupplyCoverageTarget> targets)
    {
        var problems = new List<MealPlanningProblem>();
        foreach (var solution in solutions)
        {
            solution.Validate();
            if (solution.Allocations.Sum(value => value.Portions) != solution.Portions ||
                solution.Allocations.Any(value => !targets.Any(target => target.OfferGroupId == solution.OfferGroupId &&
                    target.RequirementGroupKey == value.RequirementGroupKey)))
                problems.Add(new(MealPlanningProblemCategory.Coverage, MealPlanningProblemCode.UnassignedPlannedPortions,
                    ReferenceId: solution.Id));
        }
        foreach (var target in targets)
        {
            decimal supplied = solutions.Where(solution => solution.OfferGroupId == target.OfferGroupId)
                .SelectMany(solution => solution.Allocations).Where(value => value.RequirementGroupKey == target.RequirementGroupKey)
                .Sum(value => value.Portions);
            if (supplied != target.Portions) problems.Add(new(MealPlanningProblemCategory.Coverage,
                supplied < target.Portions ? MealPlanningProblemCode.RequirementGroupUndercovered : MealPlanningProblemCode.RequirementGroupOvercovered,
                target.RequirementGroupKey, target.OfferGroupId));
        }
        return problems;
    }
}

/// <summary>Published revision data only. Infrastructure supplies this projection; no participant names.</summary>
public interface ISupplySuitabilityReferences : IRecipeSnapshotSource, IRecipeVariantSource
{
    IngredientRevisionDraftContent Ingredient(Guid revisionId);
    MeasurementUnit Unit(Guid id);
    IReadOnlyList<IngredientPropertyDefinition> Allergens { get; }
    IReadOnlyList<IngredientPropertyDefinition> MainOrigins { get; }
    bool IsQuantitativeSubstance(Guid id);
    DietaryOriginDecision? DietDecision(Guid dietTypeId, Guid originId);
}

public sealed record SupplySuitabilityResult(IReadOnlyList<CalculatedIngredient> Ingredients,
    IReadOnlyList<MealPlanningProblem> Problems)
{
    public bool IsSuitable => Problems.Count == 0;
}

public sealed class SupplySuitabilityEvaluator(ISupplySuitabilityReferences references)
{
    public SupplySuitabilityResult Evaluate(SupplySolution solution, RequirementSignature requirements)
    {
        solution.Validate();
        // Calculate the entire batch first: Fixed/Stepwise quantities cannot be evaluated at demand 1.
        var calculated = new RecipeCalculator(references, references).Calculate(new(
            solution.RecipeRevisionId, solution.Portions, solution.Portions, PositionSelections: solution.PositionSelections));
        var problems = new List<MealPlanningProblem>();
        var substances = requirements.Intolerances.ToDictionary(value => value.SubstanceId,
            _ => new List<SubstancePortionContribution>());
        foreach (var position in calculated.Ingredients)
        {
            var ingredient = references.Ingredient(position.IngredientId);
            var variant = position.VariantKey is null ? null : ingredient.Variants?.SingleOrDefault(value => value.VariantKey == position.VariantKey)
                ?? throw new InvalidOperationException("Selected variant is absent from the pinned ingredient revision.");
            foreach (var allergenId in requirements.AllergenIds)
            {
                var ids = references.Allergens.Where(value => value.Id == allergenId || IsDescendant(value, allergenId)).Select(value => value.Id).ToArray();
                var states = ids.Select(id => IngredientPropertyResolver.EffectiveState(ingredient.Allergens, variant?.AllergenOverrides, id)).ToArray();
                if (states.Contains(IngredientPropertyState.Contains))
                    Problem(MealPlanningProblemCategory.Suitability, MealPlanningProblemCode.AllergenConflict, allergenId, position.PositionPath);
                else if (states.Length == 0 || states.Any(value => value != IngredientPropertyState.DoesNotContain) ||
                    ingredient.AllergenReviewState != IngredientPropertyReviewState.Reviewed)
                    Problem(MealPlanningProblemCategory.DataQuality, MealPlanningProblemCode.UnknownAllergenData, allergenId, position.PositionPath);
            }
            if (requirements.DietTypeId is Guid diet)
            {
                var origins = references.MainOrigins.Where(origin => IngredientPropertyResolver.EffectiveState(
                    ingredient.Origins, variant?.OriginOverrides, origin.Id) == IngredientPropertyState.Contains).ToArray();
                var decision = origins.Length == 1 && origins[0].Code != "UNKNOWN_ORIGIN" &&
                    ingredient.OriginReviewState == IngredientPropertyReviewState.Reviewed
                    ? references.DietDecision(diet, origins[0].Id) : null;
                if (decision != DietaryOriginDecision.Allowed)
                    Problem(decision is null ? MealPlanningProblemCategory.DataQuality : MealPlanningProblemCategory.Suitability,
                        decision is null ? MealPlanningProblemCode.UnknownDietTypeRule : MealPlanningProblemCode.DietTypeExcludedOrigin,
                        diet, position.PositionPath);
            }
            foreach (var requirement in requirements.Intolerances)
            {
                var profile = IngredientSubstanceProfile.Resolve(requirement.SubstanceId, ingredient.SubstanceContents,
                    ingredient.Intolerances, variant?.SubstanceContentOverrides, variant?.IntoleranceOverrides);
                var contribution = SubstancePortionContribution.Unknown(position.PositionPath);
                if (profile.Mode == IngredientSubstanceAssessmentMode.Qualitative &&
                    ingredient.IntoleranceReviewState == IngredientPropertyReviewState.Reviewed)
                    contribution = SubstancePortionContribution.Qualitative(position.PositionPath, profile.Contained!.Value);
                else if (profile.Content is { ReviewState: IngredientSubstanceContentReviewState.Reviewed } content &&
                    references.IsQuantitativeSubstance(requirement.SubstanceId))
                {
                    var amountUnit = references.Unit(content.AmountUnitId);
                    var referenceUnit = references.Unit(content.ReferenceUnitId);
                    var baseUnit = references.Unit(ingredient.BaseUnitId);
                    if (amountUnit.Dimension == MeasurementDimension.Mass && referenceUnit.Dimension == baseUnit.Dimension)
                    {
                        decimal referenceInBase = content.ReferenceQuantity * referenceUnit.BaseUnitFactor / baseUnit.BaseUnitFactor;
                        contribution = SubstancePortionContribution.Quantitative(position.PositionPath,
                            content.Amount * amountUnit.BaseUnitFactor * position.QuantityInBaseUnit / referenceInBase / solution.Portions);
                    }
                }
                substances[requirement.SubstanceId].Add(contribution);
            }
        }
        foreach (var requirement in requirements.Intolerances)
        {
            var values = substances[requirement.SubstanceId];
            if (references.IsQuantitativeSubstance(requirement.SubstanceId))
                problems.AddRange(SubstancePortionEvaluator.Evaluate(requirement, values).Problems
                    .Select(value => value with { RequirementGroupKey = requirements.Key }));
            else
                foreach (var value in values)
                    if (value.Contained != false) Problem(value.Contained == true ? MealPlanningProblemCategory.Suitability : MealPlanningProblemCategory.DataQuality,
                        value.Contained == true ? MealPlanningProblemCode.SubstancePresentWithoutThreshold : MealPlanningProblemCode.UnknownSubstanceData,
                        requirement.SubstanceId, value.PositionPath);
        }
        if (calculated.Ingredients.Count == 0)
            problems.Add(new(MealPlanningProblemCategory.DataQuality, MealPlanningProblemCode.NoMatchingDirectSolution, requirements.Key));
        return new(calculated.Ingredients, problems);

        void Problem(MealPlanningProblemCategory category, MealPlanningProblemCode code, Guid reference, string path) =>
            problems.Add(new(category, code, requirements.Key, reference, path));
    }

    private bool IsDescendant(IngredientPropertyDefinition value, Guid parent)
    {
        var visited = new HashSet<Guid>();
        while (value.ParentId is Guid ancestor && visited.Add(ancestor))
        {
            if (ancestor == parent) return true;
            var next = references.Allergens.SingleOrDefault(item => item.Id == ancestor);
            if (next is null) return false;
            value = next;
        }
        return false;
    }
}
