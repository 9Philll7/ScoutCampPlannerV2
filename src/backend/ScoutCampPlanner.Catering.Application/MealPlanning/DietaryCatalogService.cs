using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record DietaryOriginRuleData(Guid OriginId, DietaryOriginDecision Decision);
public sealed record DietaryTypeDocument(Guid Id, Guid? TenantId, string Name, string? Description,
    int SortOrder, int Version, IReadOnlyList<DietaryOriginRuleData> Rules);
public sealed record SaveDietaryTypeRequest(string Name, string? Description, int SortOrder,
    int ExpectedVersion, IReadOnlyList<DietaryOriginRuleData> Rules);
public sealed record DietaryContributionDocument(Guid Id, DietaryTypeDocument Submitted,
    Guid SubmittedBy, DateTimeOffset SubmittedAtUtc, int Status, Guid? CentralId);
public enum DietaryMutationStatus { Success, NotFound, Invalid, Conflict, Forbidden }
public sealed record DietaryMutationResult(DietaryMutationStatus Status, Guid? Id = null);
public sealed record SubstanceThresholdDefaultRequest(decimal? GramsPerPortion, string? Source, int ExpectedVersion);

public interface IDietaryCatalogStore
{
    Task<IReadOnlyList<DietaryTypeDocument>> ListAsync(Guid? tenantId, CancellationToken ct);
    Task<DietaryMutationResult> SaveAsync(Guid id, Guid? tenantId, SaveDietaryTypeRequest request, CancellationToken ct);
    Task<DietaryMutationResult> SubmitAsync(Guid id, Guid tenantId, int version, Guid actor, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<DietaryContributionDocument>> ContributionsAsync(CancellationToken ct);
    Task<DietaryMutationResult> ReviewAsync(Guid id, bool accept, Guid? targetCentralId, Guid actor, DateTimeOffset now, CancellationToken ct);
    Task<DietaryMutationResult> SetThresholdDefaultAsync(Guid substanceId, SubstanceThresholdDefaultRequest request, CancellationToken ct);
}

/// <summary>Uses the existing central/tenant master-data administration boundary, not health grants.</summary>
public sealed class DietaryCatalogService(IDietaryCatalogStore store, IIngredientManagementAuthorization authorization, TimeProvider clock)
{
    public async Task<IReadOnlyList<DietaryTypeDocument>?> ListAsync(Guid actor, Guid? tenantId, CancellationToken ct) =>
        await CanManageAsync(actor, tenantId, ct) ? await store.ListAsync(tenantId, ct) : null;
    public async Task<DietaryMutationResult> SaveAsync(Guid actor, Guid? tenantId, Guid id, SaveDietaryTypeRequest request, CancellationToken ct) =>
        await CanManageAsync(actor, tenantId, ct) ? await store.SaveAsync(id, tenantId, request, ct) : new(DietaryMutationStatus.Forbidden);
    public async Task<DietaryMutationResult> SubmitAsync(Guid actor, Guid tenantId, Guid id, int version, CancellationToken ct) =>
        await CanManageAsync(actor, tenantId, ct) ? await store.SubmitAsync(id, tenantId, version, actor, clock.GetUtcNow(), ct) : new(DietaryMutationStatus.Forbidden);
    public async Task<IReadOnlyList<DietaryContributionDocument>?> ContributionsAsync(Guid actor, CancellationToken ct) =>
        await authorization.CanManageCentralAsync(actor, ct) ? await store.ContributionsAsync(ct) : null;
    public async Task<DietaryMutationResult> ReviewAsync(Guid actor, Guid id, bool accept, Guid? targetCentralId, CancellationToken ct) =>
        await authorization.CanManageCentralAsync(actor, ct) ? await store.ReviewAsync(id, accept, targetCentralId, actor, clock.GetUtcNow(), ct) : new(DietaryMutationStatus.Forbidden);
    public async Task<DietaryMutationResult> SetThresholdDefaultAsync(Guid actor, Guid substanceId, SubstanceThresholdDefaultRequest request, CancellationToken ct) =>
        await authorization.CanManageCentralAsync(actor, ct) ? await store.SetThresholdDefaultAsync(substanceId, request, ct) : new(DietaryMutationStatus.Forbidden);
    private Task<bool> CanManageAsync(Guid actor, Guid? tenant, CancellationToken ct) => tenant.HasValue
        ? authorization.CanManageTenantAsync(actor, tenant.Value, ct) : authorization.CanManageCentralAsync(actor, ct);
}
