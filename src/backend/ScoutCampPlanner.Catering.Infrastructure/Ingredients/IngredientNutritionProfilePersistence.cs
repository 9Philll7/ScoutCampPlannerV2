using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

internal static class IngredientNutritionProfilePersistence
{
    public static IngredientRevisionNutritionProfileRecord CreateRevisionRecord(
        Guid revisionId,
        IngredientNutritionProfile profile) => CopyTo(
            profile,
            new IngredientRevisionNutritionProfileRecord { IngredientRevisionId = revisionId });

    public static IngredientVariantNutritionProfileRecord CreateVariantRecord(
        Guid variantId,
        IngredientNutritionProfile profile) => CopyTo(
            profile,
            new IngredientVariantNutritionProfileRecord { VariantRevisionId = variantId });

    public static IngredientRevisionNutritionProfileRecord CopyTo(
        IngredientNutritionProfile profile,
        IngredientRevisionNutritionProfileRecord record)
    {
        record.ReferenceQuantity = profile.ReferenceQuantity;
        record.ReferenceUnitId = profile.ReferenceUnitId;
        record.EnergyKilojoules = profile.EnergyKilojoules;
        record.FatGrams = profile.FatGrams;
        record.SaturatedFatGrams = profile.SaturatedFatGrams;
        record.CarbohydrateGrams = profile.CarbohydrateGrams;
        record.SugarsGrams = profile.SugarsGrams;
        record.ProteinGrams = profile.ProteinGrams;
        record.SaltGrams = profile.SaltGrams;
        record.FiberGrams = profile.FiberGrams;
        record.SourceType = (int)profile.SourceType;
        record.SourceReference = profile.SourceReference;
        record.ReviewState = (int)profile.ReviewState;
        record.ReferenceDate = profile.ReferenceDate;
        return record;
    }

    public static IngredientVariantNutritionProfileRecord CopyTo(
        IngredientNutritionProfile profile,
        IngredientVariantNutritionProfileRecord record)
    {
        record.ReferenceQuantity = profile.ReferenceQuantity;
        record.ReferenceUnitId = profile.ReferenceUnitId;
        record.EnergyKilojoules = profile.EnergyKilojoules;
        record.FatGrams = profile.FatGrams;
        record.SaturatedFatGrams = profile.SaturatedFatGrams;
        record.CarbohydrateGrams = profile.CarbohydrateGrams;
        record.SugarsGrams = profile.SugarsGrams;
        record.ProteinGrams = profile.ProteinGrams;
        record.SaltGrams = profile.SaltGrams;
        record.FiberGrams = profile.FiberGrams;
        record.SourceType = (int)profile.SourceType;
        record.SourceReference = profile.SourceReference;
        record.ReviewState = (int)profile.ReviewState;
        record.ReferenceDate = profile.ReferenceDate;
        return record;
    }

    public static IngredientNutritionProfileItem ToItem(IngredientRevisionNutritionProfileRecord record) =>
        CreateItem(record.ReferenceQuantity, record.ReferenceUnitId, record.EnergyKilojoules,
            record.FatGrams, record.SaturatedFatGrams, record.CarbohydrateGrams, record.SugarsGrams,
            record.ProteinGrams, record.SaltGrams, record.FiberGrams, record.SourceType,
            record.SourceReference, record.ReviewState, record.ReferenceDate);

    public static IngredientNutritionProfileItem ToItem(IngredientVariantNutritionProfileRecord record) =>
        CreateItem(record.ReferenceQuantity, record.ReferenceUnitId, record.EnergyKilojoules,
            record.FatGrams, record.SaturatedFatGrams, record.CarbohydrateGrams, record.SugarsGrams,
            record.ProteinGrams, record.SaltGrams, record.FiberGrams, record.SourceType,
            record.SourceReference, record.ReviewState, record.ReferenceDate);

    public static IngredientNutritionProfile ToDomain(IngredientRevisionNutritionProfileRecord record) =>
        CreateDomain(record.ReferenceQuantity, record.ReferenceUnitId, record.EnergyKilojoules,
            record.FatGrams, record.SaturatedFatGrams, record.CarbohydrateGrams, record.SugarsGrams,
            record.ProteinGrams, record.SaltGrams, record.FiberGrams, record.SourceType,
            record.SourceReference, record.ReviewState, record.ReferenceDate);

    public static IngredientNutritionProfile ToDomain(IngredientVariantNutritionProfileRecord record) =>
        CreateDomain(record.ReferenceQuantity, record.ReferenceUnitId, record.EnergyKilojoules,
            record.FatGrams, record.SaturatedFatGrams, record.CarbohydrateGrams, record.SugarsGrams,
            record.ProteinGrams, record.SaltGrams, record.FiberGrams, record.SourceType,
            record.SourceReference, record.ReviewState, record.ReferenceDate);

    private static IngredientNutritionProfileItem CreateItem(
        decimal referenceQuantity, Guid referenceUnitId, decimal? energyKilojoules,
        decimal? fatGrams, decimal? saturatedFatGrams, decimal? carbohydrateGrams,
        decimal? sugarsGrams, decimal? proteinGrams, decimal? saltGrams, decimal? fiberGrams,
        int sourceType, string sourceReference, int reviewState, DateOnly? referenceDate) => new(
            referenceQuantity, referenceUnitId, energyKilojoules, fatGrams, saturatedFatGrams,
            carbohydrateGrams, sugarsGrams, proteinGrams, saltGrams, fiberGrams,
            (IngredientNutritionSourceType)sourceType, sourceReference,
            (IngredientNutritionReviewState)reviewState, referenceDate);

    private static IngredientNutritionProfile CreateDomain(
        decimal referenceQuantity, Guid referenceUnitId, decimal? energyKilojoules,
        decimal? fatGrams, decimal? saturatedFatGrams, decimal? carbohydrateGrams,
        decimal? sugarsGrams, decimal? proteinGrams, decimal? saltGrams, decimal? fiberGrams,
        int sourceType, string sourceReference, int reviewState, DateOnly? referenceDate) => new(
            referenceQuantity, referenceUnitId, energyKilojoules, fatGrams, saturatedFatGrams,
            carbohydrateGrams, sugarsGrams, proteinGrams, saltGrams, fiberGrams,
            (IngredientNutritionSourceType)sourceType, sourceReference,
            (IngredientNutritionReviewState)reviewState, referenceDate);

}
