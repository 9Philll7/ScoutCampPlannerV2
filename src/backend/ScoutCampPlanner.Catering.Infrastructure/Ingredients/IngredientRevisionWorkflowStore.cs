using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

public sealed class IngredientRevisionWorkflowStore(CateringDbContext database)
    : IIngredientRevisionWorkflowStore
{
    public Task<IngredientRevisionScope?> GetScopeAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default) =>
        (from revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
         join identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
             on revision.IngredientId equals identity.Id
         where revision.Id == revisionId && identity.Status == (int)IngredientIdentityStatus.Active
         select new IngredientRevisionScope((IngredientScopeType)identity.ScopeType, identity.ScopeId))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IngredientRevisionMutationResult> SaveDraftAsync(
        Guid revisionId,
        IngredientRevisionDraftContent content,
        long expectedRowVersion,
        Guid actorUserId,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!await ReferencesExistAsync(content.CategoryId, content.BaseUnitId, cancellationToken))
            return new(IngredientRevisionMutationStatus.Invalid);

        int affected = await database.Set<IngredientRevisionRecord>()
            .Where(value => value.Id == revisionId &&
                            value.State == (int)IngredientRevisionState.Draft &&
                            value.RowVersion == expectedRowVersion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Name, content.Name)
                .SetProperty(value => value.NormalizedName, content.NormalizedName)
                .SetProperty(value => value.CategoryId, content.CategoryId)
                .SetProperty(value => value.BaseUnitId, content.BaseUnitId)
                .SetProperty(value => value.AllergenReviewState, (int)content.AllergenReviewState)
                .SetProperty(value => value.IntoleranceReviewState, (int)content.IntoleranceReviewState)
                .SetProperty(value => value.OriginReviewState, (int)content.OriginReviewState)
                .SetProperty(value => value.UpdatedAtUtc, changedAtUtc)
                .SetProperty(value => value.UpdatedBy, actorUserId)
                .SetProperty(value => value.RowVersion, value => value.RowVersion + 1),
                cancellationToken);

        return affected == 1
            ? new(IngredientRevisionMutationStatus.Saved, expectedRowVersion + 1)
            : await DetermineFailureAsync(revisionId, expectedRowVersion, cancellationToken);
    }

    public async Task<IngredientRevisionMutationResult> PublishAsync(
        Guid revisionId,
        long expectedRowVersion,
        Guid actorUserId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        IngredientRevisionRecord? revision = await database.Set<IngredientRevisionRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == revisionId, cancellationToken);
        if (revision is null)
            return new(IngredientRevisionMutationStatus.NotFound);
        if (revision.State != (int)IngredientRevisionState.Draft)
            return new(IngredientRevisionMutationStatus.NotDraft, revision.RowVersion);
        if (revision.RowVersion != expectedRowVersion)
            return new(IngredientRevisionMutationStatus.ConcurrencyConflict, revision.RowVersion);

        try
        {
            await ValidatePublicationAsync(revision, cancellationToken);
        }
        catch (ArgumentException)
        {
            return new(IngredientRevisionMutationStatus.Invalid, revision.RowVersion);
        }
        catch (InvalidOperationException)
        {
            return new(IngredientRevisionMutationStatus.Invalid, revision.RowVersion);
        }

        int revisionUpdates = await database.Set<IngredientRevisionRecord>()
            .Where(value => value.Id == revisionId &&
                            value.State == (int)IngredientRevisionState.Draft &&
                            value.RowVersion == expectedRowVersion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.State, (int)IngredientRevisionState.Published)
                .SetProperty(value => value.PublishedAtUtc, publishedAtUtc)
                .SetProperty(value => value.PublishedBy, actorUserId)
                .SetProperty(value => value.UpdatedAtUtc, publishedAtUtc)
                .SetProperty(value => value.UpdatedBy, actorUserId)
                .SetProperty(value => value.RowVersion, value => value.RowVersion + 1),
                cancellationToken);
        if (revisionUpdates != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(IngredientRevisionMutationStatus.ConcurrencyConflict);
        }

        int identityUpdates = await database.Set<IngredientIdentityRecord>()
            .Where(value => value.Id == revision.IngredientId &&
                            value.Status == (int)IngredientIdentityStatus.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.CurrentPublishedRevisionId, revisionId),
                cancellationToken);
        if (identityUpdates != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(IngredientRevisionMutationStatus.Invalid, expectedRowVersion);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(IngredientRevisionMutationStatus.Published, expectedRowVersion + 1);
    }

    private async Task ValidatePublicationAsync(
        IngredientRevisionRecord revision,
        CancellationToken cancellationToken)
    {
        IngredientPropertyDefinition[] definitions = await database
            .Set<IngredientAllergenDefinitionRecord>()
            .AsNoTracking()
            .Select(value => new IngredientPropertyDefinition(value.Id, value.Code, value.ParentAllergenId))
            .ToArrayAsync(cancellationToken);
        IngredientPropertyValue[] allergens = await database.Set<IngredientRevisionAllergenRecord>()
            .AsNoTracking()
            .Where(value => value.IngredientRevisionId == revision.Id)
            .Select(value => new IngredientPropertyValue(
                value.AllergenId,
                (IngredientPropertyState)value.State,
                (IngredientPropertySource)value.Source))
            .ToArrayAsync(cancellationToken);
        IngredientVariantRevisionRecord[] variants = await database.Set<IngredientVariantRevisionRecord>()
            .AsNoTracking()
            .Where(value => value.IngredientRevisionId == revision.Id)
            .ToArrayAsync(cancellationToken);
        Guid[] variantIds = variants.Select(value => value.Id).ToArray();
        IngredientVariantAllergenOverrideRecord[] overrides = await database
            .Set<IngredientVariantAllergenOverrideRecord>()
            .AsNoTracking()
            .Where(value => variantIds.Contains(value.VariantRevisionId))
            .ToArrayAsync(cancellationToken);

        var snapshot = new IngredientRevisionPublicationSnapshot(
            (IngredientRevisionState)revision.State,
            (IngredientPropertyReviewState)revision.AllergenReviewState,
            (IngredientPropertyReviewState)revision.IntoleranceReviewState,
            (IngredientPropertyReviewState)revision.OriginReviewState,
            allergens,
            variants.Select(variant => new IngredientVariantPublicationSnapshot(
                variant.VariantKey,
                overrides.Where(value => value.VariantRevisionId == variant.Id)
                    .Select(value => new IngredientPropertyValue(
                        value.AllergenId,
                        (IngredientPropertyState)value.State,
                        (IngredientPropertySource)value.Source))
                    .ToArray()))
                .ToArray());

        new IngredientRevisionPublicationValidator(definitions).Validate(snapshot);
    }

    private async Task<bool> ReferencesExistAsync(
        Guid categoryId,
        Guid baseUnitId,
        CancellationToken cancellationToken) =>
        await database.Set<IngredientCategoryRecord>().AsNoTracking()
            .AnyAsync(value => value.Id == categoryId, cancellationToken) &&
        await database.MeasurementUnits.AsNoTracking()
            .AnyAsync(value => value.Id == baseUnitId, cancellationToken);

    private async Task<IngredientRevisionMutationResult> DetermineFailureAsync(
        Guid revisionId,
        long expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var current = await database.Set<IngredientRevisionRecord>()
            .AsNoTracking()
            .Where(value => value.Id == revisionId)
            .Select(value => new { value.State, value.RowVersion })
            .SingleOrDefaultAsync(cancellationToken);
        if (current is null)
            return new(IngredientRevisionMutationStatus.NotFound);
        if (current.State != (int)IngredientRevisionState.Draft)
            return new(IngredientRevisionMutationStatus.NotDraft, current.RowVersion);
        return new(
            current.RowVersion == expectedRowVersion
                ? IngredientRevisionMutationStatus.Invalid
                : IngredientRevisionMutationStatus.ConcurrencyConflict,
            current.RowVersion);
    }
}
