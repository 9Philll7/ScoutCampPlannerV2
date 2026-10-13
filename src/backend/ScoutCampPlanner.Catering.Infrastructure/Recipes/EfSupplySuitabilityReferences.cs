using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

/// <summary>Reads pinned published revisions, including revisions of retired identities.
/// No lookup of the current published head and no cross-module database access.</summary>
public sealed class EfSupplySuitabilityReferences(CateringDbContext database) : ISupplySuitabilityReferences
{
    private readonly EfRecipeReferences recipes = new(database);

    public RecipeSnapshot GetRevision(Guid revisionId) => recipes.GetRevision(revisionId);
    public MeasurementUnit Unit(Guid id) => database.MeasurementUnits.AsNoTracking().Single(value => value.Id == id);
    public IReadOnlyList<IngredientPropertyDefinition> Allergens => database.Set<IngredientAllergenDefinitionRecord>()
        .AsNoTracking().Select(value => new IngredientPropertyDefinition(value.Id, value.Code, value.ParentAllergenId)).ToArray();
    public IReadOnlyList<IngredientPropertyDefinition> MainOrigins => database.Set<IngredientOriginPropertyRecord>()
        .AsNoTracking().AsEnumerable().Where(value => DietaryCatalogStore.MainOriginCodes.Contains(value.Code))
        .Select(value => new IngredientPropertyDefinition(value.Id, value.Code, null)).ToArray();
    public bool IsQuantitativeSubstance(Guid id) => database.Set<IngredientIntoleranceDefinitionRecord>()
        .AsNoTracking().Any(value => value.Id == id && value.IsQuantityDependent);
    public DietaryOriginDecision? DietDecision(Guid dietTypeId, Guid originId) => database.DietaryRequirements
        .AsNoTracking().SingleOrDefault(value => value.Id == dietTypeId)?.DecisionFor(originId);

