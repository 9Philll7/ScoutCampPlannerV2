using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Platform.Application.Auditing;

namespace ScoutCampPlanner.Api.Catering;

public sealed record AddAdditionalMealRequest(DateOnly Date, string Name, long? ExpectedScheduleVersion);
public sealed record OrderDayMealsRequest(IReadOnlyList<Guid> MealIds, long? ExpectedScheduleVersion);
public sealed record MealScheduleMutation(UpdateCampMealsFailure Failure, long ScheduleVersion = 0,
    CampMealSummary? Meal = null, string? Outcome = null);

public sealed partial class CateringPlanningService
{
    private sealed class ScheduleRejected(UpdateCampMealsFailure failure) : Exception
    {
        public UpdateCampMealsFailure Failure { get; } = failure;
    }

    private static void Require(bool condition, UpdateCampMealsFailure failure = UpdateCampMealsFailure.Invalid)
    {
        if (!condition) throw new ScheduleRejected(failure);
    }

    private async Task<MealScheduleMutation> MutateScheduleAsync(Guid actor, Guid tenantId, Guid campId,
        bool frozen, long? expectedVersion, string action, Func<CancellationToken, Task<MealScheduleMutation>> mutate,
        CancellationToken ct)
    {
        if (frozen) return new(UpdateCampMealsFailure.Frozen);
        if (expectedVersion < 0) return new(UpdateCampMealsFailure.Invalid);
        MealScheduleMutation result = new(UpdateCampMealsFailure.None);
        var audit = new AuditEventDraft(Guid.NewGuid(), timeProvider.GetUtcNow(), action, "success",
            actor, tenantId, campId, "camp-meal-schedule", campId, "server", auditRuntime.InstanceId,
            Guid.NewGuid(), null, null, new Dictionary<string, string>());
        try
        {
            await auditedOperation.ExecuteAsync(audit, async token =>
            {
                var transaction = platform.Database.CurrentTransaction!;
                await catering.Database.UseTransactionAsync(transaction.GetDbTransaction(), token);
                await camps.Database.UseTransactionAsync(transaction.GetDbTransaction(), token);
                try
                {
                    // All reads affecting the decision belong to the audited transaction.
                    catering.ChangeTracker.Clear();
                    var currentCamp = await camps.Camps.AsNoTracking().SingleOrDefaultAsync(x => x.Id == campId, token);
                    Require(currentCamp is not null, UpdateCampMealsFailure.NotFound);
                    Require(!currentCamp!.IsFrozen, UpdateCampMealsFailure.Frozen);
                    var state = await catering.CampMealScheduleStates.SingleOrDefaultAsync(x => x.CampId == campId, token);
                    if (state is null) { state = new(campId); catering.Add(state); }
                    Require(expectedVersion is null || state.Version == expectedVersion, UpdateCampMealsFailure.Changed);
                    state.Advance();
                    // Persist the concurrency token before changing the schedule.
                    await catering.SaveChangesAsync(token);
                    result = await mutate(token);
                    await catering.SaveChangesAsync(token);
                    result = result with { ScheduleVersion = state.Version };
                }
                finally
                {
                    await camps.Database.UseTransactionAsync(null, CancellationToken.None);
                    await catering.Database.UseTransactionAsync(null, CancellationToken.None);
                }
            }, ct);
            return result;
        }
        catch (ScheduleRejected rejected) { catering.ChangeTracker.Clear(); return new(rejected.Failure); }
        catch (DbUpdateConcurrencyException) { catering.ChangeTracker.Clear(); return new(UpdateCampMealsFailure.Changed); }
        catch (DbUpdateException exception) when (exception.InnerException is
            Microsoft.Data.Sqlite.SqliteException { SqliteExtendedErrorCode: 787 } or
            Npgsql.PostgresException { SqlState: "23503" })
        {
            // A competing reference writer won. The audited transaction has rolled back;
            // never remove that reference or leave a partially deleted schedule.
            catering.ChangeTracker.Clear(); return new(UpdateCampMealsFailure.InUse);
        }
        catch { catering.ChangeTracker.Clear(); throw; }
    }

    private async Task<bool> IsMealUsedAsync(Guid campId, Guid mealId, CancellationToken ct)
    {
        if (await catering.MealPlanOfferGroups.AnyAsync(x => x.CampMealId == mealId, ct) ||
            await catering.CookingUnitMealStates.AnyAsync(x => x.CampMealId == mealId, ct) ||
            await catering.CookingUnitStructureAssignments.AnyAsync(x => x.CampMealId == mealId, ct) ||
            await camps.Participants.AnyAsync(x => x.CampId == campId && x.AbsentMeals.Any(m => m.MealId == mealId), ct)) return true;
        var json = await catering.Set<MealPlanningParticipantConfiguration>().Where(x => x.CampId == campId)
            .Select(x => x.AssignmentsJson).SingleOrDefaultAsync(ct);
        return json is not null && (JsonSerializer.Deserialize<CookingUnitParticipantAssignments[]>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []).Any(x => x.MealOverrides.Any(m => m.CampMealId == mealId));
    }

