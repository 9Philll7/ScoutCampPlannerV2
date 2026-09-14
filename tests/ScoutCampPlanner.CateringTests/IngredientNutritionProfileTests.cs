using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientNutritionProfileTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid UnitId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Unreviewed_profile_may_keep_unknown_values()
    {
        IngredientNutritionProfile profile = Profile(
            IngredientNutritionReviewState.Unreviewed,
            energyKilojoules: null,
            fatGrams: null,
            saturatedFatGrams: null,
            carbohydrateGrams: null,
            sugarsGrams: null,
            proteinGrams: null,
            saltGrams: null);

        Assert.False(profile.IsComplete);
        Assert.Null(profile.EnergyKilojoules);
        Assert.Null(profile.EnergyKilocalories);
    }

    [Fact]
    public void Reviewed_profile_requires_every_core_value()
    {
        Assert.Throws<ArgumentException>(() => Profile(
            IngredientNutritionReviewState.Reviewed,
            saltGrams: null));
    }

    [Fact]
    public void Profile_rejects_negative_and_inconsistent_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Profile(
            IngredientNutritionReviewState.Unreviewed,
            proteinGrams: -1m));
        Assert.Throws<ArgumentException>(() => Profile(
            IngredientNutritionReviewState.Unreviewed,
            fatGrams: 10m,
            saturatedFatGrams: 11m));
        Assert.Throws<ArgumentException>(() => Profile(
            IngredientNutritionReviewState.Unreviewed,
            carbohydrateGrams: 10m,
            sugarsGrams: 11m));
    }

    [Fact]
    public void Kilocalories_are_derived_from_kilojoules()
    {
        IngredientNutritionProfile profile = Profile(
            IngredientNutritionReviewState.Reviewed,
            energyKilojoules: 418.4m);

        Assert.Equal(100m, profile.EnergyKilocalories);
    }

    [Fact]
    public void Variant_inherits_or_replaces_the_complete_profile()
    {
        IngredientNutritionProfile basis = Profile(IngredientNutritionReviewState.Reviewed);
        IngredientNutritionProfile variant = Profile(
            IngredientNutritionReviewState.Reviewed,
            energyKilojoules: 900m);

        Assert.Same(basis, IngredientNutritionResolver.EffectiveProfile(basis, null));
        Assert.Same(variant, IngredientNutritionResolver.EffectiveProfile(basis, variant));
    }

    [Fact]
    public void New_draft_copies_revision_and_variant_profiles()
    {
        IngredientIdentity ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision revision = ingredient.CreateDraft(
            Guid.NewGuid(), "Milch", Guid.NewGuid(), UnitId, UserId, Now);
        IngredientNutritionProfile basis = Profile(IngredientNutritionReviewState.Reviewed);
        IngredientNutritionProfile variantProfile = Profile(
            IngredientNutritionReviewState.Reviewed,
            energyKilojoules: 800m);
        revision.SetNutritionProfile(basis, UserId, Now);
        IngredientVariantRevision variant = revision.AddVariant(
            Guid.NewGuid(), "lactose_free", "Laktosefrei", UserId, Now);
        revision.SetVariantNutritionProfile(variant.VariantKey, variantProfile, UserId, Now);
        revision.SetReviewStates(
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed,
            UserId,
            Now);
        ingredient.PublishDraft(revision.Id, UserId, Now, new IngredientRevisionPublicationValidator([]));

        IngredientRevision copy = ingredient.CreateDraftFromPublished(Guid.NewGuid(), UserId, Now.AddDays(1));

        Assert.Equal(basis, copy.NutritionProfile);
        Assert.Equal(variantProfile, Assert.Single(copy.Variants).NutritionProfile);
    }

    private static IngredientNutritionProfile Profile(
        IngredientNutritionReviewState reviewState,
        decimal? energyKilojoules = 1000m,
        decimal? fatGrams = 10m,
        decimal? saturatedFatGrams = 5m,
        decimal? carbohydrateGrams = 20m,
        decimal? sugarsGrams = 4m,
        decimal? proteinGrams = 8m,
        decimal? saltGrams = 1m) => new(
            100m,
            UnitId,
            energyKilojoules,
            fatGrams,
            saturatedFatGrams,
            carbohydrateGrams,
            sugarsGrams,
            proteinGrams,
            saltGrams,
            fiberGrams: null,
            IngredientNutritionSourceType.Manufacturer,
            "Herstelleretikett",
            reviewState,
            new DateOnly(2026, 9, 14));
}
