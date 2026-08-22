using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientPropertyTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid UnitId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Variant_inherits_base_property_without_override()
    {
        IngredientRevision revision = CreateDraft();
        Guid milk = Guid.NewGuid();
        revision.SetAllergen(Value(milk, IngredientPropertyState.Contains), UserId, Now);
        IngredientVariantRevision variant = revision.AddVariant(
            Guid.NewGuid(), "lactose_free", "Laktosefrei", UserId, Now);

        IngredientPropertyState? effective = IngredientPropertyResolver.EffectiveState(
            revision.Allergens, variant.AllergenOverrides, milk);

        Assert.Equal(IngredientPropertyState.Contains, effective);
    }

    [Fact]
    public void Variant_override_has_priority()
    {
        IngredientRevision revision = CreateDraft();
        Guid lactose = Guid.NewGuid();
        revision.SetIntolerance(Value(lactose, IngredientPropertyState.Contains), UserId, Now);
        IngredientVariantRevision variant = revision.AddVariant(
            Guid.NewGuid(), "lactose_free", "Laktosefrei", UserId, Now);
        revision.SetVariantIntoleranceOverride(
            variant.VariantKey,
            Value(lactose, IngredientPropertyState.DoesNotContain),
            UserId,
            Now);

        IngredientPropertyState? effective = IngredientPropertyResolver.EffectiveState(
            revision.Intolerances, variant.IntoleranceOverrides, lactose);

        Assert.Equal(IngredientPropertyState.DoesNotContain, effective);
    }

    [Fact]
    public void Missing_property_in_unreviewed_group_is_unknown()
    {
        IngredientCompatibility result = IngredientPropertyResolver.Evaluate(
            null, IngredientPropertyReviewState.Unreviewed);

        Assert.Equal(IngredientCompatibility.Unknown, result);
    }

    [Fact]
    public void Missing_property_in_reviewed_group_is_compatible()
    {
        IngredientCompatibility result = IngredientPropertyResolver.Evaluate(
            null, IngredientPropertyReviewState.Reviewed);

        Assert.Equal(IngredientCompatibility.Compatible, result);
    }

    [Theory]
    [InlineData(IngredientPropertyState.Contains, IngredientCompatibility.Incompatible)]
    [InlineData(IngredientPropertyState.DoesNotContain, IngredientCompatibility.Compatible)]
    [InlineData(IngredientPropertyState.MayContain, IngredientCompatibility.Unknown)]
    [InlineData(IngredientPropertyState.Unknown, IngredientCompatibility.Unknown)]
    public void Property_state_maps_to_safe_compatibility(
        IngredientPropertyState state,
        IngredientCompatibility expected) =>
        Assert.Equal(expected, IngredientPropertyResolver.Evaluate(
            state, IngredientPropertyReviewState.Reviewed));

    [Fact]
    public void New_draft_preserves_variant_key_and_properties()
    {
        var ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision published = ingredient.CreateDraft(
            Guid.NewGuid(), "Butter", CategoryId, UnitId, UserId, Now);
        Guid milk = Guid.NewGuid();
        published.SetAllergen(Value(milk, IngredientPropertyState.Contains), UserId, Now);
        published.AddVariant(Guid.NewGuid(), "lactose_free", "Laktosefrei", UserId, Now);
        ingredient.PublishDraft(published.Id, UserId, Now);

        IngredientRevision next = ingredient.CreateDraftFromPublished(
            Guid.NewGuid(), UserId, Now.AddDays(1));

        Assert.Equal("lactose_free", Assert.Single(next.Variants).VariantKey);
        Assert.Equal(IngredientPropertyState.Contains, Assert.Single(next.Allergens).State);
    }

    [Fact]
    public void Published_revision_rejects_property_and_variant_changes()
    {
        var ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision revision = ingredient.CreateDraft(
            Guid.NewGuid(), "Butter", CategoryId, UnitId, UserId, Now);
        IngredientVariantRevision variant = revision.AddVariant(
            Guid.NewGuid(), "lactose_free", "Laktosefrei", UserId, Now);
        ingredient.PublishDraft(revision.Id, UserId, Now);

        Assert.Throws<InvalidOperationException>(() => revision.SetAllergen(
            Value(Guid.NewGuid(), IngredientPropertyState.Contains), UserId, Now));
        Assert.Throws<InvalidOperationException>(() => revision.SetVariantOriginOverride(
            variant.VariantKey,
            Value(Guid.NewGuid(), IngredientPropertyState.DoesNotContain),
            UserId,
            Now));
    }

    private static IngredientRevision CreateDraft()
    {
        var ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        return ingredient.CreateDraft(Guid.NewGuid(), "Butter", CategoryId, UnitId, UserId, Now);
    }

    private static IngredientPropertyValue Value(Guid id, IngredientPropertyState state) =>
        new(id, state, IngredientPropertySource.ManuallyVerified);
}
