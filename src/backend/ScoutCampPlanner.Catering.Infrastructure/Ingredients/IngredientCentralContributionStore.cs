using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

public sealed class IngredientCentralContributionStore(CateringDbContext database)
    : IIngredientCentralContributionStore
{
    public Task<IngredientRevisionScope?> GetActiveScopeAsync(
        Guid revisionId, CancellationToken cancellationToken = default) =>
        (from revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
         join identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
             on revision.IngredientId equals identity.Id
         where revision.Id == revisionId && identity.Status == (int)IngredientIdentityStatus.Active
         select new IngredientRevisionScope((IngredientScopeType)identity.ScopeType, identity.ScopeId))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CentralIngredientCandidate>> FindCentralCandidatesAsync(
        Guid localRevisionId, CancellationToken cancellationToken = default)
    {
        var local = await (
            from revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
            join identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
                on revision.IngredientId equals identity.Id
            where revision.Id == localRevisionId && identity.Status == (int)IngredientIdentityStatus.Active &&
                  (identity.ScopeType == (int)IngredientScopeType.Tenant ||
                   identity.ScopeType == (int)IngredientScopeType.Camp)
            select new { revision.NormalizedName, revision.CategoryId }).SingleOrDefaultAsync(cancellationToken);
        if (local is null) return [];

        return await (
            from identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
            join revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
                on identity.CurrentPublishedRevisionId equals revision.Id
            where identity.ScopeType == (int)IngredientScopeType.Central &&
                  identity.Status == (int)IngredientIdentityStatus.Active &&
                  revision.NormalizedName == local.NormalizedName
            orderby revision.CategoryId == local.CategoryId descending, revision.Name
            select new CentralIngredientCandidate(identity.Id, revision.Id, revision.Name,
                revision.CategoryId, revision.BaseUnitId)).ToArrayAsync(cancellationToken);
    }

    public async Task<IngredientContributionMutationResult> SubmitAsync(
        Guid contributionId, Guid localRevisionId, Guid actorUserId, DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var source = await (
            from revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
            join identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
                on revision.IngredientId equals identity.Id
            where revision.Id == localRevisionId && identity.Status == (int)IngredientIdentityStatus.Active
            select new { Revision = revision, Identity = identity }).SingleOrDefaultAsync(cancellationToken);
        if (source is null) return new(IngredientContributionMutationStatus.NotFound);
        if (source.Identity.ScopeType is not ((int)IngredientScopeType.Tenant) and not ((int)IngredientScopeType.Camp))
            return new(IngredientContributionMutationStatus.Invalid);
        if (source.Revision.State != (int)IngredientRevisionState.Published ||
            source.Identity.CurrentPublishedRevisionId != localRevisionId)
            return new(IngredientContributionMutationStatus.NotPublished);
        if (await database.Set<IngredientCentralContributionRecord>().AsNoTracking()
                .AnyAsync(value => value.SubmittedLocalRevisionId == localRevisionId, cancellationToken))
            return new(IngredientContributionMutationStatus.AlreadySubmitted);

        database.Add(new IngredientCentralContributionRecord
        {
            Id = contributionId,
            SubmittedLocalRevisionId = localRevisionId,
            SuggestedCentralIngredientId = source.Identity.SourceIngredientId,
            Status = (int)IngredientCentralContributionStatus.Pending,
            SubmittedAtUtc = submittedAtUtc,
            SubmittedBy = actorUserId,
        });
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return new(IngredientContributionMutationStatus.Created, contributionId);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            return new(IngredientContributionMutationStatus.AlreadySubmitted);
        }
    }

    public async Task<IReadOnlyList<IngredientCentralContributionSummary>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from contribution in database.Set<IngredientCentralContributionRecord>().AsNoTracking()
            join revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
                on contribution.SubmittedLocalRevisionId equals revision.Id
            join identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
                on revision.IngredientId equals identity.Id
            where contribution.Status == (int)IngredientCentralContributionStatus.Pending
            select new { contribution, revision, identity }).ToArrayAsync(cancellationToken);
        rows = rows.OrderBy(value => value.contribution.SubmittedAtUtc).ToArray();
        Guid[] suggestedIds = rows.Where(value => value.contribution.SuggestedCentralIngredientId.HasValue)
            .Select(value => value.contribution.SuggestedCentralIngredientId!.Value).Distinct().ToArray();
        Dictionary<Guid, string> names = await (
            from identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
            join revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
                on identity.CurrentPublishedRevisionId equals revision.Id
            where suggestedIds.Contains(identity.Id)
            select new { identity.Id, revision.Name }).ToDictionaryAsync(value => value.Id, value => value.Name,
                cancellationToken);
        return rows.Select(value => new IngredientCentralContributionSummary(
            value.contribution.Id, value.revision.Id, value.identity.Id,
            (IngredientScopeType)value.identity.ScopeType, value.identity.ScopeId!.Value,
            value.revision.Name, value.revision.CategoryId, value.revision.BaseUnitId,
            value.contribution.SuggestedCentralIngredientId,
            value.contribution.SuggestedCentralIngredientId is Guid id && names.TryGetValue(id, out string? name)
                ? name : null,
            value.contribution.SubmittedAtUtc, value.contribution.SubmittedBy)).ToArray();
    }

    public async Task<IngredientContributionMutationResult> AcceptAsync(
        Guid contributionId, Guid? targetCentralIngredientId, Guid newCentralIngredientId,
        Guid newCentralRevisionId, Guid actorUserId, DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        IngredientCentralContributionRecord? contribution = await database
            .Set<IngredientCentralContributionRecord>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == contributionId,
                cancellationToken);
        if (contribution is null) return new(IngredientContributionMutationStatus.NotFound);
        if (contribution.Status != (int)IngredientCentralContributionStatus.Pending)
            return new(IngredientContributionMutationStatus.AlreadyReviewed);

        IngredientRevisionRecord? source = await database.Set<IngredientRevisionRecord>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == contribution.SubmittedLocalRevisionId, cancellationToken);
        if (source is null) return new(IngredientContributionMutationStatus.NotFound);

        Guid centralIngredientId;
        int revisionNumber;
        Guid? basedOnRevisionId;
        if (targetCentralIngredientId.HasValue)
        {
            IngredientIdentityRecord? target = await database.Set<IngredientIdentityRecord>().AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == targetCentralIngredientId &&
                    value.ScopeType == (int)IngredientScopeType.Central &&
                    value.Status == (int)IngredientIdentityStatus.Active, cancellationToken);
            if (target is null) return new(IngredientContributionMutationStatus.Invalid);
            if (await database.Set<IngredientRevisionRecord>().AsNoTracking().AnyAsync(value =>
                    value.IngredientId == target.Id && value.State == (int)IngredientRevisionState.Draft,
                    cancellationToken))
                return new(IngredientContributionMutationStatus.DraftAlreadyExists);
            centralIngredientId = target.Id;
            basedOnRevisionId = target.CurrentPublishedRevisionId;
            revisionNumber = await database.Set<IngredientRevisionRecord>()
                .Where(value => value.IngredientId == target.Id)
                .MaxAsync(value => value.RevisionNumber, cancellationToken) + 1;
        }
        else
        {
            centralIngredientId = newCentralIngredientId;
            revisionNumber = 1;
            basedOnRevisionId = null;
            database.Add(new IngredientIdentityRecord
            {
                Id = centralIngredientId,
                ScopeType = (int)IngredientScopeType.Central,
                Status = (int)IngredientIdentityStatus.Active,
            });
        }

        database.Add(new IngredientRevisionRecord
        {
            Id = newCentralRevisionId,
            IngredientId = centralIngredientId,
            RevisionNumber = revisionNumber,
            State = (int)IngredientRevisionState.Draft,
            BasedOnRevisionId = basedOnRevisionId,
            Name = source.Name,
            NormalizedName = source.NormalizedName,
            CategoryId = source.CategoryId,
            BaseUnitId = source.BaseUnitId,
            AllergenReviewState = (int)IngredientPropertyReviewState.Unreviewed,
            IntoleranceReviewState = (int)IngredientPropertyReviewState.Unreviewed,
            OriginReviewState = (int)IngredientPropertyReviewState.Unreviewed,
            RowVersion = 1,
            CreatedAtUtc = reviewedAtUtc,
            CreatedBy = actorUserId,
            UpdatedAtUtc = reviewedAtUtc,
            UpdatedBy = actorUserId,
        });
        await CopyDetailsAsync(source.Id, newCentralRevisionId, cancellationToken);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            int reviewed = await database.Set<IngredientCentralContributionRecord>()
                .Where(value => value.Id == contributionId &&
                    value.Status == (int)IngredientCentralContributionStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.Status, (int)IngredientCentralContributionStatus.Accepted)
                    .SetProperty(value => value.ReviewedAtUtc, reviewedAtUtc)
                    .SetProperty(value => value.ReviewedBy, actorUserId)
                    .SetProperty(value => value.ResultingCentralIngredientId, centralIngredientId)
                    .SetProperty(value => value.ResultingCentralRevisionId, newCentralRevisionId),
                    cancellationToken);
            if (reviewed != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
                return new(IngredientContributionMutationStatus.AlreadyReviewed);
            }
            await transaction.CommitAsync(cancellationToken);
            return new(IngredientContributionMutationStatus.Accepted, contributionId,
                centralIngredientId, newCentralRevisionId);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new(IngredientContributionMutationStatus.Invalid);
        }
    }

    public async Task<IngredientContributionMutationResult> RejectAsync(
        Guid contributionId, Guid actorUserId, DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken = default)
    {
        int affected = await database.Set<IngredientCentralContributionRecord>()
            .Where(value => value.Id == contributionId &&
                value.Status == (int)IngredientCentralContributionStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.Status, (int)IngredientCentralContributionStatus.Rejected)
                .SetProperty(value => value.ReviewedAtUtc, reviewedAtUtc)
                .SetProperty(value => value.ReviewedBy, actorUserId), cancellationToken);
        if (affected == 1) return new(IngredientContributionMutationStatus.Rejected, contributionId);
        return await database.Set<IngredientCentralContributionRecord>().AsNoTracking()
            .AnyAsync(value => value.Id == contributionId, cancellationToken)
            ? new(IngredientContributionMutationStatus.AlreadyReviewed)
            : new(IngredientContributionMutationStatus.NotFound);
    }

    public async Task<IngredientContributionMutationResult> ReplaceAsync(
        Guid localRevisionId, Guid centralRevisionId, Guid actorUserId, DateTimeOffset replacedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var source = await (
            from revision in database.Set<IngredientRevisionRecord>()
            join identity in database.Set<IngredientIdentityRecord>() on revision.IngredientId equals identity.Id
            where revision.Id == localRevisionId && identity.Status == (int)IngredientIdentityStatus.Active
            select new { revision, identity }).SingleOrDefaultAsync(cancellationToken);
        if (source is null) return new(IngredientContributionMutationStatus.NotFound);
        if (source.identity.ScopeType is not ((int)IngredientScopeType.Tenant) and not ((int)IngredientScopeType.Camp))
            return new(IngredientContributionMutationStatus.Invalid);
        IngredientIdentityRecord? central = await (
            from revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
            join identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
                on revision.IngredientId equals identity.Id
            where revision.Id == centralRevisionId && identity.ScopeType == (int)IngredientScopeType.Central &&
                  identity.Status == (int)IngredientIdentityStatus.Active &&
                  identity.CurrentPublishedRevisionId == centralRevisionId
            select identity).SingleOrDefaultAsync(cancellationToken);
        if (central is null) return new(IngredientContributionMutationStatus.Invalid);

        source.identity.Status = (int)IngredientIdentityStatus.Archived;
        source.identity.ReplacedByCentralIngredientId = central.Id;
        source.identity.ReplacedAtUtc = replacedAtUtc;
        source.identity.ReplacedBy = actorUserId;
        await database.SaveChangesAsync(cancellationToken);
        return new(IngredientContributionMutationStatus.Replaced, CentralIngredientId: central.Id,
            CentralRevisionId: centralRevisionId);
    }

    private async Task CopyDetailsAsync(Guid sourceRevisionId, Guid targetRevisionId,
        CancellationToken cancellationToken)
    {
        IngredientRevisionAllergenRecord[] allergens = await database.Set<IngredientRevisionAllergenRecord>()
            .AsNoTracking().Where(value => value.IngredientRevisionId == sourceRevisionId)
            .ToArrayAsync(cancellationToken);
        IngredientRevisionIntoleranceRecord[] intolerances = await database.Set<IngredientRevisionIntoleranceRecord>()
            .AsNoTracking().Where(value => value.IngredientRevisionId == sourceRevisionId)
            .ToArrayAsync(cancellationToken);
        IngredientRevisionOriginRecord[] origins = await database.Set<IngredientRevisionOriginRecord>()
            .AsNoTracking().Where(value => value.IngredientRevisionId == sourceRevisionId)
            .ToArrayAsync(cancellationToken);
        IngredientRevisionUnitConversionRecord[] conversions = await database.Set<IngredientRevisionUnitConversionRecord>()
            .AsNoTracking().Where(value => value.IngredientRevisionId == sourceRevisionId)
            .ToArrayAsync(cancellationToken);
        database.AddRange(allergens.Select(value => new IngredientRevisionAllergenRecord
            { IngredientRevisionId = targetRevisionId, AllergenId = value.AllergenId, State = value.State, Source = value.Source }));
        database.AddRange(intolerances.Select(value => new IngredientRevisionIntoleranceRecord
            { IngredientRevisionId = targetRevisionId, IntoleranceId = value.IntoleranceId, State = value.State, Source = value.Source }));
        database.AddRange(origins.Select(value => new IngredientRevisionOriginRecord
            { IngredientRevisionId = targetRevisionId, OriginPropertyId = value.OriginPropertyId, State = value.State, Source = value.Source }));
        database.AddRange(conversions.Select(value => new IngredientRevisionUnitConversionRecord
            { IngredientRevisionId = targetRevisionId, SourceUnitId = value.SourceUnitId,
              FactorToBaseUnit = value.FactorToBaseUnit, Precision = value.Precision }));

        IngredientVariantRevisionRecord[] variants = await database.Set<IngredientVariantRevisionRecord>()
            .AsNoTracking().Where(value => value.IngredientRevisionId == sourceRevisionId)
            .ToArrayAsync(cancellationToken);
        Guid[] ids = variants.Select(value => value.Id).ToArray();
        var allergenOverrides = await database.Set<IngredientVariantAllergenOverrideRecord>().AsNoTracking()
            .Where(value => ids.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken);
        var intoleranceOverrides = await database.Set<IngredientVariantIntoleranceOverrideRecord>().AsNoTracking()
            .Where(value => ids.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken);
        var originOverrides = await database.Set<IngredientVariantOriginOverrideRecord>().AsNoTracking()
            .Where(value => ids.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken);
        var conversionOverrides = await database.Set<IngredientVariantUnitConversionOverrideRecord>().AsNoTracking()
            .Where(value => ids.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken);
        foreach (IngredientVariantRevisionRecord variant in variants)
        {
            Guid id = Guid.NewGuid();
            database.Add(new IngredientVariantRevisionRecord
            {
                Id = id, IngredientRevisionId = targetRevisionId, VariantKey = variant.VariantKey,
                Name = variant.Name, NormalizedName = variant.NormalizedName, Status = variant.Status,
                SortOrder = variant.SortOrder,
            });
            database.AddRange(allergenOverrides.Where(value => value.VariantRevisionId == variant.Id)
                .Select(value => new IngredientVariantAllergenOverrideRecord
                    { VariantRevisionId = id, AllergenId = value.AllergenId, State = value.State, Source = value.Source }));
            database.AddRange(intoleranceOverrides.Where(value => value.VariantRevisionId == variant.Id)
                .Select(value => new IngredientVariantIntoleranceOverrideRecord
                    { VariantRevisionId = id, IntoleranceId = value.IntoleranceId, State = value.State, Source = value.Source }));
            database.AddRange(originOverrides.Where(value => value.VariantRevisionId == variant.Id)
                .Select(value => new IngredientVariantOriginOverrideRecord
                    { VariantRevisionId = id, OriginPropertyId = value.OriginPropertyId, State = value.State, Source = value.Source }));
            database.AddRange(conversionOverrides.Where(value => value.VariantRevisionId == variant.Id)
                .Select(value => new IngredientVariantUnitConversionOverrideRecord
                    { VariantRevisionId = id, SourceUnitId = value.SourceUnitId,
                      FactorToBaseUnit = value.FactorToBaseUnit, Precision = value.Precision }));
        }
    }
}
