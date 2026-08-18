namespace ScoutCampPlanner.Camp.Domain;

public sealed class ParticipantEstimate
{
    private ParticipantEstimate() { }
    public ParticipantEstimate(Guid id, Guid campId, Guid structureNodeId, Guid campStageId,
        int childYouthCount, int leaderCount)
    {
        if (id == Guid.Empty || campId == Guid.Empty || structureNodeId == Guid.Empty || campStageId == Guid.Empty)
            throw new ArgumentException("Estimate identifiers are required.");
        SetCounts(childYouthCount, leaderCount);
        Id = id; CampId = campId; StructureNodeId = structureNodeId; CampStageId = campStageId;
    }
    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public Guid StructureNodeId { get; private set; }
    public Guid CampStageId { get; private set; }
    public int ChildYouthCount { get; private set; }
    public int LeaderCount { get; private set; }
    public void SetCounts(int childYouthCount, int leaderCount)
    {
        if (childYouthCount < 0 || leaderCount < 0) throw new ArgumentOutOfRangeException(nameof(childYouthCount));
        ChildYouthCount = childYouthCount; LeaderCount = leaderCount;
    }
}
