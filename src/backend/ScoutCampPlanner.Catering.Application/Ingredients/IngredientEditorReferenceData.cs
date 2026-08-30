using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Ingredients;

public sealed record IngredientCategoryReference(Guid Id, Guid? ParentCategoryId, string Code, string Name);

public sealed record MeasurementUnitReference(
    Guid Id,
    string Name,
    string Symbol,
    MeasurementDimension Dimension,
    decimal BaseUnitFactor);

public sealed record IngredientAllergenReference(
    Guid Id,
    Guid? ParentAllergenId,
    string Code,
    string Name,
    bool IsEuMajorAllergen);

public sealed record IngredientIntoleranceReference(
    Guid Id,
    string Code,
    string Name,
    bool IsQuantityDependent);

public sealed record IngredientOriginReference(
    Guid Id,
    string Code,
    string Name,
    bool IsAnimalOrigin);

public sealed record IngredientEditorReferenceData(
    IReadOnlyList<IngredientCategoryReference> Categories,
    IReadOnlyList<MeasurementUnitReference> Units,
    IReadOnlyList<IngredientAllergenReference> Allergens,
    IReadOnlyList<IngredientIntoleranceReference> Intolerances,
    IReadOnlyList<IngredientOriginReference> Origins);

public interface IIngredientEditorReferenceDataStore
{
    Task<IngredientEditorReferenceData> GetAsync(CancellationToken cancellationToken = default);
}
