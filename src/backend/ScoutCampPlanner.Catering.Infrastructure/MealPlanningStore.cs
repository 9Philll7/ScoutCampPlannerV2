using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;

namespace ScoutCampPlanner.Catering.Infrastructure;

public sealed class MealPlanningStore(CateringDbContext database) : IMealPlanningStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MealPlanningData> LoadAsync(
        Guid campId, CancellationToken cancellationToken = default) => new(
        await database.MealPlans.AsNoTracking().Where(value => value.CampId == campId)
            .OrderBy(value => value.SortOrder).ToArrayAsync(cancellationToken),
        await database.MealPlanSnapshots.AsNoTracking().Where(value => value.CampId == campId)
            .ToArrayAsync(cancellationToken),
        await (from offerGroup in database.MealPlanOfferGroups.AsNoTracking()
            join plan in database.MealPlans.AsNoTracking() on offerGroup.MealPlanId equals plan.Id
            where plan.CampId == campId select offerGroup).ToArrayAsync(cancellationToken),
        await (from entry in database.MealPlanEntries.AsNoTracking()
            join offerGroup in database.MealPlanOfferGroups.AsNoTracking() on entry.OfferGroupId equals offerGroup.Id
            join plan in database.MealPlans.AsNoTracking() on offerGroup.MealPlanId equals plan.Id
            where plan.CampId == campId select entry).ToArrayAsync(cancellationToken),
        await database.CampMealTypes.AsNoTracking().Where(value => value.CampId == campId)
            .OrderBy(value => value.SortOrder).ToArrayAsync(cancellationToken),
        await database.CampMeals.AsNoTracking().Where(value => value.CampId == campId)
            .OrderBy(value => value.Date).ToArrayAsync(cancellationToken),
        await database.CampStageFoodFactors.AsNoTracking().Where(value => value.CampId == campId)
            .ToArrayAsync(cancellationToken),
        await database.CookingUnitGroups.AsNoTracking().Where(value => value.CampId == campId)
            .OrderBy(value => value.SortOrder).ToArrayAsync(cancellationToken),
        await database.CookingUnits.AsNoTracking().Where(value => value.CampId == campId)
            .OrderBy(value => value.SortOrder).ToArrayAsync(cancellationToken),
        await database.CookingUnitStructureAssignments.AsNoTracking().Where(value => value.CampId == campId)
            .ToArrayAsync(cancellationToken),
        await database.CookingUnitMealStates.AsNoTracking().Where(value => value.CampId == campId)
            .ToArrayAsync(cancellationToken),
        await (from target in database.CookingUnitMealOfferTargets.AsNoTracking()
            join state in database.CookingUnitMealStates.AsNoTracking()
                on target.CookingUnitMealStateId equals state.Id
            where state.CampId == campId select target).ToArrayAsync(cancellationToken),
        await (from choice in database.CookingUnitMealRecipeChoices.AsNoTracking()
            join state in database.CookingUnitMealStates.AsNoTracking()
                on choice.CookingUnitMealStateId equals state.Id
            where state.CampId == campId select choice).ToArrayAsync(cancellationToken));

    public async Task<IReadOnlyList<MealPlanningRecipeOption>> ListAccessibleRecipesAsync(
        Guid campId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var values = await (from revision in database.Set<RecipeRevisionRecord>().AsNoTracking()
            join recipe in database.Set<RecipeRecord>().AsNoTracking() on revision.RecipeId equals recipe.Id
            where recipe.ScopeType == (int)RecipeScopeType.Central ||
                  recipe.ScopeType == (int)RecipeScopeType.Tenant && recipe.ScopeId == tenantId ||
                  recipe.ScopeType == (int)RecipeScopeType.Camp && recipe.ScopeId == campId
            orderby recipe.Name, revision.RevisionNumber descending
            select new
            {
                revision.Id, revision.RecipeId, recipe.Name, revision.RevisionNumber,
                recipe.RecipeType, revision.SnapshotJson,
            }).ToArrayAsync(cancellationToken);
        return values.Select(value =>
        {
            RecipeSnapshot snapshot = RecipeSnapshotBuilder.Deserialize(value.SnapshotJson);
            return new MealPlanningRecipeOption(
                value.Id, value.RecipeId, value.Name, value.RevisionNumber,
                value.RecipeType == (int)RecipeType.PortionBased,
                ReadSuggestedRole(snapshot));
        }).Where(value => value.IsPortionBased).ToArray();
    }

    public async Task<bool> AreRecipeRevisionsAccessibleAsync(
        Guid campId, Guid tenantId, IReadOnlyCollection<Guid> revisionIds,
        CancellationToken cancellationToken = default)
    {
        if (revisionIds.Count == 0) return true;
        int count = await (from revision in database.Set<RecipeRevisionRecord>().AsNoTracking()
            join recipe in database.Set<RecipeRecord>().AsNoTracking() on revision.RecipeId equals recipe.Id
            where revisionIds.Contains(revision.Id) && recipe.RecipeType == (int)RecipeType.PortionBased &&
                  (recipe.ScopeType == (int)RecipeScopeType.Central ||
                   recipe.ScopeType == (int)RecipeScopeType.Tenant && recipe.ScopeId == tenantId ||
                   recipe.ScopeType == (int)RecipeScopeType.Camp && recipe.ScopeId == campId)
            select revision.Id).Distinct().CountAsync(cancellationToken);
        return count == revisionIds.Distinct().Count();
    }

    public async Task EnsureCampRecipeLibraryAsync(
        Guid campId, Guid actorUserId, IReadOnlyCollection<Guid> revisionIds,
        CancellationToken cancellationToken = default)
    {
        if (revisionIds.Count == 0) return;
        var revisions = await (from revision in database.Set<RecipeRevisionRecord>().AsNoTracking()
            join recipe in database.Set<RecipeRecord>().AsNoTracking() on revision.RecipeId equals recipe.Id
            where revisionIds.Contains(revision.Id)
            select new { RevisionId = revision.Id, RecipeId = recipe.Id, Scope = recipe.ScopeType })
            .ToArrayAsync(cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (var revision in revisions)
        {
            if (revision.Scope == (int)RecipeScopeType.Camp)
            {
                if (!await database.Set<CampRecipeEntryRecord>().AnyAsync(value =>
                        value.CampId == campId && (value.CampRecipeId == revision.RecipeId ||
                            value.UpstreamRecipeRevisionId == revision.RevisionId), cancellationToken))
                    database.Add(new CampRecipeEntryRecord
                    {
                        Id = Guid.NewGuid(), CampId = campId, CampRecipeId = revision.RecipeId,
                        CreatedBy = actorUserId, CreatedAtUtc = now, UpdatedBy = actorUserId, UpdatedAtUtc = now,
                    });
            }
            else if (!await database.Set<CampRecipeEntryRecord>().AnyAsync(value =>
                         value.CampId == campId && value.UpstreamRecipeRevisionId == revision.RevisionId,
                         cancellationToken))
                database.Add(new CampRecipeEntryRecord
                {
                    Id = Guid.NewGuid(), CampId = campId, UpstreamRecipeRevisionId = revision.RevisionId,
                    CreatedBy = actorUserId, CreatedAtUtc = now, UpdatedBy = actorUserId, UpdatedAtUtc = now,
                });
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<MealPlanningMutationResult> CreateMealPlanAsync(
        MealPlan mealPlan, CancellationToken cancellationToken = default)
    {
        if (await database.MealPlans.AnyAsync(value => value.CampId == mealPlan.CampId &&
                value.Name.ToUpper() == mealPlan.Name.ToUpper(), cancellationToken))
            return Invalid("meal_plan_name_duplicate", "Ein Mahlzeitenplan mit diesem Namen existiert bereits.");
        database.MealPlans.Add(mealPlan);
        await database.SaveChangesAsync(cancellationToken);
        return Success(mealPlan.Id, mealPlan.Version);
    }

    public async Task<MealPlanningMutationResult> SaveMealPlanAsync(
        Guid campId, Guid mealPlanId, int expectedVersion, string name, int sortOrder,
        IReadOnlyList<MealPlanOfferGroupDocument> groups, string snapshotJson,
        DateTimeOffset savedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        MealPlan? plan = await database.MealPlans.SingleOrDefaultAsync(
            value => value.Id == mealPlanId && value.CampId == campId, cancellationToken);
        if (plan is null) return NotFound();
        if (plan.Version != expectedVersion)
            return new MealPlanningMutationResult(MealPlanningMutationStatus.Conflict,
                "meal_plan_version_conflict", "Der Mahlzeitenplan wurde zwischenzeitlich geändert.", Version: plan.Version);
        if (await database.MealPlans.AnyAsync(value => value.CampId == campId && value.Id != mealPlanId &&
                value.Name.ToUpper() == name.ToUpper(), cancellationToken))
            return Invalid("meal_plan_name_duplicate", "Ein Mahlzeitenplan mit diesem Namen existiert bereits.");

        MealPlanOfferGroup[] existingGroups = await database.MealPlanOfferGroups
            .Where(value => value.MealPlanId == mealPlanId).ToArrayAsync(cancellationToken);
        Guid[] oldGroupIds = existingGroups.Select(value => value.Id).ToArray();
        MealPlanEntry[] existingEntries = await database.MealPlanEntries
            .Where(value => oldGroupIds.Contains(value.OfferGroupId)).ToArrayAsync(cancellationToken);
        Dictionary<Guid, MealPlanOfferGroup> groupsById = existingGroups.ToDictionary(value => value.Id);
        Dictionary<Guid, MealPlanEntry> entriesById = existingEntries.ToDictionary(value => value.Id);
        foreach (MealPlanOfferGroupDocument group in groups)
        {
            if (groupsById.TryGetValue(group.Id, out MealPlanOfferGroup? existingGroup) &&
                existingGroup.CampMealId != group.CampMealId)
                return Invalid("offer_group_meal_immutable", "Eine bestehende Angebotsgruppe kann nicht in eine andere Mahlzeit verschoben werden.");
            foreach (MealPlanEntryDocument entry in group.Entries)
                if (entriesById.TryGetValue(entry.Id, out MealPlanEntry? existingEntry) &&
                    existingEntry.OfferGroupId != group.Id)
                    return Invalid("meal_plan_entry_group_immutable", "Ein bestehender Eintrag kann nicht in eine andere Angebotsgruppe verschoben werden.");
        }
        Guid[] incomingGroupIds = groups.Select(value => value.Id).ToArray();
        Guid[] incomingEntryIds = groups.SelectMany(value => value.Entries).Select(value => value.Id).ToArray();
        Guid[] removedGroupIds = oldGroupIds.Except(incomingGroupIds).ToArray();
        Guid[] removedEntryIds = existingEntries.Select(value => value.Id).Except(incomingEntryIds).ToArray();
        bool removedReferences = await database.CookingUnitMealOfferTargets.AnyAsync(
                value => removedGroupIds.Contains(value.OfferGroupId), cancellationToken) ||
            await database.CookingUnitMealRecipeChoices.AnyAsync(value =>
                value.OfferGroupId.HasValue && removedGroupIds.Contains(value.OfferGroupId.Value) ||
                value.MealPlanEntryId.HasValue && removedEntryIds.Contains(value.MealPlanEntryId.Value),
                cancellationToken);
        if (removedReferences)
            return new MealPlanningMutationResult(MealPlanningMutationStatus.Blocked,
                "meal_plan_entries_in_use",
                "Mindestens eine zu entfernende Angebotsgruppe oder ein Eintrag wird operativ verwendet.");
        string existingSupply = string.Join('|', existingGroups.OrderBy(value => value.Id)
            .Select(value => $"G:{value.Id}:{value.CampMealId}")
            .Concat(existingEntries.OrderBy(value => value.Id)
                .Select(value => $"E:{value.Id}:{value.OfferGroupId}:{value.RecipeRevisionId}:{value.IsStandard}:{value.Role}")));
        string incomingSupply = string.Join('|', groups.OrderBy(value => value.Id)
            .Select(value => $"G:{value.Id}:{value.CampMealId}")
            .Concat(groups.SelectMany(value => value.Entries.Select(entry => new { GroupId = value.Id, Entry = entry }))
                .OrderBy(value => value.Entry.Id)
                .Select(value => $"E:{value.Entry.Id}:{value.GroupId}:{value.Entry.RecipeRevisionId}:{value.Entry.IsStandard}:{value.Entry.Role}")));
        bool supplyChanged = plan.Version == 0 ||
            !string.Equals(existingSupply, incomingSupply, StringComparison.Ordinal);
        plan.Rename(name);
        plan.SetSortOrder(sortOrder);
        foreach (MealPlanEntry removed in existingEntries.Where(value => removedEntryIds.Contains(value.Id)))
            database.MealPlanEntries.Remove(removed);
        foreach (MealPlanOfferGroup removed in existingGroups.Where(value => removedGroupIds.Contains(value.Id)))
            database.MealPlanOfferGroups.Remove(removed);
        foreach (MealPlanOfferGroupDocument group in groups)
        {
            if (groupsById.TryGetValue(group.Id, out MealPlanOfferGroup? persistedGroup))
                persistedGroup.Update(group.Name, group.SortOrder);
            else
                database.MealPlanOfferGroups.Add(new MealPlanOfferGroup(
                    group.Id, mealPlanId, group.CampMealId, group.Name, group.SortOrder));
            foreach (MealPlanEntryDocument entry in group.Entries)
            {
                if (entriesById.TryGetValue(entry.Id, out MealPlanEntry? persistedEntry))
                    persistedEntry.Update(entry.RecipeRevisionId, entry.IsStandard, entry.DisplayName,
                        entry.Role, entry.Note, entry.SortOrder);
                else
                    database.MealPlanEntries.Add(new MealPlanEntry(
                        entry.Id, group.Id, entry.RecipeRevisionId, entry.IsStandard, entry.DisplayName,
                        entry.Role, entry.Note, entry.SortOrder));
            }
        }
        if (!supplyChanged)
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(mealPlanId, plan.Version);
        }
        int version = plan.CommitVersion();
        var snapshot = new MealPlanSnapshot(Guid.NewGuid(), mealPlanId, campId, version, snapshotJson, savedAtUtc);
        database.MealPlanSnapshots.Add(snapshot);
        CookingUnitMealState[] affected = await (from state in database.CookingUnitMealStates
            join unit in database.CookingUnits on state.CookingUnitId equals unit.Id
            where unit.CampId == campId && unit.StandardMealPlanId == mealPlanId &&
                  state.SubscriptionState == MealPlanSubscriptionState.FollowStandard
            select state).ToArrayAsync(cancellationToken);
        foreach (CookingUnitMealState state in affected) state.MarkStale();
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Success(mealPlanId, version);
    }

    public async Task<MealPlanningMutationResult> DeleteMealPlanAsync(
        Guid campId, Guid mealPlanId, CancellationToken cancellationToken = default)
    {
        MealPlan? plan = await database.MealPlans.SingleOrDefaultAsync(
            value => value.Id == mealPlanId && value.CampId == campId, cancellationToken);
        if (plan is null) return NotFound();
        string[] references = (await database.CookingUnits.AsNoTracking()
                .Where(value => value.StandardMealPlanId == mealPlanId)
                .Select(value => $"Kocheinheit: {value.Name}").ToArrayAsync(cancellationToken))
            .Concat(await database.CookingUnitMealStates.AsNoTracking()
                .Where(value => value.MealPlanId == mealPlanId)
                .Select(value => $"Verpflegungs-Teilstand: {value.CookingUnitId}/{value.CampMealId}")
                .ToArrayAsync(cancellationToken)).Distinct().ToArray();
        if (references.Length > 0)
            return new MealPlanningMutationResult(MealPlanningMutationStatus.Blocked,
                "meal_plan_in_use", "Der Mahlzeitenplan wird noch verwendet.", references);
        Guid[] groups = await database.MealPlanOfferGroups.Where(value => value.MealPlanId == mealPlanId)
            .Select(value => value.Id).ToArrayAsync(cancellationToken);
        await database.MealPlanEntries.Where(value => groups.Contains(value.OfferGroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.MealPlanOfferGroups.Where(value => value.MealPlanId == mealPlanId).ExecuteDeleteAsync(cancellationToken);
        await database.MealPlanSnapshots.Where(value => value.MealPlanId == mealPlanId).ExecuteDeleteAsync(cancellationToken);
        database.MealPlans.Remove(plan);
        await database.SaveChangesAsync(cancellationToken);
        return Success(mealPlanId);
    }

    public async Task<MealPlanningMutationResult> ReorderMealPlansAsync(
        Guid campId, IReadOnlyList<Guid> mealPlanIds, CancellationToken cancellationToken = default)
    {
        MealPlan[] plans = await database.MealPlans.Where(value => value.CampId == campId)
            .ToArrayAsync(cancellationToken);
        if (plans.Length != mealPlanIds.Count) return Invalid("meal_plan_order_invalid", "Die Planreihenfolge ist ungültig.");
        Dictionary<Guid, MealPlan> byId = plans.ToDictionary(value => value.Id);
        for (int index = 0; index < mealPlanIds.Count; index++)
        {
            if (!byId.TryGetValue(mealPlanIds[index], out MealPlan? plan))
                return Invalid("meal_plan_order_invalid", "Die Planreihenfolge ist ungültig.");
            plan.SetSortOrder(index);
        }
        await database.SaveChangesAsync(cancellationToken);
        return Success();
    }

    public async Task<MealPlanningMutationResult> SaveCookingUnitGroupAsync(
        CookingUnitGroup group, CancellationToken cancellationToken = default)
    {
        if (await database.CookingUnitGroups.AnyAsync(value => value.CampId == group.CampId &&
                value.Id != group.Id && value.Name.ToUpper() == group.Name.ToUpper(), cancellationToken))
            return Invalid("cooking_unit_group_name_duplicate", "Eine Gruppe mit diesem Namen existiert bereits.");
        CookingUnitGroup? existing = await database.CookingUnitGroups.SingleOrDefaultAsync(
            value => value.Id == group.Id && value.CampId == group.CampId, cancellationToken);
        if (existing is null) database.CookingUnitGroups.Add(group);
        else existing.Update(group.Name, group.SortOrder);
        await database.SaveChangesAsync(cancellationToken);
        return Success(group.Id);
    }

    public async Task<MealPlanningMutationResult> DeleteCookingUnitGroupAsync(
        Guid campId, Guid groupId, CancellationToken cancellationToken = default)
    {
        CookingUnitGroup? group = await database.CookingUnitGroups.SingleOrDefaultAsync(
            value => value.Id == groupId && value.CampId == campId, cancellationToken);
        if (group is null) return NotFound();
        CookingUnit[] units = await database.CookingUnits.Where(value => value.GroupId == groupId).ToArrayAsync(cancellationToken);
        foreach (CookingUnit unit in units)
            unit.Update(unit.Name, unit.SortOrder, null, unit.StandardMealPlanId);
        database.CookingUnitGroups.Remove(group);
        await database.SaveChangesAsync(cancellationToken);
        return Success(groupId);
    }

    public async Task<MealPlanningMutationResult> SaveCookingUnitAsync(
        CookingUnit unit, IReadOnlyCollection<Guid> defaultStructureNodeIds,
        CancellationToken cancellationToken = default)
    {
        if (unit.GroupId.HasValue && !await database.CookingUnitGroups.AnyAsync(value =>
                value.Id == unit.GroupId && value.CampId == unit.CampId, cancellationToken))
            return Invalid("cooking_unit_group_invalid", "Die ausgewählte Gruppe existiert nicht.");
        if (unit.StandardMealPlanId.HasValue && !await database.MealPlans.AnyAsync(value =>
                value.Id == unit.StandardMealPlanId && value.CampId == unit.CampId, cancellationToken))
            return Invalid("meal_plan_invalid", "Der ausgewählte Standardplan existiert nicht.");
        if (await database.CookingUnits.AnyAsync(value => value.CampId == unit.CampId && value.Id != unit.Id &&
                value.Name.ToUpper() == unit.Name.ToUpper(), cancellationToken))
            return Invalid("cooking_unit_name_duplicate", "Eine Kocheinheit mit diesem Namen existiert bereits.");
        CookingUnit? existing = await database.CookingUnits.SingleOrDefaultAsync(
            value => value.Id == unit.Id && value.CampId == unit.CampId, cancellationToken);
        Guid[] previousNodes = await database.CookingUnitStructureAssignments.AsNoTracking().Where(value =>
                value.CookingUnitId == unit.Id && value.CampMealId == null)
            .Select(value => value.StructureNodeId).ToArrayAsync(cancellationToken);
        bool structureChanged = !previousNodes.Order().SequenceEqual(defaultStructureNodeIds.Order());
        bool standardPlanChanged = existing is not null && existing.StandardMealPlanId != unit.StandardMealPlanId;
        if (existing is null) database.CookingUnits.Add(unit);
        else existing.Update(unit.Name, unit.SortOrder, unit.GroupId, unit.StandardMealPlanId);
        await database.CookingUnitStructureAssignments.Where(value =>
                value.CookingUnitId == unit.Id && value.CampMealId == null)
            .ExecuteDeleteAsync(cancellationToken);
        database.CookingUnitStructureAssignments.AddRange(defaultStructureNodeIds.Select(nodeId =>
            new CookingUnitStructureAssignment(Guid.NewGuid(), unit.CampId, unit.Id, null, nodeId)));
        if (structureChanged || standardPlanChanged)
        {
            CookingUnitMealState[] states = await database.CookingUnitMealStates
                .Where(value => value.CookingUnitId == unit.Id).ToArrayAsync(cancellationToken);
            Guid[] overriddenMeals = structureChanged
                ? await database.CookingUnitStructureAssignments.AsNoTracking().Where(value =>
                        value.CookingUnitId == unit.Id && value.CampMealId != null)
                    .Select(value => value.CampMealId!.Value).Distinct().ToArrayAsync(cancellationToken)
                : [];
            foreach (CookingUnitMealState state in states)
                if (standardPlanChanged && state.SubscriptionState == MealPlanSubscriptionState.FollowStandard ||
                    structureChanged && !overriddenMeals.Contains(state.CampMealId))
                    state.MarkStale();
        }
        await database.SaveChangesAsync(cancellationToken);
        return Success(unit.Id);
    }

    public async Task<MealPlanningMutationResult> DeleteCookingUnitAsync(
        Guid campId, Guid cookingUnitId, CancellationToken cancellationToken = default)
    {
        CookingUnit? unit = await database.CookingUnits.SingleOrDefaultAsync(
            value => value.Id == cookingUnitId && value.CampId == campId, cancellationToken);
        if (unit is null) return NotFound();
        database.CookingUnits.Remove(unit);
        await database.SaveChangesAsync(cancellationToken);
        return Success(cookingUnitId);
    }

    public async Task<MealPlanningMutationResult> ConfigureCookingUnitMealAsync(
        Guid campId, Guid cookingUnitId, Guid mealId, ConfigureCookingUnitMealRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await database.CookingUnits.AnyAsync(value => value.Id == cookingUnitId && value.CampId == campId,
                cancellationToken) || !await database.CampMeals.AnyAsync(value => value.Id == mealId && value.CampId == campId,
                cancellationToken)) return NotFound();
        if (request.DemandOverride is < 0 || (request.OfferTargets ?? []).Any(value => value.TargetOverride is < 0))
            return Invalid("demand_invalid", "Bedarfswerte dürfen nicht negativ sein.");
        CookingUnitMealState? state = await database.CookingUnitMealStates.SingleOrDefaultAsync(value =>
            value.CookingUnitId == cookingUnitId && value.CampMealId == mealId, cancellationToken);
        if (state is null)
        {
            state = new CookingUnitMealState(Guid.NewGuid(), campId, cookingUnitId, mealId);
            database.CookingUnitMealStates.Add(state);
        }
        Guid[] previousStructure = await database.CookingUnitStructureAssignments.AsNoTracking().Where(value =>
                value.CookingUnitId == cookingUnitId && value.CampMealId == mealId)
            .Select(value => value.StructureNodeId).ToArrayAsync(cancellationToken);
        var previousTargets = await database.CookingUnitMealOfferTargets.AsNoTracking()
            .Where(value => value.CookingUnitMealStateId == state.Id)
            .Select(value => new { value.OfferGroupId, value.TargetOverride }).ToArrayAsync(cancellationToken);
        var previousChoices = await database.CookingUnitMealRecipeChoices.AsNoTracking()
            .Where(value => value.CookingUnitMealStateId == state.Id)
            .Select(value => new { value.RecipeRevisionId, value.OfferGroupId, value.MealPlanEntryId, value.SortOrder })
            .ToArrayAsync(cancellationToken);
        bool relevantCollectionsChanged = request.StructureOverrideNodeIds is not null &&
                !previousStructure.Order().SequenceEqual(request.StructureOverrideNodeIds.Order()) ||
            !previousTargets.OrderBy(value => value.OfferGroupId).Select(value => (value.OfferGroupId, value.TargetOverride))
                .SequenceEqual((request.OfferTargets ?? []).OrderBy(value => value.OfferGroupId)
                    .Select(value => (value.OfferGroupId, value.TargetOverride))) ||
            !previousChoices.OrderBy(value => value.SortOrder)
                .Select(value => (value.RecipeRevisionId, value.OfferGroupId, value.MealPlanEntryId, value.SortOrder))
                .SequenceEqual((request.RecipeChoices ?? []).OrderBy(value => value.SortOrder)
                    .Select(value => (value.RecipeRevisionId, value.OfferGroupId, value.MealPlanEntryId, value.SortOrder)));
        state.Configure(request.SubscriptionState, request.DemandOverride);
        if (relevantCollectionsChanged) state.MarkStale();
        if (request.StructureOverrideNodeIds is not null)
        {
            await database.CookingUnitStructureAssignments.Where(value =>
                    value.CookingUnitId == cookingUnitId && value.CampMealId == mealId)
                .ExecuteDeleteAsync(cancellationToken);
            database.CookingUnitStructureAssignments.AddRange(request.StructureOverrideNodeIds.Select(nodeId =>
                new CookingUnitStructureAssignment(Guid.NewGuid(), campId, cookingUnitId, mealId, nodeId)));
        }
        await database.CookingUnitMealOfferTargets.Where(value => value.CookingUnitMealStateId == state.Id)
            .ExecuteDeleteAsync(cancellationToken);
        database.CookingUnitMealOfferTargets.AddRange((request.OfferTargets ?? []).Select(value =>
            new CookingUnitMealOfferTarget(value.Id == Guid.Empty ? Guid.NewGuid() : value.Id,
                state.Id, value.OfferGroupId, value.TargetOverride)));
        await database.CookingUnitMealRecipeChoices.Where(value => value.CookingUnitMealStateId == state.Id)
            .ExecuteDeleteAsync(cancellationToken);
        database.CookingUnitMealRecipeChoices.AddRange((request.RecipeChoices ?? []).Select(value =>
            new CookingUnitMealRecipeChoice(value.Id == Guid.Empty ? Guid.NewGuid() : value.Id,
                state.Id, value.RecipeRevisionId, value.OfferGroupId, value.MealPlanEntryId, value.SortOrder)));
        await database.SaveChangesAsync(cancellationToken);
        return Success(state.Id);
    }

    public async Task<MealPlanningMutationResult> ResetStructureOverrideAsync(
        Guid campId, Guid cookingUnitId, Guid mealId, CancellationToken cancellationToken = default)
    {
        CookingUnitMealState? state = await database.CookingUnitMealStates.SingleOrDefaultAsync(value =>
            value.CampId == campId && value.CookingUnitId == cookingUnitId && value.CampMealId == mealId,
            cancellationToken);
        if (state is null) return NotFound();
        await database.CookingUnitStructureAssignments.Where(value =>
                value.CookingUnitId == cookingUnitId && value.CampMealId == mealId)
            .ExecuteDeleteAsync(cancellationToken);
        state.MarkStale();
        await database.SaveChangesAsync(cancellationToken);
        return Success(state.Id);
    }

    public async Task SaveCalculationAsync(
        Guid campId, Guid cookingUnitId, Guid mealId, decimal? calculatedDemand,
        Guid? mealPlanId, int? mealPlanVersion, Guid? snapshotId, string calculationJson,
        string sourceFingerprint, IReadOnlyList<string> warnings, bool complete,
        DateTimeOffset calculatedAtUtc, CancellationToken cancellationToken = default)
    {
        CookingUnitMealState? state = await database.CookingUnitMealStates.SingleOrDefaultAsync(value =>
            value.CampId == campId && value.CookingUnitId == cookingUnitId && value.CampMealId == mealId,
            cancellationToken);
        if (state is null)
        {
            state = new CookingUnitMealState(Guid.NewGuid(), campId, cookingUnitId, mealId);
            database.CookingUnitMealStates.Add(state);
        }
        state.ApplyCalculation(calculatedDemand, mealPlanId, mealPlanVersion, snapshotId,
            calculationJson, sourceFingerprint, JsonSerializer.Serialize(warnings, JsonOptions),
            calculatedAtUtc, complete);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static MealPlanEntryRole? ReadSuggestedRole(RecipeSnapshot snapshot)
    {
        // Recipe snapshots predating meal planning do not carry a role qualifier. The meal-plan entry remains optional.
        return null;
    }

    private static MealPlanningMutationResult Success(Guid? id = null, int? version = null) =>
        new(MealPlanningMutationStatus.Success, Id: id, Version: version);

    private static MealPlanningMutationResult Invalid(string code, string message) =>
        new(MealPlanningMutationStatus.Invalid, code, message);

    private static MealPlanningMutationResult NotFound() =>
        new(MealPlanningMutationStatus.NotFound, "not_found", "Der Datensatz wurde nicht gefunden.");
}
