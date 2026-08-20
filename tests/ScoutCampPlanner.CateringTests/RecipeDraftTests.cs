using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeDraftTests
{
    [Fact]
    public void Draft_may_start_incomplete()
    {
        var draft = new RecipeDraft(Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased);

        Assert.Equal(RecipeStatus.Draft, draft.Status);
        Assert.Empty(draft.Name);
        Assert.Null(draft.ReferenceServings);
        Assert.Empty(draft.IngredientPositions);
        Assert.Empty(draft.SubrecipePositions);
    }

    [Fact]
    public void Tenant_draft_requires_owner_and_normalizes_name()
    {
        Guid tenantId = Guid.NewGuid();
        var draft = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Tenant, tenantId, RecipeType.PortionBased, "  Gemüse   Curry ");

        Assert.Equal(tenantId, draft.ScopeId);
        Assert.Equal("Gemüse Curry", draft.Name);
        Assert.Equal("GEMÜSE CURRY", draft.NormalizedName);
        Assert.Throws<ArgumentException>(() =>
            new RecipeDraft(Guid.NewGuid(), RecipeScopeType.Tenant, null, RecipeType.PortionBased));
    }

    [Fact]
    public void Portion_reference_keeps_complete_authoring_stage_snapshot()
    {
        var stage = new AuthoringStageSnapshot(Guid.NewGuid(), "WiWö", 0.75m);
        var draft = new RecipeDraft(Guid.NewGuid(), RecipeScopeType.Camp, Guid.NewGuid(), RecipeType.PortionBased);

        draft.ConfigurePortionReference(20m, true, stage);

        Assert.Equal(20m, draft.ReferenceServings);
        Assert.Equal(0.75m, draft.AuthoringStage!.Factor);
        Assert.Null(draft.ReferenceQuantity);
        Assert.Null(draft.ReferenceUnitId);
    }

    [Fact]
    public void Switching_reference_model_clears_incompatible_fields()
    {
        var draft = new RecipeDraft(Guid.NewGuid(), RecipeScopeType.Camp, Guid.NewGuid(), RecipeType.PortionBased);
        draft.ConfigurePortionReference(10m, true, new AuthoringStageSnapshot(Guid.NewGuid(), "GuSp", 1m));
        Guid unitId = Guid.NewGuid();

        draft.ConfigureQuantityReference(5m, unitId);

        Assert.Equal(RecipeType.QuantityBased, draft.RecipeType);
        Assert.Equal(5m, draft.ReferenceQuantity);
        Assert.Equal(unitId, draft.ReferenceUnitId);
        Assert.Null(draft.ReferenceServings);
        Assert.Null(draft.AuthoringStage);
        Assert.Null(draft.DefaultAgeGroupScalingApplies);
    }

    [Fact]
    public void Draft_supports_grouped_and_ungrouped_mixed_positions()
    {
        Guid recipeId = Guid.NewGuid();
        var draft = new RecipeDraft(recipeId, RecipeScopeType.Central, null, RecipeType.PortionBased);
        var group = new RecipeIngredientGroup(Guid.NewGuid(), recipeId, "Sauce", 1);
        draft.AddGroup(group);

        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), recipeId, null, Guid.NewGuid(), 1m, Guid.NewGuid(), 0));
        draft.AddSubrecipePosition(new RecipeSubrecipePosition(
            Guid.NewGuid(), recipeId, group.Id, Guid.NewGuid(), 2m, null, null, 1));

        Assert.Single(draft.IngredientPositions);
        Assert.Single(draft.SubrecipePositions);
        Assert.Equal(group.Id, draft.SubrecipePositions[0].GroupId);
    }

    [Fact]
    public void Position_rejects_group_from_another_recipe()
    {
        Guid recipeId = Guid.NewGuid();
        var draft = new RecipeDraft(recipeId, RecipeScopeType.Central, null, RecipeType.PortionBased);

        Assert.Throws<InvalidOperationException>(() => draft.AddIngredientPosition(
            new RecipeIngredientPosition(
                Guid.NewGuid(), recipeId, Guid.NewGuid(), Guid.NewGuid(), 1m, Guid.NewGuid(), 0)));
    }

    [Fact]
    public void Replacement_rule_keeps_multiple_typed_conflicts_without_duplicates()
    {
        Guid positionId = Guid.NewGuid();
        var conflict = new ConflictReference(ConflictType.Allergen, Guid.NewGuid());
        var intolerance = new ConflictReference(ConflictType.Intolerance, Guid.NewGuid());
        var rule = new IngredientReplacementRule(
            Guid.NewGuid(), positionId, Guid.NewGuid(), 1m, Guid.NewGuid(), [conflict, conflict, intolerance]);
        var position = new RecipeIngredientPosition(
            positionId, Guid.NewGuid(), null, Guid.NewGuid(), 1m, Guid.NewGuid(), 0);

        position.AddReplacementRule(rule);

        Assert.Equal(2, position.ReplacementRules.Single().Conflicts.Count);
    }

    [Fact]
    public void Tags_are_normalized_and_deduplicated()
    {
        var draft = new RecipeDraft(Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased);

        draft.ReplaceTags(["  Schnell ", "SCHNELL", "Vegetarisch"]);

        Assert.Equal(2, draft.Tags.Count);
        Assert.Contains("SCHNELL", draft.Tags);
        Assert.Contains("VEGETARISCH", draft.Tags);
    }

    [Fact]
    public void Published_revision_exposes_no_mutation_api()
    {
        var revision = new RecipeRevision(
            Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow, Guid.NewGuid(), 1, "{\"schemaVersion\":1}");

        Assert.All(typeof(RecipeRevision).GetProperties(), property => Assert.False(property.CanWrite));
        Assert.Equal(1, revision.RevisionNumber);
    }

    [Fact]
    public void Validation_errors_block_and_warnings_require_acknowledgement()
    {
        var warningOnly = new RecipeValidationResult([
            new RecipeValidationIssue(
                "recipe.description.missing", RecipeValidationSeverity.Warning, "Description is missing.")]);
        var withError = new RecipeValidationResult([
            new RecipeValidationIssue(
                "recipe.positions.empty", RecipeValidationSeverity.Error, "At least one position is required."),
            new RecipeValidationIssue(
                "recipe.description.missing", RecipeValidationSeverity.Warning, "Description is missing.")]);

        Assert.False(warningOnly.CanPublish(warningsAcknowledged: false));
        Assert.True(warningOnly.CanPublish(warningsAcknowledged: true));
        Assert.False(withError.CanPublish(warningsAcknowledged: true));
    }
}
