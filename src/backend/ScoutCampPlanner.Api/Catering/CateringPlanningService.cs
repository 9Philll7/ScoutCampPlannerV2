using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Api.Catering;

public sealed record TenantStageFoodFactorSummary(string StageName, decimal Factor);
public sealed record UpdateTenantStageFoodFactorsRequest(IReadOnlyList<TenantStageFoodFactorSummary>? Factors);
public enum UpdateFoodFactorsFailure { None, Forbidden, InvalidFactors }
public sealed record CampStageReference(Guid Id, string Name);
public sealed record CampStageFoodFactorSummary(Guid CampStageId, string StageName, decimal Factor);
public sealed record UpdateCampStageFoodFactorsRequest(IReadOnlyList<CampStageFoodFactorSummary>? Factors);
public sealed record WeightedStageTotal(Guid CampStageId, string StageName, long ChildYouthCount,
    long LeaderCount, decimal Factor, decimal FoodUnits);
public sealed record CampMealTypeSummary(Guid Id, string Name, int SortOrder);
public sealed record CampMealSummary(Guid Id, Guid MealTypeId, string MealTypeName, DateOnly Date, bool IsActive);
public sealed record CampMealPlanSummary(IReadOnlyList<CampMealTypeSummary> MealTypes, IReadOnlyList<CampMealSummary> Meals);
public sealed record UpdateCampMealTypesRequest(IReadOnlyList<string>? Names);
public sealed record UpdateCampMealActivityRequest(bool IsActive);
public enum UpdateCampMealsFailure { None, Invalid, Frozen }