    public Task<MealScheduleMutation> AddAdditionalMealAsync(Guid actor, Guid tenantId, Guid campId,
        DateOnly start, DateOnly end, bool frozen, AddAdditionalMealRequest request, CancellationToken ct = default) =>
        MutateScheduleAsync(actor, tenantId, campId, frozen, request.ExpectedScheduleVersion, "camp.meal.additional-created", async token =>
        {
            string name = request.Name?.Trim() ?? "";
            var period = await camps.Camps.AsNoTracking().SingleAsync(x => x.Id == campId, token);
            Require(request.ExpectedScheduleVersion.HasValue && request.Date >= period.StartDate && request.Date <= period.EndDate && name.Length is > 0 and <= 100);
            string normalized = name.ToUpperInvariant();
            var types = await catering.CampMealTypes.Where(x => x.CampId == campId).ToListAsync(token);
            var type = types.SingleOrDefault(x => x.NormalizedName == normalized);
            Require(type is null || type.Kind == CampMealKind.Additional, UpdateCampMealsFailure.NameConflict);
            if (type is null)
            {
                type = new(Guid.NewGuid(), campId, name, types.Select(x => x.SortOrder).DefaultIfEmpty(-1).Max() + 1, CampMealKind.Additional);
                catering.Add(type);
            }
            Require(!await catering.CampMeals.AnyAsync(x => x.CampId == campId && x.Date == request.Date && x.MealTypeId == type.Id, token),
                UpdateCampMealsFailure.AlreadyExists);
            int order = await catering.CampMeals.Where(x => x.CampId == campId && x.Date == request.Date)
                .Select(x => (int?)x.SortOrder).MaxAsync(token) ?? -1;
            var meal = new CampMeal(Guid.NewGuid(), campId, type.Id, request.Date, sortOrder: order + 1);
            catering.Add(meal);
            return new(UpdateCampMealsFailure.None, Meal: new(meal.Id, type.Id, type.Name, meal.Date, true, meal.SortOrder, 0, type.Kind));
        }, ct);

    public Task<MealScheduleMutation> OrderDayMealsAsync(Guid actor, Guid tenantId, Guid campId, DateOnly date,
        DateOnly start, DateOnly end, bool frozen, OrderDayMealsRequest request, CancellationToken ct = default) =>
        MutateScheduleAsync(actor, tenantId, campId, frozen, request.ExpectedScheduleVersion, "camp.meals.reordered", async token =>
        {
            var period = await camps.Camps.AsNoTracking().SingleAsync(x => x.Id == campId, token);
            Require(request.ExpectedScheduleVersion.HasValue && date >= period.StartDate && date <= period.EndDate && request.MealIds is not null);
            var meals = await catering.CampMeals.Where(x => x.CampId == campId && x.Date == date).ToListAsync(token);
            Require(request.MealIds!.Count == meals.Count && request.MealIds.Distinct().Count() == meals.Count &&
                request.MealIds.All(id => meals.Any(x => x.Id == id)));
            for (int index = 0; index < request.MealIds.Count; index++) meals.Single(x => x.Id == request.MealIds[index]).SetSortOrder(index);
            return new(UpdateCampMealsFailure.None);
        }, ct);

    public Task<MealScheduleMutation> RemoveAdditionalMealAsync(Guid actor, Guid tenantId, Guid campId, Guid mealId,
        bool frozen, long expectedVersion, CancellationToken ct = default) =>
        MutateScheduleAsync(actor, tenantId, campId, frozen, expectedVersion, "camp.meal.additional-removed", async token =>
        {
            var meal = await catering.CampMeals.SingleOrDefaultAsync(x => x.CampId == campId && x.Id == mealId, token);
            Require(meal is not null, UpdateCampMealsFailure.NotFound);
            var type = await catering.CampMealTypes.SingleAsync(x => x.CampId == campId && x.Id == meal!.MealTypeId, token);
            Require(type.Kind == CampMealKind.Additional);
            if (await IsMealUsedAsync(campId, mealId, token))
            {
                meal!.SetActive(false);
                return new(UpdateCampMealsFailure.None, Outcome: "Deactivated");
            }
            catering.Remove(meal!);
            // Keep unused type identities: they remain reusable and never generate regular slots.
            return new(UpdateCampMealsFailure.None, Outcome: "Deleted");
        }, ct);

