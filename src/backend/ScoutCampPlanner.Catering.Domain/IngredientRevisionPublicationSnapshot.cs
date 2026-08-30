namespace ScoutCampPlanner.Catering.Domain;

public sealed record IngredientVariantPublicationSnapshot(
    string VariantKey,
    IReadOnlyCollection<IngredientPropertyValue> AllergenOverrides);

public sealed record IngredientRevisionPublicationSnapshot(
    IngredientRevisionState State,
    IngredientPropertyReviewState AllergenReviewState,
    IngredientPropertyReviewState IntoleranceReviewState,
    IngredientPropertyReviewState OriginReviewState,
    IReadOnlyCollection<IngredientPropertyValue> Allergens,
    IReadOnlyCollection<IngredientVariantPublicationSnapshot> Variants);

