using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class RecipeChangeSubmissionStore(
    CateringDbContext database,
    RecipeDraftStore drafts,
    RecipePublisher publisher) : IRecipeChangeSubmissionStore
{
    public async Task<RecipeSubmissionCandidate?> FindCandidateAsync(
        Guid localRevisionId,
        CancellationToken cancellationToken = default) =>
        await (from revision in database.Set<RecipeRevisionRecord>().AsNoTracking()
            join recipe in database.Set<RecipeRecord>().AsNoTracking() on revision.RecipeId equals recipe.Id
            where revision.Id == localRevisionId && recipe.ScopeType != (int)RecipeScopeType.Central
            select new RecipeSubmissionCandidate(
                revision.Id, (RecipeScopeType)recipe.ScopeType, recipe.ScopeId!.Value))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<RecipeSubmissionResult> SubmitAsync(
        Guid submissionId,
        Guid localRevisionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        var source = await (from revision in database.Set<RecipeRevisionRecord>().AsNoTracking()
            join recipe in database.Set<RecipeRecord>().AsNoTracking() on revision.RecipeId equals recipe.Id
            where revision.Id == localRevisionId
            select new
            {
                LocalScope = (RecipeScopeType)recipe.ScopeType,
                recipe.CentralSourceRecipeId,
                recipe.CentralSourceRevisionId,
            }).SingleOrDefaultAsync(cancellationToken);
        if (source is null) return new RecipeSubmissionResult(RecipeSubmissionStatus.NotFound);
        if (source.LocalScope == RecipeScopeType.Central)
            return new RecipeSubmissionResult(RecipeSubmissionStatus.InvalidSource);
        if (!source.CentralSourceRecipeId.HasValue || !source.CentralSourceRevisionId.HasValue)
            return new RecipeSubmissionResult(RecipeSubmissionStatus.NoCentralLineage);
        if (await database.Set<CentralRecipeChangeSubmissionRecord>().AsNoTracking()
                .AnyAsync(value => value.SubmittedLocalRecipeRevisionId == localRevisionId, cancellationToken))
            return new RecipeSubmissionResult(RecipeSubmissionStatus.AlreadySubmitted);

        database.Add(new CentralRecipeChangeSubmissionRecord
        {
            Id = submissionId,
            CentralRecipeId = source.CentralSourceRecipeId.Value,
            SourceCentralRevisionId = source.CentralSourceRevisionId.Value,
            SubmittedLocalRecipeRevisionId = localRevisionId,
            Status = (int)CentralRecipeChangeSubmissionStatus.Pending,
            SubmittedBy = actorUserId,
            SubmittedAtUtc = timestampUtc,
        });
        await database.SaveChangesAsync(cancellationToken);
        return new RecipeSubmissionResult(RecipeSubmissionStatus.Submitted, submissionId);
    }

    public async Task<IReadOnlyList<CentralRecipeChangeComparison>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var pending = await database.Set<CentralRecipeChangeSubmissionRecord>().AsNoTracking()
            .Where(value => value.Status == (int)CentralRecipeChangeSubmissionStatus.Pending)
            .OrderBy(value => value.Id).ToArrayAsync(cancellationToken);
        if (pending.Length == 0) return [];
        Guid[] centralRecipeIds = pending.Select(value => value.CentralRecipeId).Distinct().ToArray();
        RecipeRevisionRecord[] revisions = await database.Set<RecipeRevisionRecord>().AsNoTracking()
            .Where(value => centralRecipeIds.Contains(value.RecipeId) ||
                            pending.Select(item => item.SubmittedLocalRecipeRevisionId).Contains(value.Id))
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, RecipeRevisionRecord> byId = revisions.ToDictionary(value => value.Id);
        Dictionary<Guid, RecipeRevisionRecord> latest = revisions
            .Where(value => centralRecipeIds.Contains(value.RecipeId))
            .GroupBy(value => value.RecipeId)
            .ToDictionary(group => group.Key, group => group.MaxBy(value => value.RevisionNumber)!);
        return pending.Select(value =>
        {
            RecipeRevisionRecord source = byId[value.SourceCentralRevisionId];
            RecipeRevisionRecord current = latest[value.CentralRecipeId];
            RecipeRevisionRecord submitted = byId[value.SubmittedLocalRecipeRevisionId];
            return new CentralRecipeChangeComparison(
                value.Id, value.CentralRecipeId,
                source.Id, RecipeSnapshotBuilder.Deserialize(source.SnapshotJson),
                current.Id, RecipeSnapshotBuilder.Deserialize(current.SnapshotJson),
                submitted.Id, RecipeSnapshotBuilder.Deserialize(submitted.SnapshotJson),
                value.SubmittedBy, value.SubmittedAtUtc);
        }).ToArray();
    }

    public async Task<RecipeSubmissionReviewResult> AcceptAsync(
        Guid submissionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        bool acknowledgeWarnings,
        string? changeNote = null,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        CentralRecipeChangeSubmissionRecord? submission = await database
            .Set<CentralRecipeChangeSubmissionRecord>()
            .SingleOrDefaultAsync(value => value.Id == submissionId, cancellationToken);
        if (submission is null)
            return Review(RecipeSubmissionReviewStatus.NotFound);
        if (submission.Status != (int)CentralRecipeChangeSubmissionStatus.Pending)
            return Review(RecipeSubmissionReviewStatus.AlreadyReviewed);

        RecipeRevisionRecord? submittedRevision = await database.Set<RecipeRevisionRecord>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == submission.SubmittedLocalRecipeRevisionId, cancellationToken);
        RecipeDraft? current = await drafts.FindAsync(submission.CentralRecipeId, cancellationToken);
        if (submittedRevision is null || current is null)
            return Review(RecipeSubmissionReviewStatus.NotFound);

        RecipeSnapshot submittedSnapshot = RecipeSnapshotBuilder.Deserialize(submittedRevision.SnapshotJson);
        RecipeDraft acceptedDraft = RecipeDraftCopy.FromSnapshot(
            current.Id, RecipeScopeType.Central, null, current.Status, submittedSnapshot, submittedSnapshot.Name);

        RecipeDraftSaveResult saved = await drafts.SaveAsync(
            acceptedDraft, current.DraftVersion, actorUserId, timestampUtc, cancellationToken);
        if (saved.Status != RecipeDraftSaveStatus.Saved)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return Review(saved.Status == RecipeDraftSaveStatus.VersionConflict
                ? RecipeSubmissionReviewStatus.VersionConflict
                : RecipeSubmissionReviewStatus.NotFound);
        }

        RecipePublicationResult published = await publisher.PublishAsync(
            current.Id, saved.CurrentDraft!.DraftVersion, actorUserId, timestampUtc,
            acknowledgeWarnings, changeNote: changeNote, cancellationToken: cancellationToken);
        if (published.Status != RecipePublicationStatus.Published)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return published.Status switch
            {
                RecipePublicationStatus.ValidationFailed =>
                    Review(RecipeSubmissionReviewStatus.ValidationFailed, published.Validation),
                RecipePublicationStatus.WarningAcknowledgementRequired =>
                    Review(RecipeSubmissionReviewStatus.WarningAcknowledgementRequired, published.Validation),
                RecipePublicationStatus.VersionConflict => Review(RecipeSubmissionReviewStatus.VersionConflict),
                _ => Review(RecipeSubmissionReviewStatus.NotFound),
            };
        }

        submission.Status = (int)CentralRecipeChangeSubmissionStatus.Accepted;
        submission.ReviewedBy = actorUserId;
        submission.ReviewedAtUtc = timestampUtc;
        submission.ResultingCentralRevisionId = published.Revision!.Id;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Review(RecipeSubmissionReviewStatus.Accepted, published.Validation, published.Revision);
    }

    public async Task<RecipeSubmissionReviewResult> RejectAsync(
        Guid submissionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        CentralRecipeChangeSubmissionRecord? submission = await database
            .Set<CentralRecipeChangeSubmissionRecord>()
            .SingleOrDefaultAsync(value => value.Id == submissionId, cancellationToken);
        if (submission is null)
            return Review(RecipeSubmissionReviewStatus.NotFound);
        if (submission.Status != (int)CentralRecipeChangeSubmissionStatus.Pending)
            return Review(RecipeSubmissionReviewStatus.AlreadyReviewed);
        submission.Status = (int)CentralRecipeChangeSubmissionStatus.Rejected;
        submission.ReviewedBy = actorUserId;
        submission.ReviewedAtUtc = timestampUtc;
        await database.SaveChangesAsync(cancellationToken);
        return Review(RecipeSubmissionReviewStatus.Rejected);
    }

    private static RecipeSubmissionReviewResult Review(
        RecipeSubmissionReviewStatus status,
        RecipeValidationResult? validation = null,
        RecipeRevision? revision = null) => new(status, validation, revision);
}