    public async Task<UpdateCampMealsFailure> SetMealActivityAsync(Guid actorUserId, Guid tenantId, Guid campId,
        Guid mealId, bool frozen, UpdateCampMealActivityRequest request, CancellationToken cancellationToken = default)
    {
        var result = await MutateScheduleAsync(actorUserId, tenantId, campId, frozen, request.ExpectedScheduleVersion,
            "camp.meal-activity.updated", async token =>
            {
                var meal = await catering.CampMeals.SingleOrDefaultAsync(x => x.CampId == campId && x.Id == mealId, token);
                Require(meal is not null, UpdateCampMealsFailure.NotFound);
                var camp = await camps.Camps.AsNoTracking().SingleAsync(x => x.Id == campId, token);
                Require(!request.IsActive || meal!.Date >= camp.StartDate && meal.Date <= camp.EndDate);
                meal!.SetActive(request.IsActive);
                return new(UpdateCampMealsFailure.None);
            }, cancellationToken);
        return result.Failure;
    }

    public async Task<UpdateCampMealsFailure> UpdateMealTypesAsync(Guid actorUserId, Guid tenantId, Guid campId,
        DateOnly startDate, DateOnly endDate, bool frozen, UpdateCampMealTypesRequest request, CancellationToken cancellationToken = default)
    {
        var result = await MutateScheduleAsync(actorUserId, tenantId, campId, frozen, request.ExpectedScheduleVersion,
            "camp.meal-types.updated", async token =>
            {
                string[] names = request.Names?.Select(x => x?.Trim() ?? "").ToArray() ?? [];
                Require(names.Length > 0 && names.All(x => x.Length is > 0 and <= 100) &&
                    names.Select(x => x.ToUpperInvariant()).Distinct().Count() == names.Length);
                var types = await catering.CampMealTypes.Where(x => x.CampId == campId).OrderBy(x => x.SortOrder).ToListAsync(token);
                var meals = await catering.CampMeals.Where(x => x.CampId == campId).ToListAsync(token);
                Require(!types.Any(x => x.Kind == CampMealKind.Additional && names.Any(n => n.ToUpperInvariant() == x.NormalizedName)),
                    UpdateCampMealsFailure.NameConflict);
                var retained = new List<CampMealType>();
                foreach (string name in names)
                {
                    var type = types.SingleOrDefault(x => x.Kind == CampMealKind.Regular && x.NormalizedName == name.ToUpperInvariant());
                    if (type is null) { type = new(Guid.NewGuid(), campId, name, 0); catering.Add(type); }
                    retained.Add(type);
                }
                foreach (var removed in types.Where(x => x.Kind == CampMealKind.Regular && !retained.Contains(x)))
                {
                    foreach (var meal in meals.Where(x => x.MealTypeId == removed.Id))
                        Require(!await IsMealUsedAsync(campId, meal.Id, token), UpdateCampMealsFailure.InUse);
                    catering.CampMeals.RemoveRange(meals.Where(x => x.MealTypeId == removed.Id));
                    catering.Remove(removed);
                }
                // Move persisted type orders out of the final range to avoid unique-index collisions.
                int temporary = types.Select(x => x.SortOrder).DefaultIfEmpty(0).Max() + types.Count + names.Length + 1;
                foreach (var type in types) if (catering.Entry(type).State != EntityState.Deleted) type.Update(type.Name, temporary++);
                foreach (var type in retained.Where(x => !types.Contains(x))) type.Update(type.Name, temporary++);
                await catering.SaveChangesAsync(token);
                var ordered = retained.Concat(types.Where(x => x.Kind == CampMealKind.Additional)).ToArray();
                for (int index = 0; index < ordered.Length; index++) ordered[index].Update(ordered[index].Name, index);
                var period = await camps.Camps.AsNoTracking().SingleAsync(x => x.Id == campId, token);
                for (var date = period.StartDate!.Value; date <= period.EndDate; date = date.AddDays(1))
                {
                    var day = meals.Where(x => x.Date == date && catering.Entry(x).State != EntityState.Deleted).OrderBy(x => x.SortOrder).ToList();
                    foreach (var type in retained.Where(t => !day.Any(x => x.MealTypeId == t.Id)))
                    {
                        int insertion = day.FindIndex(x => types.Any(t => t.Id == x.MealTypeId && t.Kind == CampMealKind.Additional));
                        if (insertion < 0) insertion = day.Count;
                        var meal = new CampMeal(Guid.NewGuid(), campId, type.Id, date);
                        day.Insert(insertion, meal); catering.Add(meal);
                    }
                    for (int index = 0; index < day.Count; index++) day[index].SetSortOrder(index);
                }
                return new(UpdateCampMealsFailure.None);
            }, cancellationToken);
        return result.Failure;
    }
}
