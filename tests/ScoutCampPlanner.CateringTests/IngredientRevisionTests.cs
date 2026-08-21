using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientRevisionTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid UnitId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Central_ingredient_can_publish_a_draft()
    {
        var ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision draft = ingredient.CreateDraft(
            Guid.NewGuid(), " Butter ", CategoryId, UnitId, UserId, Now);

        ingredient.PublishDraft(draft.Id, UserId, Now.AddMinutes(1));

        Assert.Equal(IngredientRevisionState.Published, draft.State);
        Assert.Equal(draft.Id, ingredient.CurrentPublishedRevisionId);
        Assert.Equal("Butter", draft.Name);
        Assert.Equal(UserId, draft.PublishedBy);
    }

    [Fact]
    public void Published_revision_is_immutable()
    {
        var ingredient = PublishedCentralIngredient(out IngredientRevision published);

        Assert.Throws<InvalidOperationException>(() => published.SetContent(
            "Neue Butter", CategoryId, UnitId, UserId, Now.AddDays(1)));
        Assert.Equal(ingredient.CurrentPublishedRevisionId, published.Id);
    }

    [Fact]
    public void Editing_a_published_ingredient_creates_a_new_draft()
    {
        var ingredient = PublishedCentralIngredient(out IngredientRevision published);

        IngredientRevision draft = ingredient.CreateDraftFromPublished(
            Guid.NewGuid(), UserId, Now.AddDays(1));

        Assert.Equal(IngredientRevisionState.Draft, draft.State);
        Assert.Equal(published.Id, draft.BasedOnRevisionId);
        Assert.Equal(published.Name, draft.Name);
        Assert.Equal(2, draft.RevisionNumber);
    }

    [Theory]
    [InlineData(IngredientScopeType.Tenant)]
    [InlineData(IngredientScopeType.Camp)]
    public void Local_fork_references_central_ingredient_and_source_revision(IngredientScopeType scope)
    {
        IngredientIdentity central = PublishedCentralIngredient(out IngredientRevision source);

        IngredientIdentity fork = IngredientIdentity.ForkCentral(
            Guid.NewGuid(), scope, Guid.NewGuid(), central, source,
            Guid.NewGuid(), UserId, Now.AddDays(1));

        Assert.Equal(central.Id, fork.SourceIngredientId);
        Assert.Equal(source.Id, fork.SourceRevisionId);
        Assert.Single(fork.Revisions);
        Assert.Equal(IngredientRevisionState.Draft, fork.Revisions.Single().State);
    }

    [Fact]
    public void Independent_local_ingredient_has_no_fork_source()
    {
        IngredientIdentity ingredient = IngredientIdentity.CreateLocal(
            Guid.NewGuid(), IngredientScopeType.Tenant, Guid.NewGuid());

        Assert.Null(ingredient.SourceIngredientId);
        Assert.Null(ingredient.SourceRevisionId);
    }

    [Fact]
    public void Only_one_draft_is_allowed()
    {
        var ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        ingredient.CreateDraft(Guid.NewGuid(), "Butter", CategoryId, UnitId, UserId, Now);

        Assert.Throws<InvalidOperationException>(() => ingredient.CreateDraft(
            Guid.NewGuid(), "Butter", CategoryId, UnitId, UserId, Now));
    }

    [Fact]
    public void Archived_identity_cannot_create_or_publish_drafts()
    {
        var ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision draft = ingredient.CreateDraft(
            Guid.NewGuid(), "Butter", CategoryId, UnitId, UserId, Now);
        ingredient.Archive();

        Assert.Throws<InvalidOperationException>(() => ingredient.PublishDraft(draft.Id, UserId, Now));
        Assert.Throws<InvalidOperationException>(() => ingredient.CreateDraft(
            Guid.NewGuid(), "Butter", CategoryId, UnitId, UserId, Now));
    }

    private static IngredientIdentity PublishedCentralIngredient(out IngredientRevision revision)
    {
        var ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        revision = ingredient.CreateDraft(
            Guid.NewGuid(), "Butter", CategoryId, UnitId, UserId, Now);
        ingredient.PublishDraft(revision.Id, UserId, Now.AddMinutes(1));
        return ingredient;
    }
}
