using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public interface IRecipeSnapshotSource
{
    RecipeSnapshot GetRevision(Guid revisionId);
}

public sealed record RecipeCalculationRequest(
    Guid RecipeRevisionId,
    decimal AgeAdjustedServings,
    decimal DirectParticipantDemand,
    IReadOnlyDictionary<Guid, Guid>? SelectedReplacementRuleIds = null);

public sealed record CalculatedIngredient(
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    MeasurementUnitSnapshot Unit,
    IReadOnlyList<ConflictReference> Conflicts,
    IReadOnlyList<Guid> RecipeRevisionPath,
    Guid PositionId,
    Guid? AppliedReplacementRuleId,
    CalculatedNutritionValues? Nutrition = null,
    MissingNutritionReason? NutritionIssue = null);

public sealed record CalculatedNutritionValues(
    decimal EnergyKilojoules,
    decimal FatGrams,
    decimal SaturatedFatGrams,
    decimal CarbohydrateGrams,
    decimal SugarsGrams,
    decimal ProteinGrams,
    decimal SaltGrams,
    decimal? FiberGrams)
{
    public decimal EnergyKilocalories => EnergyKilojoules / 4.184m;
}

public enum MissingNutritionReason
{
    ProfileMissing,
    ProfileUnreviewed,
    InvalidReference,
}

public sealed record MissingNutritionContribution(
    Guid IngredientRevisionId,
    string IngredientName,
    IReadOnlyList<Guid> RecipeRevisionPath,
    Guid PositionId,
    MissingNutritionReason Reason);

public sealed record RecipeNutritionCalculation(
    bool IsComplete,
    CalculatedNutritionValues? Total,
    CalculatedNutritionValues? PerStandardPortion,
    IReadOnlyList<MissingNutritionContribution> MissingContributions);

public sealed record RecipeCalculationResult(
    IReadOnlyList<CalculatedIngredient> Ingredients,
    IReadOnlyList<ConflictReference> Conflicts,
    RecipeNutritionCalculation Nutrition);

