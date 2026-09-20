namespace ScoutCampPlanner.Camp.Contracts;

public sealed record CampReference(Guid Id, Guid TenantId, string Name, bool IsFrozen);

public interface ICampLookup
{
    Task<CampReference?> FindAsync(Guid campId, CancellationToken cancellationToken = default);
}

public sealed record CampPlanningNode(Guid Id, Guid? ParentId, string Name);

public sealed record CampPlanningStage(Guid Id, string Name, int SortOrder);

public sealed record CampPlanningEstimate(
    Guid StructureNodeId,
    Guid CampStageId,
    int ChildYouthCount,
    int LeaderCount);

public sealed record CampPlanningData(
    Guid CampId,
    Guid TenantId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<CampPlanningNode> Nodes,
    IReadOnlyList<CampPlanningStage> Stages,
    IReadOnlyList<CampPlanningEstimate> Estimates);

public interface ICampPlanningLookup
{
    Task<CampPlanningData?> GetPlanningDataAsync(
        Guid campId,
        CancellationToken cancellationToken = default);
}
