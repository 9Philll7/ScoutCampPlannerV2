using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public sealed record RecipeEditorConflict(ConflictType Type, Guid Id);
public sealed record RecipeEditorGroup(Guid Id, string? Name, int SortOrder);
public sealed record RecipeEditorIngredientReplacement(
    Guid Id, Guid? IngredientRevisionId, decimal? Quantity, Guid? UnitId,
    IReadOnlyList<RecipeEditorConflict> Conflicts);
public sealed record RecipeEditorIngredientPosition(
    Guid Id, Guid? GroupId, Guid? IngredientRevisionId, decimal? Quantity, Guid? UnitId,
    int SortOrder, ScalingMode ScalingMode, AgeGroupScalingMode AgeGroupScaling,
    decimal? StepSize, decimal? QuantityPerStep,
    IReadOnlyList<RecipeEditorIngredientReplacement> Replacements);
public sealed record RecipeEditorSubrecipeReplacement(
    Guid Id, Guid? RecipeRevisionId, decimal? Servings, decimal? Quantity, Guid? UnitId,
    IReadOnlyList<RecipeEditorConflict> Conflicts);
public sealed record RecipeEditorSubrecipePosition(
    Guid Id, Guid? GroupId, Guid? RecipeRevisionId, decimal? Servings, decimal? Quantity,
    Guid? UnitId, int SortOrder, IReadOnlyList<RecipeEditorSubrecipeReplacement> Replacements);
public sealed record RecipeEditorAuthoringStage(Guid StageId, string StageName, decimal Factor);

public sealed record RecipeEditorContent(
    string? Name,
    string? Description,
    string? Source,
    string? InternalNotes,
    RecipeType RecipeType,
    decimal? ReferenceServings,
    decimal? ReferenceQuantity,
    Guid? ReferenceUnitId,
    bool? DefaultAgeGroupScalingApplies,
    RecipeEditorAuthoringStage? AuthoringStage,
    IReadOnlyList<string> Tags,
    IReadOnlyList<RecipeEditorGroup> Groups,
    IReadOnlyList<RecipeEditorIngredientPosition> IngredientPositions,
    IReadOnlyList<RecipeEditorSubrecipePosition> SubrecipePositions);

public sealed record RecipeEditorDraft(
    Guid Id,
    Guid CampId,
    RecipeStatus Status,
    long DraftVersion,
    RecipeEditorContent Content);

public enum RecipeEditorStatus
{
    Found,
    Created,
    Saved,
    NotFound,
    Forbidden,
    VersionConflict,
}

public sealed record RecipeEditorResult(RecipeEditorStatus Status, RecipeEditorDraft? Draft = null);

public interface IRecipeEditorAuthorization
{
    Task<bool> CanReadCampAsync(Guid actorUserId, Guid campId, CancellationToken cancellationToken = default);
    Task<bool> CanEditCampAsync(Guid actorUserId, Guid campId, CancellationToken cancellationToken = default);
}

