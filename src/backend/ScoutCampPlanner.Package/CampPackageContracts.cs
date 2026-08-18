using System.Text.Json.Serialization;

namespace ScoutCampPlanner.Package;

public static class CampPackageVersions
{
    public const int Current = 1;
}

[JsonConverter(typeof(JsonStringEnumConverter<CampPackageDirection>))]
public enum CampPackageDirection
{
    CloudToLocal,
    LocalToCloud
}

public sealed record CampPackageManifest(
    int FormatVersion,
    Guid TenantId,
    Guid CampId,
    Guid TransferId,
    long BaselineVersion,
    CampPackageDirection Direction,
    IReadOnlyList<string> IncludedModules,
    DateTimeOffset CreatedAtUtc);

public sealed record CampPackagePayload(
    CampPackageManifest Manifest,
    TenantData Tenant,
    CampData Camp,
    IReadOnlyList<CampStageData> CampStages,
    IReadOnlyList<ParticipantEstimateData> ParticipantEstimates,
    IReadOnlyList<StructureNodeData> StructureNodes,
    IReadOnlyList<MealPlanData> MealPlans);

public sealed record TenantData(Guid Id, string Name);
public sealed record CampData(
    Guid Id, Guid TenantId, string Name, DateOnly StartDate, DateOnly EndDate, string StructureMode,
    IReadOnlyList<string> StructureLevelNames);
public sealed record StructureNodeData(Guid Id, Guid CampId, Guid? ParentId, string Name);
public sealed record CampStageData(Guid Id, Guid CampId, string Name, int SortOrder);
public sealed record ParticipantEstimateData(Guid Id, Guid CampId, Guid StructureNodeId, Guid CampStageId,
    int ChildYouthCount, int LeaderCount);
public sealed record MealPlanData(Guid Id, Guid CampId, string Name);

public sealed class CampPackageValidationException(string message) : Exception(message);