public sealed class CateringPlanningService(
    PlatformDbContext platform, CateringDbContext catering, IAuditedOperationExecutor auditedOperation,
    AuditRuntimeState auditRuntime, TimeProvider timeProvider)
{
    public async Task<CampMealPlanSummary> GetMealPlanAsync(
        Guid campId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (!await catering.CampMealTypes.AnyAsync(value => value.CampId == campId, cancellationToken))
        {
            string[] defaults = ["Frühstück", "Mittagessen", "Abendessen"];
            var newTypes = defaults.Select((name, index) => new CampMealType(Guid.NewGuid(), campId, name, index)).ToArray();
            catering.CampMealTypes.AddRange(newTypes);
            for (DateOnly date = startDate; date <= endDate; date = date.AddDays(1))
                foreach (var type in newTypes) catering.CampMeals.Add(new CampMeal(Guid.NewGuid(), campId, type.Id, date));
            await catering.SaveChangesAsync(cancellationToken);
        }
        var types = await catering.CampMealTypes.Where(value => value.CampId == campId).OrderBy(value => value.SortOrder)
            .Select(value => new CampMealTypeSummary(value.Id, value.Name, value.SortOrder)).ToListAsync(cancellationToken);
        var names = types.ToDictionary(value => value.Id, value => value.Name);
        var meals = await catering.CampMeals.Where(value => value.CampId == campId).OrderBy(value => value.Date)
            .ThenBy(value => value.MealTypeId).ToListAsync(cancellationToken);
        return new(types, meals.Select(value => new CampMealSummary(value.Id, value.MealTypeId,
            names.GetValueOrDefault(value.MealTypeId, string.Empty), value.Date, value.IsActive)).ToList());
    }

    public async Task<UpdateCampMealsFailure> UpdateMealTypesAsync(
        Guid actorUserId, Guid tenantId, Guid campId, DateOnly startDate, DateOnly endDate, bool frozen,
        UpdateCampMealTypesRequest request, CancellationToken cancellationToken = default)
    {
        if (frozen) return UpdateCampMealsFailure.Frozen;
        string[] names = request.Names?.Select(value => value?.Trim() ?? string.Empty).ToArray() ?? [];
        if (names.Length == 0 || names.Any(value => value.Length is 0 or > 100) ||
            names.Select(value => value.ToUpperInvariant()).Distinct().Count() != names.Length)
            return UpdateCampMealsFailure.Invalid;
        var existing = await catering.CampMealTypes.Where(value => value.CampId == campId).ToListAsync(cancellationToken);
        var byName = existing.ToDictionary(value => value.NormalizedName);
        var retained = new HashSet<Guid>();
        for (int index = 0; index < names.Length; index++)
        {
            string normalized = names[index].ToUpperInvariant();
            if (!byName.TryGetValue(normalized, out var type))
            {
                type = new CampMealType(Guid.NewGuid(), campId, names[index], index);
                catering.CampMealTypes.Add(type);
                for (DateOnly date = startDate; date <= endDate; date = date.AddDays(1))
                    catering.CampMeals.Add(new CampMeal(Guid.NewGuid(), campId, type.Id, date));
            }
            else type.Update(names[index], index);
            retained.Add(type.Id);
        }
        Guid[] removed = existing.Where(value => !retained.Contains(value.Id)).Select(value => value.Id).ToArray();
        if (removed.Length > 0)
        {
            catering.CampMeals.RemoveRange(await catering.CampMeals.Where(value => removed.Contains(value.MealTypeId)).ToListAsync(cancellationToken));
            catering.CampMealTypes.RemoveRange(existing.Where(value => removed.Contains(value.Id)));
        }
        var auditEvent = new AuditEventDraft(Guid.NewGuid(), timeProvider.GetUtcNow(), "camp.meal-types.updated", "success",
            actorUserId, tenantId, campId, "camp-meal-types", campId, "server", auditRuntime.InstanceId,
            Guid.NewGuid(), null, null, new Dictionary<string, string> { ["mealTypeCount"] = names.Length.ToString() });
        await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
        {
            var transaction = platform.Database.CurrentTransaction!;
            await catering.Database.UseTransactionAsync(transaction.GetDbTransaction(), operationCancellationToken);
            try { await catering.SaveChangesAsync(operationCancellationToken); }
            finally { await catering.Database.UseTransactionAsync(null, CancellationToken.None); }
        }, cancellationToken);
        return UpdateCampMealsFailure.None;
    }

    public async Task<UpdateCampMealsFailure> SetMealActivityAsync(
        Guid actorUserId, Guid tenantId, Guid campId, Guid mealId, bool frozen, UpdateCampMealActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (frozen) return UpdateCampMealsFailure.Frozen;
        var meal = await catering.CampMeals.SingleOrDefaultAsync(value => value.Id == mealId && value.CampId == campId, cancellationToken);
        if (meal is null) return UpdateCampMealsFailure.Invalid;
        meal.SetActive(request.IsActive);
        var auditEvent = new AuditEventDraft(Guid.NewGuid(), timeProvider.GetUtcNow(), "camp.meal-activity.updated", "success",
            actorUserId, tenantId, campId, "camp-meal", mealId, "server", auditRuntime.InstanceId,
            Guid.NewGuid(), null, null, new Dictionary<string, string> { ["isActive"] = request.IsActive.ToString() });
        await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
        {
            var transaction = platform.Database.CurrentTransaction!;
            await catering.Database.UseTransactionAsync(transaction.GetDbTransaction(), operationCancellationToken);
            try { await catering.SaveChangesAsync(operationCancellationToken); }
            finally { await catering.Database.UseTransactionAsync(null, CancellationToken.None); }
        }, cancellationToken);
        return UpdateCampMealsFailure.None;
    }

    public async Task<IReadOnlyList<TenantStageFoodFactorSummary>?> GetTenantFactorsAsync(
        Guid actorUserId, Guid tenantId, IReadOnlyList<string> stageNames, CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(actorUserId, tenantId, Permissions.Tenant.View, cancellationToken)) return null;
        var stored = await catering.TenantStageFoodFactors.Where(value => value.TenantId == tenantId)
            .ToDictionaryAsync(value => value.NormalizedStageName, cancellationToken);
        return stageNames.Select(name => stored.TryGetValue(name.Trim().ToUpperInvariant(), out var factor)
            ? new TenantStageFoodFactorSummary(name, factor.Factor)
            : new TenantStageFoodFactorSummary(name, 1m)).ToList();
    }

    public async Task<UpdateFoodFactorsFailure> UpdateTenantFactorsAsync(
        Guid actorUserId, Guid tenantId, IReadOnlyList<string> stageNames,
        UpdateTenantStageFoodFactorsRequest request, CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(actorUserId, tenantId, Permissions.Tenant.ManageSettings, cancellationToken))
            return UpdateFoodFactorsFailure.Forbidden;
        var factors = request.Factors?.ToArray() ?? [];
        var expected = stageNames.Select(value => value.Trim().ToUpperInvariant()).ToHashSet();
        if (factors.Length != expected.Count || factors.Select(value => value.StageName.Trim().ToUpperInvariant()).Distinct().Count() != factors.Length ||
            factors.Any(value => !expected.Contains(value.StageName.Trim().ToUpperInvariant()) || value.Factor < 0.1m ||
                value.Factor > 3m || decimal.Round(value.Factor, 2) != value.Factor))
            return UpdateFoodFactorsFailure.InvalidFactors;
        var entities = factors.Select(value => new TenantStageFoodFactor(
            Guid.NewGuid(), tenantId, value.StageName, value.Factor)).ToArray();
        var auditEvent = new AuditEventDraft(Guid.NewGuid(), timeProvider.GetUtcNow(),
            "tenant.catering-stage-factors.updated", "success", actorUserId, tenantId, null,
            "tenant-catering-stage-factors", tenantId, "server", auditRuntime.InstanceId,
            Guid.NewGuid(), null, null, new Dictionary<string, string> { ["stageCount"] = entities.Length.ToString() });
        await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
        {
            var transaction = platform.Database.CurrentTransaction!;
            await catering.Database.UseTransactionAsync(transaction.GetDbTransaction(), operationCancellationToken);
            try
            {
                await catering.TenantStageFoodFactors.Where(value => value.TenantId == tenantId)
                    .ExecuteDeleteAsync(operationCancellationToken);
                catering.TenantStageFoodFactors.AddRange(entities); await catering.SaveChangesAsync(operationCancellationToken);
            }
            finally { await catering.Database.UseTransactionAsync(null, CancellationToken.None); }
        }, cancellationToken);
        return UpdateFoodFactorsFailure.None;
    }

    public async Task<IReadOnlyList<CampStageFoodFactorSummary>> GetCampFactorsAsync(
        Guid tenantId, Guid campId, IReadOnlyList<CampStageReference> stages, CancellationToken cancellationToken = default)
    {
        var stored = await catering.CampStageFoodFactors.Where(value => value.CampId == campId)
            .ToDictionaryAsync(value => value.CampStageId, cancellationToken);
        if (stored.Count > 0) return stages.Select(stage => stored.TryGetValue(stage.Id, out var factor)
            ? new CampStageFoodFactorSummary(stage.Id, stage.Name, factor.Factor)
            : new CampStageFoodFactorSummary(stage.Id, stage.Name, 1m)).ToList();
        var tenantFactors = await catering.TenantStageFoodFactors.Where(value => value.TenantId == tenantId)
            .ToDictionaryAsync(value => value.NormalizedStageName, cancellationToken);
        return stages.Select(stage => tenantFactors.TryGetValue(stage.Name.Trim().ToUpperInvariant(), out var factor)
            ? new CampStageFoodFactorSummary(stage.Id, stage.Name, factor.Factor)
            : new CampStageFoodFactorSummary(stage.Id, stage.Name, 1m)).ToList();
    }

    public async Task<UpdateFoodFactorsFailure> UpdateCampFactorsAsync(
        Guid actorUserId, Guid tenantId, Guid campId, IReadOnlyList<CampStageReference> stages,
        UpdateCampStageFoodFactorsRequest request, CancellationToken cancellationToken = default)
    {
        var factors = request.Factors?.ToArray() ?? [];
        var stageIds = stages.Select(value => value.Id).ToHashSet();
        if (factors.Length != stageIds.Count || factors.Select(value => value.CampStageId).Distinct().Count() != factors.Length ||
            factors.Any(value => !stageIds.Contains(value.CampStageId) || value.Factor < 0.1m || value.Factor > 3m ||
                decimal.Round(value.Factor, 2) != value.Factor)) return UpdateFoodFactorsFailure.InvalidFactors;
        var entities = factors.Select(value => new CampStageFoodFactor(Guid.NewGuid(), campId,
            value.CampStageId, value.StageName, value.Factor)).ToArray();
        var auditEvent = new AuditEventDraft(Guid.NewGuid(), timeProvider.GetUtcNow(),
            "camp.catering-stage-factors.updated", "success", actorUserId, tenantId, campId,
            "camp-catering-stage-factors", campId, "server", auditRuntime.InstanceId,
            Guid.NewGuid(), null, null, new Dictionary<string, string> { ["stageCount"] = entities.Length.ToString() });
        await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
        {
            var transaction = platform.Database.CurrentTransaction!;
            await catering.Database.UseTransactionAsync(transaction.GetDbTransaction(), operationCancellationToken);
            try
            {
                await catering.CampStageFoodFactors.Where(value => value.CampId == campId)
                    .ExecuteDeleteAsync(operationCancellationToken);
                catering.CampStageFoodFactors.AddRange(entities); await catering.SaveChangesAsync(operationCancellationToken);
            }
            finally { await catering.Database.UseTransactionAsync(null, CancellationToken.None); }
        }, cancellationToken);
        return UpdateFoodFactorsFailure.None;
    }

    public static IReadOnlyList<WeightedStageTotal> CalculateWeightedTotals(
        CampPlanningSummary summary, IReadOnlyList<CampStageFoodFactorSummary> factors)
    {
        var byStage = factors.ToDictionary(value => value.CampStageId);
        return summary.StageTotals.Select(total =>
        {
            decimal factor = byStage.GetValueOrDefault(total.CampStageId)?.Factor ?? 1m;
            return new WeightedStageTotal(total.CampStageId, total.StageName, total.ChildYouthCount,
                total.LeaderCount, factor, total.ChildYouthCount * factor + total.LeaderCount);
        }).ToList();
    }

    private async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken cancellationToken)
    {
        string[] roles = await platform.TenantMemberships.Where(value => value.UserId == userId &&
                value.TenantId == tenantId && value.State == TenantMembershipState.Active)
            .Join(platform.TenantRoleAssignments, membership => membership.Id, role => role.MembershipId,
                (_, role) => role.RoleIdentifier).ToArrayAsync(cancellationToken);
        return TenantRoleSetValidator.Validate(roles).IsValid &&
            AuthorizationCatalogue.ResolvePermissions(AuthorizationScope.Tenant, roles).Contains(permission);
    }
}