public interface IRecipeEditorStore
{
    Task<RecipeDraft?> FindAsync(Guid recipeId, CancellationToken cancellationToken = default);
    Task<RecipeDraft> CreateCampAsync(
        RecipeDraft draft, Guid campId, Guid actorUserId, DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<RecipeDraftSaveResult> SaveAsync(
        RecipeDraft draft, long expectedVersion, Guid actorUserId, DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
}

public sealed class RecipeEditorService(
    IRecipeEditorStore drafts,
    IRecipeEditorAuthorization authorization,
    TimeProvider timeProvider)
{
    public async Task<RecipeEditorResult> FindCampAsync(
        Guid campId, Guid recipeId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(campId, nameof(campId));
        Required(recipeId, nameof(recipeId));
        Required(actorUserId, nameof(actorUserId));
        if (!await authorization.CanReadCampAsync(actorUserId, campId, cancellationToken))
            return new(RecipeEditorStatus.Forbidden);
        RecipeDraft? draft = await drafts.FindAsync(recipeId, cancellationToken);
        return draft is null || draft.ScopeType != RecipeScopeType.Camp || draft.ScopeId != campId
            ? new(RecipeEditorStatus.NotFound)
            : new(RecipeEditorStatus.Found, Map(draft));
    }

    public async Task<RecipeEditorResult> CreateCampAsync(
        Guid campId, RecipeEditorContent content, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(campId, nameof(campId));
        Required(actorUserId, nameof(actorUserId));
        ArgumentNullException.ThrowIfNull(content);
        if (!await authorization.CanEditCampAsync(actorUserId, campId, cancellationToken))
            return new(RecipeEditorStatus.Forbidden);
        RecipeDraft draft = Build(Guid.NewGuid(), RecipeScopeType.Camp, campId, RecipeStatus.Draft, content);
        RecipeDraft created = await drafts.CreateCampAsync(
            draft, campId, actorUserId, timeProvider.GetUtcNow(), cancellationToken);
        return new(RecipeEditorStatus.Created, Map(created));
    }

    public async Task<RecipeEditorResult> SaveCampAsync(
        Guid campId, Guid recipeId, long expectedVersion, RecipeEditorContent content,
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(campId, nameof(campId));
        Required(recipeId, nameof(recipeId));
        Required(actorUserId, nameof(actorUserId));
        if (expectedVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        ArgumentNullException.ThrowIfNull(content);
        if (!await authorization.CanEditCampAsync(actorUserId, campId, cancellationToken))
            return new(RecipeEditorStatus.Forbidden);
        RecipeDraft? current = await drafts.FindAsync(recipeId, cancellationToken);
        if (current is null || current.ScopeType != RecipeScopeType.Camp || current.ScopeId != campId)
            return new(RecipeEditorStatus.NotFound);
        RecipeDraft replacement = Build(recipeId, current.ScopeType, current.ScopeId, current.Status, content);
        RecipeDraftSaveResult saved = await drafts.SaveAsync(
            replacement, expectedVersion, actorUserId, timeProvider.GetUtcNow(), cancellationToken);
        return saved.Status switch
        {
            RecipeDraftSaveStatus.Saved => new(RecipeEditorStatus.Saved, Map(saved.CurrentDraft!)),
            RecipeDraftSaveStatus.VersionConflict => new(RecipeEditorStatus.VersionConflict,
                saved.CurrentDraft is null ? null : Map(saved.CurrentDraft)),
            _ => new(RecipeEditorStatus.NotFound),
        };
    }

    private static RecipeDraft Build(
        Guid recipeId, RecipeScopeType scopeType, Guid? scopeId, RecipeStatus status, RecipeEditorContent content)
    {
        var draft = new RecipeDraft(recipeId, scopeType, scopeId, content.RecipeType, content.Name, status);
        draft.SetDetails(content.Description, content.Source, content.InternalNotes);
        if (content.RecipeType == RecipeType.PortionBased)
        {
            AuthoringStageSnapshot? stage = content.AuthoringStage is null ? null : new AuthoringStageSnapshot(
                content.AuthoringStage.StageId, content.AuthoringStage.StageName, content.AuthoringStage.Factor);
            draft.ConfigurePortionReference(content.ReferenceServings, content.DefaultAgeGroupScalingApplies, stage);
        }
        else
        {
            draft.ConfigureQuantityReference(content.ReferenceQuantity, content.ReferenceUnitId);
        }
        draft.ReplaceTags(content.Tags ?? []);
        foreach (RecipeEditorGroup group in content.Groups ?? [])
            draft.AddGroup(new RecipeIngredientGroup(group.Id, recipeId, group.Name, group.SortOrder));
        foreach (RecipeEditorIngredientPosition value in content.IngredientPositions ?? [])
        {
            var position = new RecipeIngredientPosition(
                value.Id, recipeId, value.GroupId, value.IngredientRevisionId, value.Quantity, value.UnitId,
                value.SortOrder, value.ScalingMode, value.AgeGroupScaling,
                value.StepSize.HasValue || value.QuantityPerStep.HasValue
                    ? new StepwiseScaling(value.StepSize, value.QuantityPerStep)
                    : null);
            foreach (RecipeEditorIngredientReplacement replacement in value.Replacements ?? [])
                position.AddReplacementRule(new IngredientReplacementRule(
                    replacement.Id, position.Id, replacement.IngredientRevisionId,
                    replacement.Quantity, replacement.UnitId,
                    (replacement.Conflicts ?? []).Select(conflict =>
                        new ConflictReference(conflict.Type, conflict.Id))));
            draft.AddIngredientPosition(position);
        }
        foreach (RecipeEditorSubrecipePosition value in content.SubrecipePositions ?? [])
        {
            var position = new RecipeSubrecipePosition(
                value.Id, recipeId, value.GroupId, value.RecipeRevisionId, value.Servings,
                value.Quantity, value.UnitId, value.SortOrder);
            foreach (RecipeEditorSubrecipeReplacement replacement in value.Replacements ?? [])
                position.AddReplacementRule(new RecipeReplacementRule(
                    replacement.Id, position.Id, replacement.RecipeRevisionId,
                    replacement.Servings, replacement.Quantity, replacement.UnitId,
                    (replacement.Conflicts ?? []).Select(conflict =>
                        new ConflictReference(conflict.Type, conflict.Id))));
            draft.AddSubrecipePosition(position);
        }
        return draft;
    }

    private static RecipeEditorDraft Map(RecipeDraft draft) => new(
        draft.Id,
        draft.ScopeId!.Value,
        draft.Status,
        draft.DraftVersion,
        new RecipeEditorContent(
            draft.Name, draft.Description, draft.Source, draft.InternalNotes, draft.RecipeType,
            draft.ReferenceServings, draft.ReferenceQuantity, draft.ReferenceUnitId,
            draft.DefaultAgeGroupScalingApplies,
            draft.AuthoringStage is null ? null : new RecipeEditorAuthoringStage(
                draft.AuthoringStage.StageId, draft.AuthoringStage.StageName, draft.AuthoringStage.Factor),
            draft.Tags.Order(StringComparer.Ordinal).ToArray(),
            draft.Groups.Select(group => new RecipeEditorGroup(group.Id, group.Name, group.SortOrder)).ToArray(),
            draft.IngredientPositions.Select(position => new RecipeEditorIngredientPosition(
                position.Id, position.GroupId, position.IngredientRevisionId, position.Quantity,
                position.UnitId, position.SortOrder, position.ScalingMode, position.AgeGroupScaling,
                position.StepwiseScaling?.StepSize, position.StepwiseScaling?.QuantityPerStep,
                position.ReplacementRules.Select(replacement => new RecipeEditorIngredientReplacement(
                    replacement.Id, replacement.ReplacementIngredientRevisionId,
                    replacement.ReplacementQuantity, replacement.ReplacementUnitId,
                    replacement.Conflicts.Select(conflict => new RecipeEditorConflict(conflict.Type, conflict.Id))
                        .ToArray())).ToArray())).ToArray(),
            draft.SubrecipePositions.Select(position => new RecipeEditorSubrecipePosition(
                position.Id, position.GroupId, position.RecipeRevisionId, position.RequiredServings,
                position.RequiredQuantity, position.RequiredUnitId, position.SortOrder,
                position.ReplacementRules.Select(replacement => new RecipeEditorSubrecipeReplacement(
                    replacement.Id, replacement.ReplacementRecipeRevisionId,
                    replacement.ReplacementServings, replacement.ReplacementQuantity,
                    replacement.ReplacementUnitId,
                    replacement.Conflicts.Select(conflict => new RecipeEditorConflict(conflict.Type, conflict.Id))
                        .ToArray())).ToArray())).ToArray()));

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
