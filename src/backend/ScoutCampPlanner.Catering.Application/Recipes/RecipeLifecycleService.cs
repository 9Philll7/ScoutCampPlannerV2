using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public sealed record RecipeRevisionSnapshot(
    Guid RecipeId,
    RecipeSnapshot Snapshot,
    RecipeScopeType? ScopeType = null);

public interface IRecipeRevisionSource
{
    RecipeRevisionSnapshot GetRevisionSnapshot(Guid revisionId);
}

public sealed class RecipeLifecycleService(
    IRecipeDraftStore drafts,
    IRecipeRevisionSource revisions,
    IRecipePermanentDeleteAuthorization? deleteAuthorization = null)
{
    public Task<RecipeLifecycleResult> ArchiveAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        Version(expectedVersion);
        return drafts.ArchiveAsync(
            Required(recipeId, nameof(recipeId)), expectedVersion,
            Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);
    }

    public Task<RecipeLifecycleResult> ReactivateAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        Version(expectedVersion);
        return drafts.ReactivateAsync(
            Required(recipeId, nameof(recipeId)), expectedVersion,
            Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);
    }

    public Task<RecipeLifecycleResult> ResetToDraftAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        Version(expectedVersion);
        return drafts.ResetToDraftAsync(
            Required(recipeId, nameof(recipeId)), expectedVersion,
            Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);
    }

    public async Task<RecipePermanentDeleteResult> DeletePermanentlyAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(recipeId, nameof(recipeId));
        Required(actorUserId, nameof(actorUserId));
        Version(expectedVersion);
        if (deleteAuthorization is null ||
            !await deleteAuthorization.CanPermanentlyDeleteCentralRecipesAsync(actorUserId, cancellationToken))
            return new RecipePermanentDeleteResult(RecipePermanentDeleteStatus.Forbidden);

        RecipeDraft? current = await drafts.FindAsync(recipeId, cancellationToken);
        if (current is null)
            return new RecipePermanentDeleteResult(RecipePermanentDeleteStatus.NotFound);
        if (current.ScopeType != RecipeScopeType.Central)
            return new RecipePermanentDeleteResult(RecipePermanentDeleteStatus.ScopeNotSupported, current);
        return await drafts.DeletePermanentlyAsync(recipeId, expectedVersion, cancellationToken);
    }

    public async Task<RecipeDraftSaveResult> RestoreRevisionAsync(
        Guid recipeId,
        Guid revisionId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        Required(recipeId, nameof(recipeId));
        Required(revisionId, nameof(revisionId));
        RecipeDraft? current = await drafts.FindAsync(recipeId, cancellationToken);
        if (current is null)
            return new RecipeDraftSaveResult(RecipeDraftSaveStatus.NotFound, null);

        RecipeRevisionSnapshot source = revisions.GetRevisionSnapshot(revisionId);
        if (source.RecipeId != recipeId)
            throw new InvalidOperationException("Only a revision of the same recipe can be restored.");
        RecipeDraft restored = RecipeDraftCopy.FromSnapshot(
            recipeId, current.ScopeType, current.ScopeId, current.Status, source.Snapshot, source.Snapshot.Name);
        return await drafts.SaveAsync(
            restored, expectedVersion, Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);
    }

    public Task<RecipeDraft> DuplicateRevisionAsync(
        Guid sourceRevisionId,
        Guid newRecipeId,
        RecipeScopeType destinationScope,
        Guid? destinationScopeId,
        string newName,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        Required(sourceRevisionId, nameof(sourceRevisionId));
        Required(newRecipeId, nameof(newRecipeId));
        RecipeRevisionSnapshot source = revisions.GetRevisionSnapshot(sourceRevisionId);
        RecipeDraft duplicate = RecipeDraftCopy.FromSnapshot(
            newRecipeId, destinationScope, destinationScopeId, RecipeStatus.Draft, source.Snapshot, newName);
        return drafts.CreateDerivedAsync(
            duplicate, new RecipeDraftLineage(source.RecipeId, sourceRevisionId, source.ScopeType),
            Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;

    private static void Version(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
    }
}

public static class RecipeDraftCopy
{
    public static RecipeDraft FromSnapshot(
        Guid recipeId,
        RecipeScopeType scopeType,
        Guid? scopeId,
        RecipeStatus status,
        RecipeSnapshot source,
        string name)
    {
        ArgumentNullException.ThrowIfNull(source);
        var draft = new RecipeDraft(recipeId, scopeType, scopeId, source.RecipeType, name, status);
        draft.SetDetails(source.Description, source.Source, source.InternalNotes);
        if (source.RecipeType == RecipeType.PortionBased)
        {
            AuthoringStageSnapshot? stage = scopeType == RecipeScopeType.Central || source.AuthoringStage is null
                ? null
                : new AuthoringStageSnapshot(
                    source.AuthoringStage.StageId, source.AuthoringStage.StageName, source.AuthoringStage.Factor);
            draft.ConfigurePortionReference(
                scopeType == RecipeScopeType.Central
                    ? source.Reference.StandardServings
                    : source.AuthoringStage?.EnteredServings ?? source.Reference.StandardServings,
                source.DefaultAgeGroupScalingApplies,
                stage);
        }
        else
        {
            draft.ConfigureQuantityReference(
                source.Reference.ReferenceQuantity, source.Reference.ReferenceUnit?.UnitId);
        }
        draft.ReplaceTags(source.Tags);

        Dictionary<Guid, Guid> groupIds = source.Groups.ToDictionary(value => value.Id, _ => Guid.NewGuid());
        foreach (RecipeGroupSnapshot group in source.Groups)
            draft.AddGroup(new RecipeIngredientGroup(groupIds[group.Id], recipeId, group.Name, group.SortOrder));
        foreach (IngredientPositionSnapshot position in source.IngredientPositions)
            draft.AddIngredientPosition(Copy(position, recipeId, MapGroup(position.GroupId, groupIds)));
        foreach (SubrecipePositionSnapshot position in source.SubrecipePositions)
            draft.AddSubrecipePosition(Copy(position, recipeId, MapGroup(position.GroupId, groupIds)));
        return draft;
    }

    private static RecipeIngredientPosition Copy(
        IngredientPositionSnapshot source,
        Guid recipeId,
        Guid? groupId)
    {
        var target = new RecipeIngredientPosition(
            Guid.NewGuid(), recipeId, groupId, source.Ingredient.IngredientId, source.Quantity,
            source.Unit.Unit.UnitId, source.SortOrder, source.ScalingMode, source.AgeGroupScaling,
            source.StepwiseScaling);
        foreach (IngredientReplacementSnapshot replacement in source.Replacements)
            target.AddReplacementRule(new IngredientReplacementRule(
                Guid.NewGuid(), target.Id, replacement.Ingredient.IngredientId, replacement.Quantity,
                replacement.Unit.Unit.UnitId, replacement.ApplicableConflicts));
        return target;
    }

    private static RecipeSubrecipePosition Copy(
        SubrecipePositionSnapshot source,
        Guid recipeId,
        Guid? groupId)
    {
        var target = new RecipeSubrecipePosition(
            Guid.NewGuid(), recipeId, groupId, source.RecipeRevisionId, source.RequiredServings,
            source.RequiredQuantity, source.RequiredUnit?.UnitId, source.SortOrder);
        foreach (RecipeReplacementSnapshot replacement in source.Replacements)
            target.AddReplacementRule(new RecipeReplacementRule(
                Guid.NewGuid(), target.Id, replacement.RecipeRevisionId, replacement.Servings,
                replacement.Quantity, replacement.Unit?.UnitId, replacement.ApplicableConflicts));
        return target;
    }

    private static Guid? MapGroup(Guid? sourceId, IReadOnlyDictionary<Guid, Guid> groupIds) =>
        sourceId.HasValue ? groupIds[sourceId.Value] : null;
}
