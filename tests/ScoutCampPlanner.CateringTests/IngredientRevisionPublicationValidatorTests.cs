using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientRevisionPublicationValidatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 14, 0, 0, TimeSpan.Zero);
    private readonly IngredientPropertyDefinition group = new(Guid.NewGuid(), "GLUTEN_CEREALS");
    private readonly IngredientPropertyDefinition wheat;

    public IngredientRevisionPublicationValidatorTests() =>
        wheat = new IngredientPropertyDefinition(Guid.NewGuid(), "WHEAT", group.Id);

    [Fact]
    public void Publish_requires_all_property_groups_to_be_reviewed()
    {
        IngredientIdentity ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision draft = Draft(ingredient);
        var validator = new IngredientRevisionPublicationValidator([group, wheat]);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            ingredient.PublishDraft(draft.Id, UserId, Now, validator));

        Assert.Contains("reviewed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(IngredientRevisionState.Draft, draft.State);
        Assert.Null(ingredient.CurrentPublishedRevisionId);
    }

    [Fact]
    public void Contradicting_parent_and_child_prevent_publish()
    {
        IngredientIdentity ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision draft = ReviewedDraft(ingredient);
        draft.SetAllergen(Value(group, IngredientPropertyState.DoesNotContain), UserId, Now);
        draft.SetAllergen(Value(wheat, IngredientPropertyState.Contains), UserId, Now);
        var validator = new IngredientRevisionPublicationValidator([group, wheat]);

        Assert.Throws<InvalidOperationException>(() =>
            ingredient.PublishDraft(draft.Id, UserId, Now, validator));
        Assert.Equal(IngredientRevisionState.Draft, draft.State);
    }

    [Fact]
    public void Contradiction_in_effective_variant_values_prevents_publish()
    {
        IngredientIdentity ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision draft = ReviewedDraft(ingredient);
        draft.SetAllergen(Value(group, IngredientPropertyState.DoesNotContain), UserId, Now);
        IngredientVariantRevision variant = draft.AddVariant(
            Guid.NewGuid(), "with_wheat", "Mit Weizen", UserId, Now);
        draft.SetVariantAllergenOverride(
            variant.VariantKey,
            Value(wheat, IngredientPropertyState.Contains),
            UserId,
            Now);
        var validator = new IngredientRevisionPublicationValidator([group, wheat]);

        Assert.Throws<InvalidOperationException>(() =>
            ingredient.PublishDraft(draft.Id, UserId, Now, validator));
    }

    [Fact]
    public void Consistent_reviewed_revision_can_be_published()
    {
        IngredientIdentity ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision draft = ReviewedDraft(ingredient);
        draft.SetAllergen(Value(wheat, IngredientPropertyState.Contains), UserId, Now);
        var validator = new IngredientRevisionPublicationValidator([group, wheat]);

        ingredient.PublishDraft(draft.Id, UserId, Now, validator);

        Assert.Equal(IngredientRevisionState.Published, draft.State);
        Assert.Equal(draft.Id, ingredient.CurrentPublishedRevisionId);
    }

    [Fact]
    public void Invalid_allergen_hierarchy_is_rejected_when_validator_is_created()
    {
        var invalid = new IngredientPropertyDefinition(Guid.NewGuid(), "WHEAT", Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => new IngredientRevisionPublicationValidator([invalid]));
    }

    private static IngredientRevision Draft(IngredientIdentity ingredient) => ingredient.CreateDraft(
        Guid.NewGuid(), "Mehl", Guid.NewGuid(), Guid.NewGuid(), UserId, Now);

    private static IngredientRevision ReviewedDraft(IngredientIdentity ingredient)
    {
        IngredientRevision draft = Draft(ingredient);
        draft.SetReviewStates(
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed,
            UserId,
            Now);
        return draft;
    }

    private static IngredientPropertyValue Value(
        IngredientPropertyDefinition definition,
        IngredientPropertyState state) =>
        new(definition.Id, state, IngredientPropertySource.ManuallyVerified);
}
