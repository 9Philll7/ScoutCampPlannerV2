using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class RecipePublisher(
    CateringDbContext database,
    RecipeDraftStore drafts,
    RecipePublicationValidator validator,
    RecipeSnapshotBuilder snapshots) : IRecipePublisher
{
    public async Task<RecipePublicationResult> PublishAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        bool acknowledgeWarnings,
        RecipeValidationContext? validationContext = null,
        string? changeNote = null,
        CancellationToken cancellationToken = default)
    {
        RecipeDraft? draft = await drafts.FindAsync(recipeId, cancellationToken);
        if (draft is null)
            return Result(RecipePublicationStatus.NotFound);
        if (draft.Status == RecipeStatus.Archived)
            return Result(RecipePublicationStatus.Archived, currentDraft: draft);
        if (draft.DraftVersion != expectedVersion)
            return Result(RecipePublicationStatus.VersionConflict, currentDraft: draft);

        RecipeValidationResult validation = validator.Validate(draft, validationContext);
        if (validation.Errors.Count != 0)
            return Result(RecipePublicationStatus.ValidationFailed, validation, currentDraft: draft);
        if (validation.Warnings.Count != 0 && !acknowledgeWarnings)
            return Result(RecipePublicationStatus.WarningAcknowledgementRequired, validation, currentDraft: draft);

        RecipeSnapshot snapshot = snapshots.Build(draft);
        string snapshotJson = RecipeSnapshotBuilder.Serialize(snapshot);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        RecipeRecord? record = await database.Set<RecipeRecord>()
            .SingleOrDefaultAsync(value => value.Id == recipeId, cancellationToken);
        if (record is null)
            return Result(RecipePublicationStatus.NotFound);
        if (record.Status == (int)RecipeStatus.Archived)
            return Result(RecipePublicationStatus.Archived, currentDraft: draft);
        if (record.DraftVersion != expectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return Result(
                RecipePublicationStatus.VersionConflict,
                currentDraft: await drafts.FindAsync(recipeId, cancellationToken));
        }

        int revisionNumber = (await database.Set<RecipeRevisionRecord>().AsNoTracking()
            .Where(value => value.RecipeId == recipeId)
            .Select(value => (int?)value.RevisionNumber).MaxAsync(cancellationToken) ?? 0) + 1;
        Guid revisionId = Guid.NewGuid();
        var revisionRecord = new RecipeRevisionRecord
        {
            Id = revisionId,
            RecipeId = recipeId,
            RevisionNumber = revisionNumber,
            PublishedAtUtc = timestampUtc,
            PublishedBy = actorUserId,
            ChangeNote = string.IsNullOrWhiteSpace(changeNote) ? null : changeNote.Trim(),
            SnapshotSchemaVersion = RecipeSnapshotBuilder.CurrentSchemaVersion,
            SnapshotJson = snapshotJson,
        };
        database.Add(revisionRecord);
        foreach (RecipeValidationIssue warning in validation.Warnings)
            database.Add(ToWarningRecord(revisionId, warning, actorUserId, timestampUtc));
        record.Status = (int)RecipeStatus.Active;
        record.DraftVersion = expectedVersion + 1;
        record.UpdatedBy = actorUserId;
        record.UpdatedAtUtc = timestampUtc;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return Result(
                RecipePublicationStatus.VersionConflict,
                currentDraft: await drafts.FindAsync(recipeId, cancellationToken));
        }

        draft.MarkPublished(expectedVersion + 1);
        var revision = new RecipeRevision(
            revisionId, recipeId, revisionNumber, timestampUtc, actorUserId,
            RecipeSnapshotBuilder.CurrentSchemaVersion, snapshotJson, changeNote);
        return Result(RecipePublicationStatus.Published, validation, revision, draft);
    }

    private static RecipeRevisionWarningRecord ToWarningRecord(
        Guid revisionId,
        RecipeValidationIssue warning,
        Guid actorUserId,
        DateTimeOffset timestampUtc)
    {
        Guid? positionId = TryGuid(warning.Context, "positionId");
        Guid? replacementId = TryGuid(warning.Context, "replacementId");
        Guid? conflictId = TryGuid(warning.Context, "conflictId");
        int? conflictType = warning.Context.TryGetValue("conflictType", out string? type) &&
                            Enum.TryParse(type, out ConflictType parsed)
            ? (int)parsed
            : null;
        return new RecipeRevisionWarningRecord
        {
            Id = Guid.NewGuid(), RecipeRevisionId = revisionId, WarningCode = warning.Code,
            Message = warning.Message, ContextJson = JsonSerializer.Serialize(warning.Context),
            SnapshotPositionId = positionId, SnapshotReplacementId = replacementId,
            ConflictType = conflictType, ConflictId = conflictId,
            AcknowledgedBy = actorUserId, AcknowledgedAtUtc = timestampUtc,
        };
    }

    private static Guid? TryGuid(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value) && Guid.TryParse(value, out Guid parsed) ? parsed : null;

    private static RecipePublicationResult Result(
        RecipePublicationStatus status,
        RecipeValidationResult? validation = null,
        RecipeRevision? revision = null,
        RecipeDraft? currentDraft = null) => new(status, validation, revision, currentDraft);
}