public sealed class RecipeCalculator(IRecipeSnapshotSource snapshots)
{
    public RecipeCalculationResult Calculate(RecipeCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RecipeRevisionId == Guid.Empty)
            throw new ArgumentException("Recipe revision ID is required.", nameof(request));
        if (request.AgeAdjustedServings <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Age-adjusted servings must be positive.");
        if (request.DirectParticipantDemand <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Direct participant demand must be positive.");

        RecipeSnapshot root = snapshots.GetRevision(request.RecipeRevisionId);
        if (root.RecipeType != RecipeType.PortionBased || root.Reference.StandardServings is not > 0)
            throw new InvalidOperationException("Only a published portion-based recipe can be calculated at top level.");

        var result = new List<CalculatedIngredient>();
        var activePath = new HashSet<Guid>();
        var path = new List<Guid>();
        var conflicts = new HashSet<ConflictReference>();
        decimal adjustedRatio = request.AgeAdjustedServings / root.Reference.StandardServings.Value;
        decimal directRatio = request.DirectParticipantDemand / root.Reference.StandardServings.Value;
        Evaluate(
            request.RecipeRevisionId, root, adjustedRatio, directRatio,
            request.AgeAdjustedServings, request.DirectParticipantDemand,
            topLevel: true, request.SelectedReplacementRuleIds ?? new Dictionary<Guid, Guid>(),
            activePath, path, result, conflicts);
        return new RecipeCalculationResult(
            result,
            conflicts.OrderBy(value => value.Type).ThenBy(value => value.Id).ToArray(),
            CalculateNutrition(result, request.AgeAdjustedServings));
    }

    private void Evaluate(
        Guid revisionId,
        RecipeSnapshot recipe,
        decimal ratio,
        decimal directRatio,
        decimal targetDemand,
        decimal directTargetDemand,
        bool topLevel,
        IReadOnlyDictionary<Guid, Guid> selections,
        HashSet<Guid> activePath,
        List<Guid> path,
        List<CalculatedIngredient> result,
        HashSet<ConflictReference> conflicts)
    {
        if (!activePath.Add(revisionId))
            throw new InvalidOperationException("A recipe cycle was encountered during calculation.");
        path.Add(revisionId);
        try
        {
            foreach (IngredientPositionSnapshot position in recipe.IngredientPositions)
            {
                (decimal positionRatio, decimal positionDemand) = ResolveIngredientDemand(
                    recipe, position, ratio, directRatio, targetDemand, directTargetDemand, topLevel);
                IngredientReplacementSnapshot? replacement = Select(
                    position.Id, position.Replacements, value => value.Id, selections);
                IngredientSnapshotSource ingredient = replacement?.Ingredient ?? position.Ingredient;
                ConflictReference[] ingredientConflicts = ingredient.Conflicts
                    .Concat(replacement?.ApplicableConflicts ?? [])
                    .Distinct().OrderBy(value => value.Type).ThenBy(value => value.Id).ToArray();
                conflicts.UnionWith(ingredientConflicts);
                decimal referenceQuantity = replacement?.Quantity ?? position.Quantity;
                IngredientUnitSnapshot unit = replacement?.Unit ?? position.Unit;
                decimal quantity = Scale(
                    referenceQuantity, position.ScalingMode, position.StepwiseScaling,
                    positionRatio, positionDemand);
                CalculatedNutritionValues? nutrition = CalculateNutrition(ingredient.Nutrition, quantity, unit);
                result.Add(new CalculatedIngredient(
                    ingredient.IngredientRevisionId, ingredient.Name, quantity, unit.Unit,
                    ingredientConflicts,
                    path.ToArray(), position.Id, replacement?.Id, nutrition,
                    NutritionIssue(ingredient.Nutrition)));
            }

            foreach (SubrecipePositionSnapshot position in recipe.SubrecipePositions)
            {
                RecipeReplacementSnapshot? replacement = Select(
                    position.Id, position.Replacements, value => value.Id, selections);
                Guid childRevisionId = replacement?.RecipeRevisionId ?? position.RecipeRevisionId;
                RecipeSnapshot child = snapshots.GetRevision(childRevisionId);
                decimal? servings = replacement?.Servings ?? position.RequiredServings;
                decimal? quantity = replacement?.Quantity ?? position.RequiredQuantity;
                MeasurementUnitSnapshot? unit = replacement?.Unit ?? position.RequiredUnit;
                conflicts.UnionWith(replacement?.ApplicableConflicts ?? []);
                conflicts.UnionWith(replacement?.RevisionConflicts ?? position.RevisionConflicts);
                decimal childRatio = ResolveSubrecipeRatio(child, servings, quantity, unit) * ratio;
                decimal childTarget = ReferenceDemand(child) * childRatio;
                Evaluate(
                    childRevisionId, child, childRatio, childRatio, childTarget, childTarget,
                    topLevel: false, selections, activePath, path, result, conflicts);
            }
        }
        finally
        {
            path.RemoveAt(path.Count - 1);
            activePath.Remove(revisionId);
        }
    }

    private static (decimal Ratio, decimal Demand) ResolveIngredientDemand(
        RecipeSnapshot recipe,
        IngredientPositionSnapshot position,
        decimal ratio,
        decimal directRatio,
        decimal targetDemand,
        decimal directTargetDemand,
        bool topLevel)
    {
        if (!topLevel) return (ratio, targetDemand);
        bool applyAgeFactor = position.AgeGroupScaling switch
        {
            AgeGroupScalingMode.Apply => true,
            AgeGroupScalingMode.Ignore => false,
            AgeGroupScalingMode.Inherit => recipe.DefaultAgeGroupScalingApplies == true,
            _ => throw new InvalidOperationException("Unsupported age-group scaling mode."),
        };
        return applyAgeFactor ? (ratio, targetDemand) : (directRatio, directTargetDemand);
    }

    private static decimal Scale(
        decimal referenceQuantity,
        ScalingMode mode,
        StepwiseScaling? stepwise,
        decimal ratio,
        decimal targetDemand) => mode switch
    {
        ScalingMode.Linear => referenceQuantity * ratio,
        ScalingMode.Fixed => referenceQuantity,
        ScalingMode.Stepwise when stepwise is { StepSize: > 0, QuantityPerStep: > 0 } =>
            decimal.Ceiling(targetDemand / stepwise.StepSize.Value) * stepwise.QuantityPerStep.Value,
        ScalingMode.Stepwise => throw new InvalidOperationException("Stepwise scaling parameters are invalid."),
        _ => throw new InvalidOperationException("Unsupported scaling mode."),
    };

    private static decimal ResolveSubrecipeRatio(
        RecipeSnapshot child,
        decimal? servings,
        decimal? quantity,
        MeasurementUnitSnapshot? requestedUnit)
    {
        if (child.RecipeType == RecipeType.PortionBased)
        {
            if (servings is not > 0 || child.Reference.StandardServings is not > 0)
                throw new InvalidOperationException("Subrecipe serving demand is invalid.");
            return servings.Value / child.Reference.StandardServings.Value;
        }

        if (quantity is not > 0 || requestedUnit is null || child.Reference.ReferenceQuantity is not > 0 ||
            child.Reference.ReferenceUnit is null)
            throw new InvalidOperationException("Subrecipe quantity demand is invalid.");
        decimal convertedQuantity = Convert(quantity.Value, requestedUnit, child.Reference.ReferenceUnit);
        return convertedQuantity / child.Reference.ReferenceQuantity.Value;
    }

    private static decimal Convert(
        decimal quantity,
        MeasurementUnitSnapshot source,
        MeasurementUnitSnapshot target)
    {
        if (source.Dimension != target.Dimension)
            throw new InvalidOperationException("Subrecipe units are not compatible.");
        return quantity * source.BaseUnitFactor / target.BaseUnitFactor;
    }

    private static decimal ReferenceDemand(RecipeSnapshot recipe) => recipe.RecipeType switch
    {
        RecipeType.PortionBased when recipe.Reference.StandardServings is > 0 =>
            recipe.Reference.StandardServings.Value,
        RecipeType.QuantityBased when recipe.Reference.ReferenceQuantity is > 0 =>
            recipe.Reference.ReferenceQuantity.Value,
        _ => throw new InvalidOperationException("Recipe reference demand is invalid."),
    };

    private static RecipeNutritionCalculation CalculateNutrition(
        IReadOnlyList<CalculatedIngredient> ingredients,
        decimal standardPortionDemand)
    {
        MissingNutritionContribution[] missing = ingredients
            .Where(value => value.Nutrition is null)
            .Select(value => new MissingNutritionContribution(
                value.IngredientId, value.IngredientName, value.RecipeRevisionPath,
                value.PositionId, value.NutritionIssue ?? MissingNutritionReason.InvalidReference))
            .ToArray();
        if (ingredients.Count == 0 || missing.Length > 0)
            return new RecipeNutritionCalculation(false, null, null, missing);

        CalculatedNutritionValues total = Sum(ingredients.Select(value => value.Nutrition!).ToArray());
        return new RecipeNutritionCalculation(
            true, total, Divide(total, standardPortionDemand), []);
    }

    private static MissingNutritionReason? NutritionIssue(IngredientNutritionSnapshot? profile) => profile switch
    {
        null => MissingNutritionReason.ProfileMissing,
        { ReviewState: not IngredientNutritionReviewState.Reviewed } =>
            MissingNutritionReason.ProfileUnreviewed,
        { ReferenceQuantityInBaseUnit: <= 0 } => MissingNutritionReason.InvalidReference,
        { EnergyKilojoules: null } or { FatGrams: null } or { SaturatedFatGrams: null } or
        { CarbohydrateGrams: null } or { SugarsGrams: null } or { ProteinGrams: null } or
        { SaltGrams: null } => MissingNutritionReason.InvalidReference,
        _ => null,
    };

    private static CalculatedNutritionValues? CalculateNutrition(
        IngredientNutritionSnapshot? profile,
        decimal quantity,
        IngredientUnitSnapshot unit)
    {
        if (profile is null || profile.ReviewState != IngredientNutritionReviewState.Reviewed ||
            profile.ReferenceQuantityInBaseUnit <= 0 || profile.EnergyKilojoules is null ||
            profile.FatGrams is null || profile.SaturatedFatGrams is null ||
            profile.CarbohydrateGrams is null || profile.SugarsGrams is null ||
            profile.ProteinGrams is null || profile.SaltGrams is null)
            return null;
        decimal factor = quantity * unit.ReferenceQuantityPerUnit / profile.ReferenceQuantityInBaseUnit;
        return new CalculatedNutritionValues(
            profile.EnergyKilojoules.Value * factor,
            profile.FatGrams.Value * factor,
            profile.SaturatedFatGrams.Value * factor,
            profile.CarbohydrateGrams.Value * factor,
            profile.SugarsGrams.Value * factor,
            profile.ProteinGrams.Value * factor,
            profile.SaltGrams.Value * factor,
            profile.FiberGrams * factor);
    }

    private static CalculatedNutritionValues Sum(IReadOnlyList<CalculatedNutritionValues> values) => new(
        values.Sum(value => value.EnergyKilojoules),
        values.Sum(value => value.FatGrams),
        values.Sum(value => value.SaturatedFatGrams),
        values.Sum(value => value.CarbohydrateGrams),
        values.Sum(value => value.SugarsGrams),
        values.Sum(value => value.ProteinGrams),
        values.Sum(value => value.SaltGrams),
        values.All(value => value.FiberGrams.HasValue)
            ? values.Sum(value => value.FiberGrams!.Value)
            : null);

    private static CalculatedNutritionValues Divide(CalculatedNutritionValues value, decimal divisor) => new(
        value.EnergyKilojoules / divisor,
        value.FatGrams / divisor,
        value.SaturatedFatGrams / divisor,
        value.CarbohydrateGrams / divisor,
        value.SugarsGrams / divisor,
        value.ProteinGrams / divisor,
        value.SaltGrams / divisor,
        value.FiberGrams / divisor);

    private static T? Select<T>(
        Guid positionId,
        IReadOnlyList<T> replacements,
        Func<T, Guid> id,
        IReadOnlyDictionary<Guid, Guid> selections)
        where T : class
    {
        if (!selections.TryGetValue(positionId, out Guid selectedId)) return null;
        return replacements.SingleOrDefault(value => id(value) == selectedId) ??
               throw new InvalidOperationException("Selected replacement rule does not belong to the position.");
    }
}
