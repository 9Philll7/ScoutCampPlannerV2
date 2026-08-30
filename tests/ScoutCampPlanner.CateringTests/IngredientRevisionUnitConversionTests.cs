using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientRevisionUnitConversionTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid BaseUnitId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Category_uses_stable_normalized_code_and_optional_parent()
    {
        Guid parentId = Guid.NewGuid();
        var category = new IngredientCategory(Guid.NewGuid(), " cereal_products ", " Getreide ", parentId);

        Assert.Equal("CEREAL_PRODUCTS", category.Code);
        Assert.Equal("Getreide", category.Name);
        Assert.Equal(parentId, category.ParentCategoryId);
    }

    [Fact]
    public void Revision_conversion_converts_quantity_to_base_unit()
    {
        var conversion = new IngredientRevisionUnitConversion(
            Guid.NewGuid(), 10m, IngredientConversionPrecision.Average);

        Assert.Equal(25m, conversion.ConvertToBaseUnit(2.5m));
    }

    [Fact]
    public void Base_unit_does_not_accept_redundant_specific_conversion()
    {
        IngredientRevision revision = Draft();
        var conversion = new IngredientRevisionUnitConversion(
            BaseUnitId, 1m, IngredientConversionPrecision.Exact);

        Assert.Throws<ArgumentException>(() =>
            revision.SetUnitConversion(conversion, UserId, Now));
    }

    [Fact]
    public void Variant_inherits_revision_conversion_and_can_override_it()
    {
        IngredientRevision revision = Draft();
        Guid spoon = Guid.NewGuid();
        var baseConversion = new IngredientRevisionUnitConversion(
            spoon, 10m, IngredientConversionPrecision.Average);
        revision.SetUnitConversion(baseConversion, UserId, Now);
        IngredientVariantRevision variant = revision.AddVariant(
            Guid.NewGuid(), "whole_grain", "Vollkorn", UserId, Now);

        Assert.Empty(variant.UnitConversionOverrides);
        Assert.Equal(baseConversion, Assert.Single(revision.UnitConversions));

        var variantConversion = new IngredientRevisionUnitConversion(
            spoon, 12m, IngredientConversionPrecision.Estimated);
        revision.SetVariantUnitConversionOverride(
            variant.VariantKey, variantConversion, UserId, Now);

        Assert.Equal(variantConversion, Assert.Single(variant.UnitConversionOverrides));
        Assert.Equal(
            variantConversion,
            IngredientUnitConversionResolver.EffectiveConversion(
                revision.UnitConversions,
                variant.UnitConversionOverrides,
                spoon));
        Assert.Equal(
            variantConversion,
            IngredientUnitConversionResolver.EffectiveConversion(
                revision.UnitConversions,
                variant.UnitConversionOverrides,
                spoon));
    }

    [Fact]
    public void New_draft_copies_conversions_and_variant_overrides()
    {
        var ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision revision = ingredient.CreateDraft(
            Guid.NewGuid(), "Mehl", Guid.NewGuid(), BaseUnitId, UserId, Now);
        Guid spoon = Guid.NewGuid();
        revision.SetUnitConversion(
            new IngredientRevisionUnitConversion(spoon, 10m, IngredientConversionPrecision.Average),
            UserId,
            Now);
        IngredientVariantRevision variant = revision.AddVariant(
            Guid.NewGuid(), "whole_grain", "Vollkorn", UserId, Now);
        revision.SetVariantUnitConversionOverride(
            variant.VariantKey,
            new IngredientRevisionUnitConversion(spoon, 12m, IngredientConversionPrecision.Estimated),
            UserId,
            Now);
        Review(revision);
        ingredient.PublishDraft(
            revision.Id, UserId, Now, new IngredientRevisionPublicationValidator([]));

        IngredientRevision copy = ingredient.CreateDraftFromPublished(Guid.NewGuid(), UserId, Now.AddDays(1));

        Assert.Single(copy.UnitConversions);
        Assert.Single(Assert.Single(copy.Variants).UnitConversionOverrides);
    }

    private static IngredientRevision Draft()
    {
        var ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        return ingredient.CreateDraft(
            Guid.NewGuid(), "Mehl", Guid.NewGuid(), BaseUnitId, UserId, Now);
    }

    private static void Review(IngredientRevision revision) => revision.SetReviewStates(
        IngredientPropertyReviewState.Reviewed,
        IngredientPropertyReviewState.Reviewed,
        IngredientPropertyReviewState.Reviewed,
        UserId,
        Now);
}
