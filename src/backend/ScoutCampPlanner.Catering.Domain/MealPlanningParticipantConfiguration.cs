namespace ScoutCampPlanner.Catering.Domain;

/// <summary>Camp-scoped planning configuration, not participant master data.</summary>
public sealed class MealPlanningParticipantConfiguration
{
    private MealPlanningParticipantConfiguration() { }
    public MealPlanningParticipantConfiguration(Guid campId)
    {
        CampId = campId == Guid.Empty ? throw new ArgumentException("Camp ID is required.", nameof(campId)) : campId;
    }
    public Guid CampId { get; private set; }
    public int Version { get; private set; }
    public MealPlanningDemandMode DemandMode { get; private set; }
    /// <summary>Legacy development migration residue only. Never used as operative assignments.</summary>
    public string AssignmentsJson { get; private set; } = "[]";
    public void Update(MealPlanningDemandMode mode, string assignmentsJson)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        ArgumentException.ThrowIfNullOrWhiteSpace(assignmentsJson);
        DemandMode = mode; AssignmentsJson = assignmentsJson; Version = checked(Version + 1);
    }
    public static MealPlanningParticipantConfiguration Restore(Guid campId, int version, MealPlanningDemandMode mode, string json)
    {
        if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
        var value = new MealPlanningParticipantConfiguration(campId); value.Update(mode, json); value.Version = version; return value;
    }
}