    public IngredientRevisionDraftContent Ingredient(Guid revisionId)
    {
        var revision = database.Set<IngredientRevisionRecord>().AsNoTracking().Single(value =>
            value.Id == revisionId && value.State == (int)IngredientRevisionState.Published);
        var allergens = database.Set<IngredientRevisionAllergenRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId).AsEnumerable()
            .Select(value => Property(value.AllergenId, value.State, value.Source)).ToArray();
        var intolerances = database.Set<IngredientRevisionIntoleranceRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId).AsEnumerable()
            .Select(value => Property(value.IntoleranceId, value.State, value.Source)).ToArray();
        var origins = database.Set<IngredientRevisionOriginRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId).AsEnumerable()
            .Select(value => Property(value.OriginPropertyId, value.State, value.Source)).ToArray();
        var conversions = database.Set<IngredientRevisionUnitConversionRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId).AsEnumerable()
            .Select(value => new IngredientRevisionUnitConversion(value.SourceUnitId, value.FactorToBaseUnit, (IngredientConversionPrecision)value.Precision)).ToArray();
        var contents = database.Set<IngredientRevisionSubstanceContentRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId).AsEnumerable()
            .Select(value => new IngredientSubstanceContent(value.SubstanceId, value.Amount, value.AmountUnitId,
                value.ReferenceQuantity, value.ReferenceUnitId, (IngredientSubstanceContentSourceType)value.SourceType,
                value.SourceReference, (IngredientSubstanceContentReviewState)value.ReviewState)).ToArray();
        var nutrition = database.Set<IngredientRevisionNutritionProfileRecord>().AsNoTracking()
            .SingleOrDefault(value => value.IngredientRevisionId == revisionId);
        var variants = database.Set<IngredientVariantRevisionRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId).ToArray().Select(Variant).ToArray();
        return IngredientRevisionDraftContent.Create(revision.Name, revision.CategoryId, revision.BaseUnitId,
            (IngredientPropertyReviewState)revision.AllergenReviewState,
            (IngredientPropertyReviewState)revision.IntoleranceReviewState,
            (IngredientPropertyReviewState)revision.OriginReviewState, allergens, intolerances, origins,
            conversions, variants, nutrition is null ? null : IngredientNutritionProfilePersistence.ToDomain(nutrition),
            contents, revision.SourceSummary);
    }

    public (IngredientSnapshotSource Ingredient, IngredientUnitSnapshot Unit) ResolveVariant(
        Guid ingredientRevisionId, string variantKey, Guid unitId)
    {
        var ingredient = Ingredient(ingredientRevisionId);
        var variant = ingredient.Variants!.Single(value => value.VariantKey == variantKey);
        var conflicts = ingredient.Allergens.Select(value => value.PropertyId)
            .Union(variant.AllergenOverrides.Select(value => value.PropertyId))
            .Where(id => IngredientPropertyResolver.EffectiveState(ingredient.Allergens, variant.AllergenOverrides, id)
                is IngredientPropertyState.Contains or IngredientPropertyState.MayContain)
            .Select(id => new ConflictReference(ConflictType.Allergen, id))
            .Concat(ingredient.Intolerances.Select(value => value.PropertyId)
                .Union(variant.IntoleranceOverrides.Select(value => value.PropertyId))
                .Where(id => IngredientPropertyResolver.EffectiveState(ingredient.Intolerances, variant.IntoleranceOverrides, id)
                    is IngredientPropertyState.Contains or IngredientPropertyState.MayContain)
                .Select(id => new ConflictReference(ConflictType.Intolerance, id))).ToArray();
        var unit = recipes.GetIngredientUnit(ingredientRevisionId, unitId);
        var conversion = variant.UnitConversionOverrides.SingleOrDefault(value => value.SourceUnitId == unitId);
        if (conversion is not null) unit = unit with { ReferenceQuantityPerUnit = conversion.FactorToBaseUnit };
        var nutrition = variant.NutritionProfile ?? ingredient.NutritionProfile;
        IngredientNutritionSnapshot? snapshot = nutrition is null ? null : new(
            nutrition.ReferenceQuantity, recipes.GetUnit(nutrition.ReferenceUnitId),
            nutrition.ReferenceQuantity * Unit(nutrition.ReferenceUnitId).BaseUnitFactor / Unit(ingredient.BaseUnitId).BaseUnitFactor,
            nutrition.EnergyKilojoules, nutrition.FatGrams, nutrition.SaturatedFatGrams, nutrition.CarbohydrateGrams,
            nutrition.SugarsGrams, nutrition.ProteinGrams, nutrition.SaltGrams, nutrition.FiberGrams,
            nutrition.SourceType, nutrition.SourceReference, nutrition.ReviewState, nutrition.ReferenceDate);
        return (new(ingredientRevisionId, ingredient.Name + " – " + variant.Name, conflicts, snapshot), unit);
    }

    private IngredientVariantDraftContent Variant(IngredientVariantRevisionRecord variant)
    {
        Guid id = variant.Id;
        var allergens = database.Set<IngredientVariantAllergenOverrideRecord>().AsNoTracking()
            .Where(value => value.VariantRevisionId == id).AsEnumerable()
            .Select(value => Property(value.AllergenId, value.State, value.Source)).ToArray();
        var intolerances = database.Set<IngredientVariantIntoleranceOverrideRecord>().AsNoTracking()
            .Where(value => value.VariantRevisionId == id).AsEnumerable()
            .Select(value => Property(value.IntoleranceId, value.State, value.Source)).ToArray();
        var origins = database.Set<IngredientVariantOriginOverrideRecord>().AsNoTracking()
            .Where(value => value.VariantRevisionId == id).AsEnumerable()
            .Select(value => Property(value.OriginPropertyId, value.State, value.Source)).ToArray();
        var conversions = database.Set<IngredientVariantUnitConversionOverrideRecord>().AsNoTracking()
            .Where(value => value.VariantRevisionId == id).AsEnumerable()
            .Select(value => new IngredientRevisionUnitConversion(value.SourceUnitId, value.FactorToBaseUnit, (IngredientConversionPrecision)value.Precision)).ToArray();
        var contents = database.Set<IngredientVariantSubstanceContentOverrideRecord>().AsNoTracking()
            .Where(value => value.VariantRevisionId == id).AsEnumerable()
            .Select(value => new IngredientSubstanceContent(value.SubstanceId, value.Amount, value.AmountUnitId,
                value.ReferenceQuantity, value.ReferenceUnitId, (IngredientSubstanceContentSourceType)value.SourceType,
                value.SourceReference, (IngredientSubstanceContentReviewState)value.ReviewState)).ToArray();
        var nutrition = database.Set<IngredientVariantNutritionProfileRecord>().AsNoTracking()
            .SingleOrDefault(value => value.VariantRevisionId == id);
        return new(id, variant.VariantKey, variant.Name, variant.Status == 0, variant.SortOrder,
            allergens, intolerances, origins, conversions,
            nutrition is null ? null : IngredientNutritionProfilePersistence.ToDomain(nutrition), contents);
    }

    private static IngredientPropertyValue Property(Guid id, int state, int source) =>
        new(id, (IngredientPropertyState)state, (IngredientPropertySource)source);
}
