using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class RecipeDraftStore(CateringDbContext database) : IRecipeDraftStore
{
    public Task<RecipeDraft?> FindAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
        LoadAsync(recipeId, cancellationToken);

    public async Task<RecipeDraft> CreateAsync(
        RecipeDraft draft,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var record = new RecipeRecord { Id = draft.Id, CreatedBy = actorUserId, CreatedAtUtc = timestampUtc };
        CopyDraft(record, draft, actorUserId, timestampUtc, version: 0);
        database.Set<RecipeRecord>().Add(record);
        AddGraph(draft);
        await database.SaveChangesAsync(cancellationToken);
        draft.SetPersistedVersion(0);
        return draft;
    }

    public async Task<RecipeDraft> CreateDerivedAsync(
        RecipeDraft draft,
        RecipeDraftLineage lineage,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(lineage);
        var record = new RecipeRecord
        {
            Id = draft.Id,
            CreatedBy = actorUserId,
            CreatedAtUtc = timestampUtc,
            DerivedFromRecipeId = lineage.SourceRecipeId,
            DerivedFromRevisionId = lineage.SourceRevisionId,
        };
        CopyDraft(record, draft, actorUserId, timestampUtc, version: 0);
        database.Set<RecipeRecord>().Add(record);
        AddGraph(draft);
        await database.SaveChangesAsync(cancellationToken);
        draft.SetPersistedVersion(0);
        return draft;
    }

    public async Task<RecipeDraftSaveResult> SaveAsync(
        RecipeDraft draft,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        RecipeRecord? record = await database.Set<RecipeRecord>()
            .SingleOrDefaultAsync(value => value.Id == draft.Id, cancellationToken);
        if (record is null)
            return new RecipeDraftSaveResult(RecipeDraftSaveStatus.NotFound, null);
        if (record.DraftVersion != expectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new RecipeDraftSaveResult(
                RecipeDraftSaveStatus.VersionConflict,
                await LoadAsync(draft.Id, cancellationToken));
        }

        CopyDraft(record, draft, actorUserId, timestampUtc, expectedVersion + 1);
        await RemoveGraphAsync(draft.Id, cancellationToken);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            AddGraph(draft);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            draft.SetPersistedVersion(expectedVersion + 1);
            return new RecipeDraftSaveResult(RecipeDraftSaveStatus.Saved, draft);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new RecipeDraftSaveResult(
                RecipeDraftSaveStatus.VersionConflict,
                await LoadAsync(draft.Id, cancellationToken));
        }
    }

    public Task<RecipeLifecycleResult> ArchiveAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(
            recipeId, expectedVersion, actorUserId, timestampUtc, reactivate: false, cancellationToken);

    public Task<RecipeLifecycleResult> ReactivateAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(
            recipeId, expectedVersion, actorUserId, timestampUtc, reactivate: true, cancellationToken);

    public async Task<RecipeLifecycleResult> ResetToDraftAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        RecipeRecord? record = await database.Set<RecipeRecord>()
            .SingleOrDefaultAsync(value => value.Id == recipeId, cancellationToken);
        if (record is null)
            return new RecipeLifecycleResult(RecipeLifecycleStatus.NotFound, null);
        if (record.DraftVersion != expectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new RecipeLifecycleResult(
                RecipeLifecycleStatus.VersionConflict, await LoadAsync(recipeId, cancellationToken));
        }
        if (record.Status != (int)RecipeStatus.Active)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new RecipeLifecycleResult(
                RecipeLifecycleStatus.InvalidStatus, await LoadAsync(recipeId, cancellationToken));
        }

        Guid[] revisionIds = await database.Set<RecipeRevisionRecord>().AsNoTracking()
            .Where(value => value.RecipeId == recipeId).Select(value => value.Id).ToArrayAsync(cancellationToken);
        if (await HasExternalRevisionReferenceAsync(recipeId, revisionIds, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new RecipeLifecycleResult(
                RecipeLifecycleStatus.ReferenceBlocked, await LoadAsync(recipeId, cancellationToken));
        }

        RecipeRevisionRecord[] revisions = await database.Set<RecipeRevisionRecord>()
            .Where(value => value.RecipeId == recipeId).ToArrayAsync(cancellationToken);
        database.RemoveRange(revisions);
        record.Status = (int)RecipeStatus.Draft;
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
            return new RecipeLifecycleResult(
                RecipeLifecycleStatus.VersionConflict, await LoadAsync(recipeId, cancellationToken));
        }
        database.ChangeTracker.Clear();
        return new RecipeLifecycleResult(
            RecipeLifecycleStatus.Changed, await LoadAsync(recipeId, cancellationToken));
    }

    private async Task<bool> HasExternalRevisionReferenceAsync(
        Guid recipeId,
        Guid[] revisionIds,
        CancellationToken cancellationToken)
    {
        if (revisionIds.Length == 0) return false;
        if (await database.Set<RecipeSubrecipePositionRecord>().AsNoTracking()
                .AnyAsync(value => value.RecipeId != recipeId && value.RecipeRevisionId.HasValue &&
                                   revisionIds.Contains(value.RecipeRevisionId.Value), cancellationToken) ||
            await database.Set<RecipeSubrecipeReplacementRecord>().AsNoTracking()
                .AnyAsync(value => value.ReplacementRecipeRevisionId.HasValue &&
                                   revisionIds.Contains(value.ReplacementRecipeRevisionId.Value), cancellationToken) ||
            await database.Set<RecipeRecord>().AsNoTracking()
                .AnyAsync(value => value.Id != recipeId &&
                    ((value.DerivedFromRevisionId.HasValue && revisionIds.Contains(value.DerivedFromRevisionId.Value)) ||
                     (value.CentralSourceRevisionId.HasValue && revisionIds.Contains(value.CentralSourceRevisionId.Value)) ||
                     (value.TenantSourceRevisionId.HasValue && revisionIds.Contains(value.TenantSourceRevisionId.Value))),
                    cancellationToken) ||
            await database.Set<RecipeRevisionRecord>().AsNoTracking()
                .AnyAsync(value => value.RecipeId != recipeId && value.RestoredFromRevisionId.HasValue &&
                                   revisionIds.Contains(value.RestoredFromRevisionId.Value), cancellationToken))
            return true;

        return await database.Set<RecipeRevisionRecord>().AsNoTracking()
            .AnyAsync(value => value.RecipeId == recipeId && value.CentralSubmissionId.HasValue, cancellationToken);
    }

    private async Task<RecipeLifecycleResult> ChangeLifecycleAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        bool reactivate,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        RecipeRecord? record = await database.Set<RecipeRecord>()
            .SingleOrDefaultAsync(value => value.Id == recipeId, cancellationToken);
        if (record is null)
            return new RecipeLifecycleResult(RecipeLifecycleStatus.NotFound, null);
        if (record.DraftVersion != expectedVersion)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new RecipeLifecycleResult(
                RecipeLifecycleStatus.VersionConflict, await LoadAsync(recipeId, cancellationToken));
        }

        bool isArchived = record.Status == (int)RecipeStatus.Archived;
        if (reactivate != isArchived)
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return new RecipeLifecycleResult(
                RecipeLifecycleStatus.InvalidStatus, await LoadAsync(recipeId, cancellationToken));
        }

        if (reactivate)
        {
            bool hasRevisions = await database.Set<RecipeRevisionRecord>().AsNoTracking()
                .AnyAsync(value => value.RecipeId == recipeId, cancellationToken);
            record.Status = (int)(hasRevisions ? RecipeStatus.Active : RecipeStatus.Draft);
            record.ReactivatedBy = actorUserId;
            record.ReactivatedAtUtc = timestampUtc;
        }
        else
        {
            record.Status = (int)RecipeStatus.Archived;
            record.ArchivedBy = actorUserId;
            record.ArchivedAtUtc = timestampUtc;
        }
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
            return new RecipeLifecycleResult(
                RecipeLifecycleStatus.VersionConflict, await LoadAsync(recipeId, cancellationToken));
        }
        database.ChangeTracker.Clear();
        return new RecipeLifecycleResult(
            RecipeLifecycleStatus.Changed, await LoadAsync(recipeId, cancellationToken));
    }

    private async Task<RecipeDraft?> LoadAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        RecipeRecord? record = await database.Set<RecipeRecord>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == recipeId, cancellationToken);
        if (record is null) return null;

        var draft = new RecipeDraft(
            record.Id, (RecipeScopeType)record.ScopeType, record.ScopeId, (RecipeType)record.RecipeType,
            record.Name, (RecipeStatus)record.Status);
        draft.SetDetails(record.Description, record.Source, record.InternalNotes);
        if ((RecipeType)record.RecipeType == RecipeType.PortionBased)
        {
            AuthoringStageSnapshot? stage = record.AuthoringStageId.HasValue &&
                                             record.AuthoringStageName is not null &&
                                             record.AuthoringStageFactor.HasValue
                ? new AuthoringStageSnapshot(
                    record.AuthoringStageId.Value, record.AuthoringStageName, record.AuthoringStageFactor.Value)
                : null;
            draft.ConfigurePortionReference(record.ReferenceServings, record.DefaultAgeGroupScalingApplies, stage);
        }
        else
        {
            draft.ConfigureQuantityReference(record.ReferenceQuantity, record.ReferenceUnitId);
        }

        string[] tags = await database.Set<RecipeDraftTagRecord>().AsNoTracking()
            .Where(value => value.RecipeId == recipeId).Select(value => value.Value).ToArrayAsync(cancellationToken);
        draft.ReplaceTags(tags);
        RecipeGroupRecord[] groups = await database.Set<RecipeGroupRecord>().AsNoTracking()
            .Where(value => value.RecipeId == recipeId).OrderBy(value => value.SortOrder).ToArrayAsync(cancellationToken);
        foreach (RecipeGroupRecord group in groups)
            draft.AddGroup(new RecipeIngredientGroup(group.Id, group.RecipeId, group.Name, group.SortOrder));

        await LoadIngredientPositionsAsync(draft, cancellationToken);
        await LoadSubrecipePositionsAsync(draft, cancellationToken);
        draft.SetPersistedVersion(record.DraftVersion);
        return draft;
    }

    private async Task LoadIngredientPositionsAsync(RecipeDraft draft, CancellationToken cancellationToken)
    {
        RecipeIngredientPositionRecord[] positions = await database.Set<RecipeIngredientPositionRecord>().AsNoTracking()
            .Where(value => value.RecipeId == draft.Id).OrderBy(value => value.SortOrder).ToArrayAsync(cancellationToken);
        Guid[] positionIds = positions.Select(value => value.Id).ToArray();
        RecipeIngredientReplacementRecord[] replacements = positionIds.Length == 0
            ? []
            : await database.Set<RecipeIngredientReplacementRecord>().AsNoTracking()
                .Where(value => positionIds.Contains(value.IngredientPositionId)).ToArrayAsync(cancellationToken);
        Dictionary<Guid, HashSet<ConflictReference>> conflicts = await LoadIngredientConflictsAsync(
            replacements.Select(value => value.Id).ToArray(), cancellationToken);

        foreach (RecipeIngredientPositionRecord value in positions)
        {
            var position = new RecipeIngredientPosition(
                value.Id, value.RecipeId, value.GroupId, value.BaseIngredientId, value.Quantity, value.UnitId,
                value.SortOrder, (ScalingMode)value.ScalingMode, (AgeGroupScalingMode)value.AgeGroupScaling,
                value.StepSize.HasValue || value.QuantityPerStep.HasValue
                    ? new StepwiseScaling(value.StepSize, value.QuantityPerStep)
                    : null);
            foreach (RecipeIngredientReplacementRecord replacement in replacements
                         .Where(item => item.IngredientPositionId == value.Id))
                position.AddReplacementRule(new IngredientReplacementRule(
                    replacement.Id, replacement.IngredientPositionId, replacement.ReplacementBaseIngredientId,
                    replacement.ReplacementQuantity, replacement.ReplacementUnitId,
                    conflicts.GetValueOrDefault(replacement.Id) ?? []));
            draft.AddIngredientPosition(position);
        }
    }

    private async Task LoadSubrecipePositionsAsync(RecipeDraft draft, CancellationToken cancellationToken)
    {
        RecipeSubrecipePositionRecord[] positions = await database.Set<RecipeSubrecipePositionRecord>().AsNoTracking()
            .Where(value => value.RecipeId == draft.Id).OrderBy(value => value.SortOrder).ToArrayAsync(cancellationToken);
        Guid[] positionIds = positions.Select(value => value.Id).ToArray();
        RecipeSubrecipeReplacementRecord[] replacements = positionIds.Length == 0
            ? []
            : await database.Set<RecipeSubrecipeReplacementRecord>().AsNoTracking()
                .Where(value => positionIds.Contains(value.SubrecipePositionId)).ToArrayAsync(cancellationToken);
        Dictionary<Guid, HashSet<ConflictReference>> conflicts = await LoadSubrecipeConflictsAsync(
            replacements.Select(value => value.Id).ToArray(), cancellationToken);

        foreach (RecipeSubrecipePositionRecord value in positions)
        {
            var position = new RecipeSubrecipePosition(
                value.Id, value.RecipeId, value.GroupId, value.RecipeRevisionId, value.RequiredServings,
                value.RequiredQuantity, value.RequiredUnitId, value.SortOrder);
            foreach (RecipeSubrecipeReplacementRecord replacement in replacements
                         .Where(item => item.SubrecipePositionId == value.Id))
                position.AddReplacementRule(new RecipeReplacementRule(
                    replacement.Id, replacement.SubrecipePositionId, replacement.ReplacementRecipeRevisionId,
                    replacement.ReplacementServings, replacement.ReplacementQuantity, replacement.ReplacementUnitId,
                    conflicts.GetValueOrDefault(replacement.Id) ?? []));
            draft.AddSubrecipePosition(position);
        }
    }

    private async Task<Dictionary<Guid, HashSet<ConflictReference>>> LoadIngredientConflictsAsync(
        Guid[] replacementIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, HashSet<ConflictReference>>();
        if (replacementIds.Length == 0) return result;
        var allergens = await database.Set<RecipeIngredientReplacementAllergenRecord>().AsNoTracking()
            .Where(value => replacementIds.Contains(value.ReplacementId)).ToArrayAsync(cancellationToken);
        var intolerances = await database.Set<RecipeIngredientReplacementIntoleranceRecord>().AsNoTracking()
            .Where(value => replacementIds.Contains(value.ReplacementId)).ToArrayAsync(cancellationToken);
        var requirements = await database.Set<RecipeIngredientReplacementDietaryRequirementRecord>().AsNoTracking()
            .Where(value => replacementIds.Contains(value.ReplacementId)).ToArrayAsync(cancellationToken);
        foreach (var value in allergens) AddConflict(result, value.ReplacementId, ConflictType.Allergen, value.AllergenId);
        foreach (var value in intolerances) AddConflict(result, value.ReplacementId, ConflictType.Intolerance, value.IntoleranceId);
        foreach (var value in requirements) AddConflict(result, value.ReplacementId, ConflictType.DietaryRequirement, value.DietaryRequirementId);
        return result;
    }

    private async Task<Dictionary<Guid, HashSet<ConflictReference>>> LoadSubrecipeConflictsAsync(
        Guid[] replacementIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, HashSet<ConflictReference>>();
        if (replacementIds.Length == 0) return result;
        var allergens = await database.Set<RecipeSubrecipeReplacementAllergenRecord>().AsNoTracking()
            .Where(value => replacementIds.Contains(value.ReplacementId)).ToArrayAsync(cancellationToken);
        var intolerances = await database.Set<RecipeSubrecipeReplacementIntoleranceRecord>().AsNoTracking()
            .Where(value => replacementIds.Contains(value.ReplacementId)).ToArrayAsync(cancellationToken);
        var requirements = await database.Set<RecipeSubrecipeReplacementDietaryRequirementRecord>().AsNoTracking()
            .Where(value => replacementIds.Contains(value.ReplacementId)).ToArrayAsync(cancellationToken);
        foreach (var value in allergens) AddConflict(result, value.ReplacementId, ConflictType.Allergen, value.AllergenId);
        foreach (var value in intolerances) AddConflict(result, value.ReplacementId, ConflictType.Intolerance, value.IntoleranceId);
        foreach (var value in requirements) AddConflict(result, value.ReplacementId, ConflictType.DietaryRequirement, value.DietaryRequirementId);
        return result;
    }

    private static void AddConflict(
        Dictionary<Guid, HashSet<ConflictReference>> target,
        Guid replacementId,
        ConflictType type,
        Guid conflictId)
    {
        if (!target.TryGetValue(replacementId, out HashSet<ConflictReference>? values))
            target[replacementId] = values = [];
        values.Add(new ConflictReference(type, conflictId));
    }

    private async Task RemoveGraphAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        RecipeIngredientPositionRecord[] ingredientPositions = await database.Set<RecipeIngredientPositionRecord>()
            .Where(value => value.RecipeId == recipeId).ToArrayAsync(cancellationToken);
        Guid[] ingredientPositionIds = ingredientPositions.Select(value => value.Id).ToArray();
        RecipeIngredientReplacementRecord[] ingredientReplacements = ingredientPositionIds.Length == 0
            ? []
            : await database.Set<RecipeIngredientReplacementRecord>()
                .Where(value => ingredientPositionIds.Contains(value.IngredientPositionId)).ToArrayAsync(cancellationToken);
        RemoveIngredientConflictLinks(ingredientReplacements.Select(value => value.Id).ToArray());

        RecipeSubrecipePositionRecord[] subrecipePositions = await database.Set<RecipeSubrecipePositionRecord>()
            .Where(value => value.RecipeId == recipeId).ToArrayAsync(cancellationToken);
        Guid[] subrecipePositionIds = subrecipePositions.Select(value => value.Id).ToArray();
        RecipeSubrecipeReplacementRecord[] subrecipeReplacements = subrecipePositionIds.Length == 0
            ? []
            : await database.Set<RecipeSubrecipeReplacementRecord>()
                .Where(value => subrecipePositionIds.Contains(value.SubrecipePositionId)).ToArrayAsync(cancellationToken);
        RemoveSubrecipeConflictLinks(subrecipeReplacements.Select(value => value.Id).ToArray());

        database.RemoveRange(ingredientReplacements);
        database.RemoveRange(subrecipeReplacements);
        database.RemoveRange(ingredientPositions);
        database.RemoveRange(subrecipePositions);
        database.RemoveRange(await database.Set<RecipeGroupRecord>().Where(value => value.RecipeId == recipeId).ToArrayAsync(cancellationToken));
        database.RemoveRange(await database.Set<RecipeDraftTagRecord>().Where(value => value.RecipeId == recipeId).ToArrayAsync(cancellationToken));
    }

    private void RemoveIngredientConflictLinks(Guid[] replacementIds)
    {
        if (replacementIds.Length == 0) return;
        database.RemoveRange(database.Set<RecipeIngredientReplacementAllergenRecord>().Where(value => replacementIds.Contains(value.ReplacementId)));
        database.RemoveRange(database.Set<RecipeIngredientReplacementIntoleranceRecord>().Where(value => replacementIds.Contains(value.ReplacementId)));
        database.RemoveRange(database.Set<RecipeIngredientReplacementDietaryRequirementRecord>().Where(value => replacementIds.Contains(value.ReplacementId)));
    }

    private void RemoveSubrecipeConflictLinks(Guid[] replacementIds)
    {
        if (replacementIds.Length == 0) return;
        database.RemoveRange(database.Set<RecipeSubrecipeReplacementAllergenRecord>().Where(value => replacementIds.Contains(value.ReplacementId)));
        database.RemoveRange(database.Set<RecipeSubrecipeReplacementIntoleranceRecord>().Where(value => replacementIds.Contains(value.ReplacementId)));
        database.RemoveRange(database.Set<RecipeSubrecipeReplacementDietaryRequirementRecord>().Where(value => replacementIds.Contains(value.ReplacementId)));
    }

    private void AddGraph(RecipeDraft draft)
    {
        database.AddRange(draft.Tags.Select(value => new RecipeDraftTagRecord { RecipeId = draft.Id, Value = value }));
        database.AddRange(draft.Groups.Select(value => new RecipeGroupRecord
        {
            Id = value.Id, RecipeId = value.RecipeId, Name = value.Name, SortOrder = value.SortOrder,
        }));
        foreach (RecipeIngredientPosition position in draft.IngredientPositions) AddIngredientPosition(position);
        foreach (RecipeSubrecipePosition position in draft.SubrecipePositions) AddSubrecipePosition(position);
    }

    private void AddIngredientPosition(RecipeIngredientPosition position)
    {
        database.Add(new RecipeIngredientPositionRecord
        {
            Id = position.Id, RecipeId = position.RecipeId, GroupId = position.GroupId,
            BaseIngredientId = position.BaseIngredientId, Quantity = position.Quantity, UnitId = position.UnitId,
            SortOrder = position.SortOrder, ScalingMode = (int)position.ScalingMode,
            AgeGroupScaling = (int)position.AgeGroupScaling, StepSize = position.StepwiseScaling?.StepSize,
            QuantityPerStep = position.StepwiseScaling?.QuantityPerStep,
        });
        foreach (IngredientReplacementRule replacement in position.ReplacementRules)
        {
            database.Add(new RecipeIngredientReplacementRecord
            {
                Id = replacement.Id, IngredientPositionId = replacement.IngredientPositionId,
                ReplacementBaseIngredientId = replacement.ReplacementBaseIngredientId,
                ReplacementQuantity = replacement.ReplacementQuantity, ReplacementUnitId = replacement.ReplacementUnitId,
            });
            foreach (ConflictReference conflict in replacement.Conflicts) AddIngredientConflict(replacement.Id, conflict);
        }
    }

    private void AddSubrecipePosition(RecipeSubrecipePosition position)
    {
        database.Add(new RecipeSubrecipePositionRecord
        {
            Id = position.Id, RecipeId = position.RecipeId, GroupId = position.GroupId,
            RecipeRevisionId = position.RecipeRevisionId, RequiredServings = position.RequiredServings,
            RequiredQuantity = position.RequiredQuantity, RequiredUnitId = position.RequiredUnitId,
            SortOrder = position.SortOrder,
        });
        foreach (RecipeReplacementRule replacement in position.ReplacementRules)
        {
            database.Add(new RecipeSubrecipeReplacementRecord
            {
                Id = replacement.Id, SubrecipePositionId = replacement.SubrecipePositionId,
                ReplacementRecipeRevisionId = replacement.ReplacementRecipeRevisionId,
                ReplacementServings = replacement.ReplacementServings,
                ReplacementQuantity = replacement.ReplacementQuantity, ReplacementUnitId = replacement.ReplacementUnitId,
            });
            foreach (ConflictReference conflict in replacement.Conflicts) AddSubrecipeConflict(replacement.Id, conflict);
        }
    }

    private void AddIngredientConflict(Guid replacementId, ConflictReference conflict)
    {
        object record = conflict.Type switch
        {
            ConflictType.Allergen => new RecipeIngredientReplacementAllergenRecord { ReplacementId = replacementId, AllergenId = conflict.Id },
            ConflictType.Intolerance => new RecipeIngredientReplacementIntoleranceRecord { ReplacementId = replacementId, IntoleranceId = conflict.Id },
            ConflictType.DietaryRequirement => new RecipeIngredientReplacementDietaryRequirementRecord { ReplacementId = replacementId, DietaryRequirementId = conflict.Id },
            _ => throw new ArgumentOutOfRangeException(nameof(conflict)),
        };
        database.Add(record);
    }

    private void AddSubrecipeConflict(Guid replacementId, ConflictReference conflict)
    {
        object record = conflict.Type switch
        {
            ConflictType.Allergen => new RecipeSubrecipeReplacementAllergenRecord { ReplacementId = replacementId, AllergenId = conflict.Id },
            ConflictType.Intolerance => new RecipeSubrecipeReplacementIntoleranceRecord { ReplacementId = replacementId, IntoleranceId = conflict.Id },
            ConflictType.DietaryRequirement => new RecipeSubrecipeReplacementDietaryRequirementRecord { ReplacementId = replacementId, DietaryRequirementId = conflict.Id },
            _ => throw new ArgumentOutOfRangeException(nameof(conflict)),
        };
        database.Add(record);
    }

    private static void CopyDraft(
        RecipeRecord target,
        RecipeDraft source,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        long version)
    {
        target.ScopeType = (int)source.ScopeType;
        target.ScopeId = source.ScopeId;
        target.Name = source.Name;
        target.NormalizedName = source.NormalizedName;
        target.Status = (int)source.Status;
        target.RecipeType = (int)source.RecipeType;
        target.Description = source.Description;
        target.Source = source.Source;
        target.InternalNotes = source.InternalNotes;
        target.ReferenceServings = source.ReferenceServings;
        target.AuthoringStageId = source.AuthoringStage?.StageId;
        target.AuthoringStageName = source.AuthoringStage?.StageName;
        target.AuthoringStageFactor = source.AuthoringStage?.Factor;
        target.ReferenceQuantity = source.ReferenceQuantity;
        target.ReferenceUnitId = source.ReferenceUnitId;
        target.DefaultAgeGroupScalingApplies = source.DefaultAgeGroupScalingApplies;
        target.DraftVersion = version;
        target.UpdatedBy = actorUserId;
        target.UpdatedAtUtc = timestampUtc;
    }
}
