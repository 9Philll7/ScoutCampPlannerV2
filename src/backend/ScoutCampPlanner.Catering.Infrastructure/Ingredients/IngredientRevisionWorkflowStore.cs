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

    public async Task<IngredientRevisionDraftDetails?> GetAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default)
    {
        var header = await (
            from revisionRecord in database.Set<IngredientRevisionRecord>().AsNoTracking()
            join identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
                on revisionRecord.IngredientId equals identity.Id
            where revisionRecord.Id == revisionId && identity.Status == (int)IngredientIdentityStatus.Active
            select new
            {
                Revision = revisionRecord,
                ScopeType = (IngredientScopeType)identity.ScopeType,
                identity.ScopeId,
            }).SingleOrDefaultAsync(cancellationToken);
        if (header is null)
            return null;

        IngredientRevisionPropertyItem[] allergens = await database.Set<IngredientRevisionAllergenRecord>()
            .AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId)
            .OrderBy(value => value.AllergenId)
            .Select(value => PropertyItem(value.AllergenId, value.State, value.Source))
            .ToArrayAsync(cancellationToken);
        IngredientRevisionPropertyItem[] intolerances = await database.Set<IngredientRevisionIntoleranceRecord>()
            .AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId)
            .OrderBy(value => value.IntoleranceId)
            .Select(value => PropertyItem(value.IntoleranceId, value.State, value.Source))
            .ToArrayAsync(cancellationToken);
        IngredientRevisionPropertyItem[] origins = await database.Set<IngredientRevisionOriginRecord>()
            .AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId)
            .OrderBy(value => value.OriginPropertyId)
            .Select(value => PropertyItem(value.OriginPropertyId, value.State, value.Source))
            .ToArrayAsync(cancellationToken);
        IngredientRevisionUnitConversionItem[] conversions = await database
            .Set<IngredientRevisionUnitConversionRecord>()
            .AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId)
            .OrderBy(value => value.SourceUnitId)
            .Select(value => ConversionItem(value.SourceUnitId, value.FactorToBaseUnit, value.Precision))
            .ToArrayAsync(cancellationToken);
        IngredientVariantRevisionRecord[] variantRecords = await database.Set<IngredientVariantRevisionRecord>()
            .AsNoTracking()
            .Where(value => value.IngredientRevisionId == revisionId)
            .OrderBy(value => value.SortOrder)
            .ThenBy(value => value.NormalizedName)
            .ToArrayAsync(cancellationToken);
        Guid[] variantIds = variantRecords.Select(value => value.Id).ToArray();
        IngredientVariantAllergenOverrideRecord[] allergenOverrides = await database
            .Set<IngredientVariantAllergenOverrideRecord>().AsNoTracking()
            .Where(value => variantIds.Contains(value.VariantRevisionId))
            .ToArrayAsync(cancellationToken);
        IngredientVariantIntoleranceOverrideRecord[] intoleranceOverrides = await database
            .Set<IngredientVariantIntoleranceOverrideRecord>().AsNoTracking()
            .Where(value => variantIds.Contains(value.VariantRevisionId))
            .ToArrayAsync(cancellationToken);
        IngredientVariantOriginOverrideRecord[] originOverrides = await database
            .Set<IngredientVariantOriginOverrideRecord>().AsNoTracking()
            .Where(value => variantIds.Contains(value.VariantRevisionId))
            .ToArrayAsync(cancellationToken);
        IngredientVariantUnitConversionOverrideRecord[] conversionOverrides = await database
            .Set<IngredientVariantUnitConversionOverrideRecord>().AsNoTracking()
            .Where(value => variantIds.Contains(value.VariantRevisionId))
            .ToArrayAsync(cancellationToken);

        IngredientRevisionRecord revision = header.Revision;
        return new IngredientRevisionDraftDetails(
            revision.Id,
            revision.IngredientId,
            header.ScopeType,
            header.ScopeId,
            revision.RevisionNumber,
            (IngredientRevisionState)revision.State,
            revision.BasedOnRevisionId,
            revision.Name,
            revision.CategoryId,
            revision.BaseUnitId,
            (IngredientPropertyReviewState)revision.AllergenReviewState,
            (IngredientPropertyReviewState)revision.IntoleranceReviewState,
            (IngredientPropertyReviewState)revision.OriginReviewState,
            revision.RowVersion,
            allergens,
            intolerances,
            origins,
            conversions,
            variantRecords.Select(variant => new IngredientVariantRevisionItem(
                variant.Id,
                variant.VariantKey,
                variant.Name,
                variant.Status == 0,
                variant.SortOrder,
                allergenOverrides.Where(value => value.VariantRevisionId == variant.Id)
                    .Select(value => PropertyItem(value.AllergenId, value.State, value.Source)).ToArray(),
                intoleranceOverrides.Where(value => value.VariantRevisionId == variant.Id)
                    .Select(value => PropertyItem(value.IntoleranceId, value.State, value.Source)).ToArray(),
                originOverrides.Where(value => value.VariantRevisionId == variant.Id)
                    .Select(value => PropertyItem(value.OriginPropertyId, value.State, value.Source)).ToArray(),
                conversionOverrides.Where(value => value.VariantRevisionId == variant.Id)
                    .Select(value => ConversionItem(value.SourceUnitId, value.FactorToBaseUnit, value.Precision))
                    .ToArray()))
                .ToArray());
    }

    public async Task<IReadOnlyList<IngredientRevisionSummary>> ListAsync(
        IngredientRevisionScope scope,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidScope(scope))
            return [];

        var candidates = await (
            from identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
            join revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
                on identity.Id equals revision.IngredientId
            where identity.ScopeType == (int)scope.ScopeType &&
                  identity.ScopeId == scope.ScopeId &&
                  identity.Status == (int)IngredientIdentityStatus.Active &&
                  (revision.State == (int)IngredientRevisionState.Draft ||
                   revision.Id == identity.CurrentPublishedRevisionId)
            select new
            {
                IngredientId = identity.Id,
                RevisionId = revision.Id,
                revision.Name,
                revision.NormalizedName,
                revision.State,
                revision.RowVersion,
            })
            .ToArrayAsync(cancellationToken);

        return candidates
            .GroupBy(value => value.IngredientId)
            .Select(group => group.OrderBy(value => value.State).First())
            .OrderBy(value => value.NormalizedName)
            .Select(value => new IngredientRevisionSummary(
                value.IngredientId,
                value.RevisionId,
                value.Name,
                (IngredientRevisionState)value.State,
                value.RowVersion))
            .ToArray();
    }

    public async Task<IngredientRevisionMutationResult> CreateDraftAsync(
        Guid ingredientId,
        Guid revisionId,
        IngredientRevisionScope scope,
        IngredientRevisionDraftContent content,
        Guid actorUserId,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (ingredientId == Guid.Empty || revisionId == Guid.Empty || actorUserId == Guid.Empty ||
            !IsValidScope(scope) ||
            !await ReferencesExistAsync(content, cancellationToken))
            return new(IngredientRevisionMutationStatus.Invalid);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.Add(new IngredientIdentityRecord
        {
            Id = ingredientId,
            ScopeType = (int)scope.ScopeType,
            ScopeId = scope.ScopeId,
            Status = (int)IngredientIdentityStatus.Active,
        });
        database.Add(new IngredientRevisionRecord
        {
            Id = revisionId,
            IngredientId = ingredientId,
            RevisionNumber = 1,
            State = (int)IngredientRevisionState.Draft,
            Name = content.Name,
            NormalizedName = content.NormalizedName,
            CategoryId = content.CategoryId,
            BaseUnitId = content.BaseUnitId,
            AllergenReviewState = (int)content.AllergenReviewState,
            IntoleranceReviewState = (int)content.IntoleranceReviewState,
            OriginReviewState = (int)content.OriginReviewState,
            RowVersion = 1,
            CreatedAtUtc = createdAtUtc,
            CreatedBy = actorUserId,
            UpdatedAtUtc = createdAtUtc,
            UpdatedBy = actorUserId,
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new(IngredientRevisionMutationStatus.Invalid);
        }

        return new(
            IngredientRevisionMutationStatus.Created,
            1,
            ingredientId,
            revisionId);
    }

    public async Task<IngredientRevisionMutationResult> CreateDraftFromPublishedAsync(
        Guid publishedRevisionId,
        Guid newRevisionId,
        Guid actorUserId,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (publishedRevisionId == Guid.Empty || newRevisionId == Guid.Empty || actorUserId == Guid.Empty)
            return new(IngredientRevisionMutationStatus.Invalid);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var source = await (
            from revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
            join identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
                on revision.IngredientId equals identity.Id
            where revision.Id == publishedRevisionId &&
                  revision.State == (int)IngredientRevisionState.Published &&
                  identity.CurrentPublishedRevisionId == publishedRevisionId &&
                  identity.Status == (int)IngredientIdentityStatus.Active
            select new { Revision = revision, Identity = identity })
            .SingleOrDefaultAsync(cancellationToken);
        if (source is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            IngredientRevisionRecord? existing = await database.Set<IngredientRevisionRecord>()
                .AsNoTracking().SingleOrDefaultAsync(value => value.Id == publishedRevisionId, cancellationToken);
            return existing is null
                ? new(IngredientRevisionMutationStatus.NotFound)
                : new(IngredientRevisionMutationStatus.NotDraft, existing.RowVersion);
        }

        IngredientRevisionRecord? existingDraft = await database.Set<IngredientRevisionRecord>()
            .AsNoTracking()
            .Where(value => value.IngredientId == source.Identity.Id &&
                            value.State == (int)IngredientRevisionState.Draft)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingDraft is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(
                IngredientRevisionMutationStatus.DraftAlreadyExists,
                existingDraft.RowVersion,
                source.Identity.Id,
                existingDraft.Id);
        }

        int revisionNumber = await database.Set<IngredientRevisionRecord>()
            .Where(value => value.IngredientId == source.Identity.Id)
            .MaxAsync(value => value.RevisionNumber, cancellationToken) + 1;
        IngredientRevisionRecord published = source.Revision;
        database.Add(new IngredientRevisionRecord
        {
            Id = newRevisionId,
            IngredientId = published.IngredientId,
            RevisionNumber = revisionNumber,
            State = (int)IngredientRevisionState.Draft,
            BasedOnRevisionId = published.Id,
            MergedCentralRevisionId = published.MergedCentralRevisionId,
            Name = published.Name,
            NormalizedName = published.NormalizedName,
            CategoryId = published.CategoryId,
            BaseUnitId = published.BaseUnitId,
            AllergenReviewState = published.AllergenReviewState,
            IntoleranceReviewState = published.IntoleranceReviewState,
            OriginReviewState = published.OriginReviewState,
            RowVersion = 1,
            CreatedAtUtc = createdAtUtc,
            CreatedBy = actorUserId,
            UpdatedAtUtc = createdAtUtc,
            UpdatedBy = actorUserId,
        });

        IngredientRevisionAllergenRecord[] allergens = await database
            .Set<IngredientRevisionAllergenRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == published.Id).ToArrayAsync(cancellationToken);
        IngredientRevisionIntoleranceRecord[] intolerances = await database
            .Set<IngredientRevisionIntoleranceRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == published.Id).ToArrayAsync(cancellationToken);
        IngredientRevisionOriginRecord[] origins = await database
            .Set<IngredientRevisionOriginRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == published.Id).ToArrayAsync(cancellationToken);
        IngredientRevisionUnitConversionRecord[] conversions = await database
            .Set<IngredientRevisionUnitConversionRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == published.Id).ToArrayAsync(cancellationToken);
        database.AddRange(allergens.Select(value => new IngredientRevisionAllergenRecord
        {
            IngredientRevisionId = newRevisionId,
            AllergenId = value.AllergenId,
            State = value.State,
            Source = value.Source,
        }));
        database.AddRange(intolerances.Select(value => new IngredientRevisionIntoleranceRecord
        {
            IngredientRevisionId = newRevisionId,
            IntoleranceId = value.IntoleranceId,
            State = value.State,
            Source = value.Source,
        }));
        database.AddRange(origins.Select(value => new IngredientRevisionOriginRecord
        {
            IngredientRevisionId = newRevisionId,
            OriginPropertyId = value.OriginPropertyId,
            State = value.State,
            Source = value.Source,
        }));
        database.AddRange(conversions.Select(value => new IngredientRevisionUnitConversionRecord
        {
            IngredientRevisionId = newRevisionId,
            SourceUnitId = value.SourceUnitId,
            FactorToBaseUnit = value.FactorToBaseUnit,
            Precision = value.Precision,
        }));

        IngredientVariantRevisionRecord[] variants = await database.Set<IngredientVariantRevisionRecord>()
            .AsNoTracking().Where(value => value.IngredientRevisionId == published.Id)
            .ToArrayAsync(cancellationToken);
        Guid[] sourceVariantIds = variants.Select(value => value.Id).ToArray();
        IngredientVariantAllergenOverrideRecord[] allergenOverrides = await database
            .Set<IngredientVariantAllergenOverrideRecord>().AsNoTracking()
            .Where(value => sourceVariantIds.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken);
        IngredientVariantIntoleranceOverrideRecord[] intoleranceOverrides = await database
            .Set<IngredientVariantIntoleranceOverrideRecord>().AsNoTracking()
            .Where(value => sourceVariantIds.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken);
        IngredientVariantOriginOverrideRecord[] originOverrides = await database
            .Set<IngredientVariantOriginOverrideRecord>().AsNoTracking()
            .Where(value => sourceVariantIds.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken);
        IngredientVariantUnitConversionOverrideRecord[] conversionOverrides = await database
            .Set<IngredientVariantUnitConversionOverrideRecord>().AsNoTracking()
            .Where(value => sourceVariantIds.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken);
        foreach (IngredientVariantRevisionRecord variant in variants)
        {
            Guid variantId = Guid.NewGuid();
            database.Add(new IngredientVariantRevisionRecord
            {
                Id = variantId,
                IngredientRevisionId = newRevisionId,
                VariantKey = variant.VariantKey,
                Name = variant.Name,
                NormalizedName = variant.NormalizedName,
                Status = variant.Status,
                SortOrder = variant.SortOrder,
            });
            database.AddRange(allergenOverrides.Where(value => value.VariantRevisionId == variant.Id)
                .Select(value => new IngredientVariantAllergenOverrideRecord
                {
                    VariantRevisionId = variantId,
                    AllergenId = value.AllergenId,
                    State = value.State,
                    Source = value.Source,
                }));
            database.AddRange(intoleranceOverrides.Where(value => value.VariantRevisionId == variant.Id)
                .Select(value => new IngredientVariantIntoleranceOverrideRecord
                {
                    VariantRevisionId = variantId,
                    IntoleranceId = value.IntoleranceId,
                    State = value.State,
                    Source = value.Source,
                }));
            database.AddRange(originOverrides.Where(value => value.VariantRevisionId == variant.Id)
                .Select(value => new IngredientVariantOriginOverrideRecord
                {
                    VariantRevisionId = variantId,
                    OriginPropertyId = value.OriginPropertyId,
                    State = value.State,
                    Source = value.Source,
                }));
            database.AddRange(conversionOverrides.Where(value => value.VariantRevisionId == variant.Id)
                .Select(value => new IngredientVariantUnitConversionOverrideRecord
                {
                    VariantRevisionId = variantId,
                    SourceUnitId = value.SourceUnitId,
                    FactorToBaseUnit = value.FactorToBaseUnit,
                    Precision = value.Precision,
                }));
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(IngredientRevisionMutationStatus.Created, 1, published.IngredientId, newRevisionId);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new(IngredientRevisionMutationStatus.Invalid);
        }
    }

    public async Task<IngredientRevisionMutationResult> SaveDraftAsync(
        Guid revisionId,
        IngredientRevisionDraftContent content,
        long expectedRowVersion,
        Guid actorUserId,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!await ReferencesExistAsync(content, cancellationToken))
            return new(IngredientRevisionMutationStatus.Invalid);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
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

        if (affected != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await DetermineFailureAsync(revisionId, expectedRowVersion, cancellationToken);
        }

        await database.Set<IngredientRevisionAllergenRecord>()
            .Where(value => value.IngredientRevisionId == revisionId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<IngredientRevisionIntoleranceRecord>()
            .Where(value => value.IngredientRevisionId == revisionId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<IngredientRevisionOriginRecord>()
            .Where(value => value.IngredientRevisionId == revisionId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<IngredientRevisionUnitConversionRecord>()
            .Where(value => value.IngredientRevisionId == revisionId)
            .ExecuteDeleteAsync(cancellationToken);

        DetachTrackedRevisionDetails(revisionId);

        database.AddRange(content.Allergens.Select(value => new IngredientRevisionAllergenRecord
        {
            IngredientRevisionId = revisionId,
            AllergenId = value.PropertyId,
            State = (int)value.State,
            Source = (int)value.Source,
        }));
        database.AddRange(content.Intolerances.Select(value => new IngredientRevisionIntoleranceRecord
        {
            IngredientRevisionId = revisionId,
            IntoleranceId = value.PropertyId,
            State = (int)value.State,
            Source = (int)value.Source,
        }));
        database.AddRange(content.Origins.Select(value => new IngredientRevisionOriginRecord
        {
            IngredientRevisionId = revisionId,
            OriginPropertyId = value.PropertyId,
            State = (int)value.State,
            Source = (int)value.Source,
        }));
        database.AddRange(content.UnitConversions.Select(value => new IngredientRevisionUnitConversionRecord
        {
            IngredientRevisionId = revisionId,
            SourceUnitId = value.SourceUnitId,
            FactorToBaseUnit = value.FactorToBaseUnit,
            Precision = (int)value.Precision,
        }));
        if (content.Variants is not null &&
            !await SynchronizeVariantsAsync(revisionId, content.Variants, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new(IngredientRevisionMutationStatus.Invalid, expectedRowVersion);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(IngredientRevisionMutationStatus.Saved, expectedRowVersion + 1);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new(IngredientRevisionMutationStatus.Invalid, expectedRowVersion);
        }
    }

    private async Task<bool> SynchronizeVariantsAsync(
        Guid revisionId,
        IReadOnlyList<IngredientVariantDraftContent> variants,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, IngredientVariantRevisionRecord> existing = await database
            .Set<IngredientVariantRevisionRecord>()
            .Where(value => value.IngredientRevisionId == revisionId)
            .ToDictionaryAsync(value => value.Id, cancellationToken);

        foreach (IngredientVariantDraftContent variant in variants)
        {
            if (existing.Remove(variant.Id, out IngredientVariantRevisionRecord? record))
            {
                if (!string.Equals(record.VariantKey, variant.VariantKey, StringComparison.Ordinal))
                    return false;
                record.Name = variant.Name;
                record.NormalizedName = variant.NormalizedName;
                record.Status = variant.IsActive ? 0 : 1;
                record.SortOrder = variant.SortOrder;
                continue;
            }

            database.Add(new IngredientVariantRevisionRecord
            {
                Id = variant.Id,
                IngredientRevisionId = revisionId,
                VariantKey = variant.VariantKey,
                Name = variant.Name,
                NormalizedName = variant.NormalizedName,
                Status = variant.IsActive ? 0 : 1,
                SortOrder = variant.SortOrder,
            });
        }

        Guid[] variantIds = variants.Select(value => value.Id).ToArray();
        if (variantIds.Length > 0)
        {
            await database.Set<IngredientVariantAllergenOverrideRecord>()
                .Where(value => variantIds.Contains(value.VariantRevisionId))
                .ExecuteDeleteAsync(cancellationToken);
            await database.Set<IngredientVariantIntoleranceOverrideRecord>()
                .Where(value => variantIds.Contains(value.VariantRevisionId))
                .ExecuteDeleteAsync(cancellationToken);
            await database.Set<IngredientVariantOriginOverrideRecord>()
                .Where(value => variantIds.Contains(value.VariantRevisionId))
                .ExecuteDeleteAsync(cancellationToken);
            await database.Set<IngredientVariantUnitConversionOverrideRecord>()
                .Where(value => variantIds.Contains(value.VariantRevisionId))
                .ExecuteDeleteAsync(cancellationToken);

            DetachTrackedOverrides(variantIds);

            database.AddRange(variants.SelectMany(variant => variant.AllergenOverrides.Select(value =>
                new IngredientVariantAllergenOverrideRecord
                {
                    VariantRevisionId = variant.Id,
                    AllergenId = value.PropertyId,
                    State = (int)value.State,
                    Source = (int)value.Source,
                })));
            database.AddRange(variants.SelectMany(variant => variant.IntoleranceOverrides.Select(value =>
                new IngredientVariantIntoleranceOverrideRecord
                {
                    VariantRevisionId = variant.Id,
                    IntoleranceId = value.PropertyId,
                    State = (int)value.State,
                    Source = (int)value.Source,
                })));
            database.AddRange(variants.SelectMany(variant => variant.OriginOverrides.Select(value =>
                new IngredientVariantOriginOverrideRecord
                {
                    VariantRevisionId = variant.Id,
                    OriginPropertyId = value.PropertyId,
                    State = (int)value.State,
                    Source = (int)value.Source,
                })));
            database.AddRange(variants.SelectMany(variant => variant.UnitConversionOverrides.Select(value =>
                new IngredientVariantUnitConversionOverrideRecord
                {
                    VariantRevisionId = variant.Id,
                    SourceUnitId = value.SourceUnitId,
                    FactorToBaseUnit = value.FactorToBaseUnit,
                    Precision = (int)value.Precision,
                })));
        }

        database.RemoveRange(existing.Values);
        return true;
    }

    private void DetachTrackedOverrides(IReadOnlyCollection<Guid> variantIds)
    {
        foreach (var entry in database.ChangeTracker.Entries<IngredientVariantAllergenOverrideRecord>()
                     .Where(value => variantIds.Contains(value.Entity.VariantRevisionId)))
            entry.State = EntityState.Detached;
        foreach (var entry in database.ChangeTracker.Entries<IngredientVariantIntoleranceOverrideRecord>()
                     .Where(value => variantIds.Contains(value.Entity.VariantRevisionId)))
            entry.State = EntityState.Detached;
        foreach (var entry in database.ChangeTracker.Entries<IngredientVariantOriginOverrideRecord>()
                     .Where(value => variantIds.Contains(value.Entity.VariantRevisionId)))
            entry.State = EntityState.Detached;
        foreach (var entry in database.ChangeTracker.Entries<IngredientVariantUnitConversionOverrideRecord>()
                     .Where(value => variantIds.Contains(value.Entity.VariantRevisionId)))
            entry.State = EntityState.Detached;
    }

    private void DetachTrackedRevisionDetails(Guid revisionId)
    {
        foreach (var entry in database.ChangeTracker.Entries<IngredientRevisionAllergenRecord>()
                     .Where(value => value.Entity.IngredientRevisionId == revisionId))
            entry.State = EntityState.Detached;
        foreach (var entry in database.ChangeTracker.Entries<IngredientRevisionIntoleranceRecord>()
                     .Where(value => value.Entity.IngredientRevisionId == revisionId))
            entry.State = EntityState.Detached;
        foreach (var entry in database.ChangeTracker.Entries<IngredientRevisionOriginRecord>()
                     .Where(value => value.Entity.IngredientRevisionId == revisionId))
            entry.State = EntityState.Detached;
        foreach (var entry in database.ChangeTracker.Entries<IngredientRevisionUnitConversionRecord>()
                     .Where(value => value.Entity.IngredientRevisionId == revisionId))
            entry.State = EntityState.Detached;
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
        IngredientRevisionDraftContent content,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IngredientVariantDraftContent> variants = content.Variants ?? [];
        IngredientRevisionUnitConversion[] allConversions = content.UnitConversions
            .Concat(variants.SelectMany(value => value.UnitConversionOverrides))
            .ToArray();
        IngredientPropertyValue[] allAllergens = content.Allergens
            .Concat(variants.SelectMany(value => value.AllergenOverrides))
            .ToArray();
        IngredientPropertyValue[] allIntolerances = content.Intolerances
            .Concat(variants.SelectMany(value => value.IntoleranceOverrides))
            .ToArray();
        IngredientPropertyValue[] allOrigins = content.Origins
            .Concat(variants.SelectMany(value => value.OriginOverrides))
            .ToArray();

        return await database.Set<IngredientCategoryRecord>().AsNoTracking()
            .AnyAsync(value => value.Id == content.CategoryId, cancellationToken) &&
        await database.MeasurementUnits.AsNoTracking()
            .AnyAsync(value => value.Id == content.BaseUnitId &&
                ((value.Dimension == MeasurementDimension.Mass &&
                    (value.Symbol == "g" || value.Symbol == "kg")) ||
                 (value.Dimension == MeasurementDimension.Volume &&
                    (value.Symbol == "ml" || value.Symbol == "l")) ||
                 (value.Dimension == MeasurementDimension.Count && value.Symbol == "Stk.")),
                cancellationToken) &&
        await AllReferencesExistAsync(allConversions.Select(value => value.SourceUnitId),
            database.MeasurementUnits.AsNoTracking().Select(value => value.Id),
            cancellationToken) &&
        await AllConversionUnitsAllowedAsync(content.BaseUnitId, allConversions, cancellationToken) &&
        await AllReferencesExistAsync(allAllergens.Select(value => value.PropertyId),
            database.Set<IngredientAllergenDefinitionRecord>().Where(value => value.Status == 0).Select(value => value.Id),
            cancellationToken) &&
        await AllReferencesExistAsync(allIntolerances.Select(value => value.PropertyId),
            database.Set<IngredientIntoleranceDefinitionRecord>().Where(value => value.Status == 0).Select(value => value.Id),
            cancellationToken) &&
        await AllReferencesExistAsync(allOrigins.Select(value => value.PropertyId),
            database.Set<IngredientOriginPropertyRecord>().Where(value => value.Status == 0).Select(value => value.Id),
            cancellationToken);
    }

    private static async Task<bool> AllReferencesExistAsync(
        IEnumerable<Guid> requestedIds,
        IQueryable<Guid> availableIds,
        CancellationToken cancellationToken)
    {
        Guid[] requested = requestedIds.Distinct().ToArray();
        if (requested.Length == 0)
            return true;
        return await availableIds.CountAsync(value => requested.Contains(value), cancellationToken) == requested.Length;
    }

    private async Task<bool> AllConversionUnitsAllowedAsync(
        Guid baseUnitId,
        IEnumerable<IngredientRevisionUnitConversion> conversions,
        CancellationToken cancellationToken)
    {
        Guid[] requested = conversions.Select(value => value.SourceUnitId).Distinct().ToArray();
        if (requested.Length == 0)
            return true;
        MeasurementDimension? baseDimension = await database.MeasurementUnits.AsNoTracking()
            .Where(value => value.Id == baseUnitId)
            .Select(value => (MeasurementDimension?)value.Dimension)
            .SingleOrDefaultAsync(cancellationToken);
        if (!baseDimension.HasValue)
            return false;
        return await database.MeasurementUnits.AsNoTracking().CountAsync(
            value => requested.Contains(value.Id) &&
                (value.Symbol == "TL" || value.Symbol == "EL" ||
                 value.Symbol == "Prise" || value.Symbol == "Bund" ||
                 (value.Symbol == "g" || value.Symbol == "kg" || value.Symbol == "ml" ||
                  value.Symbol == "l" || value.Symbol == "Stk.") &&
                    value.Dimension != baseDimension.Value),
            cancellationToken) == requested.Length;
    }

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

    private static IngredientRevisionPropertyItem PropertyItem(Guid id, int state, int source) =>
        new(id, (IngredientPropertyState)state, (IngredientPropertySource)source);

    private static IngredientRevisionUnitConversionItem ConversionItem(Guid id, decimal factor, int precision) =>
        new(id, factor, (IngredientConversionPrecision)precision);

    private static bool IsValidScope(IngredientRevisionScope scope) =>
        scope.ScopeType == IngredientScopeType.Central
            ? scope.ScopeId is null
            : scope.ScopeType is IngredientScopeType.Tenant or IngredientScopeType.Camp &&
              scope.ScopeId.HasValue && scope.ScopeId.Value != Guid.Empty;
}
