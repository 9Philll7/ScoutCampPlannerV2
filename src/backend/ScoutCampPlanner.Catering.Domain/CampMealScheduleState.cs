namespace ScoutCampPlanner.Catering.Domain;

public sealed class CampMealScheduleState
{
    private CampMealScheduleState() { }
    public CampMealScheduleState(Guid campId, long version = 0)
    {
        CampId = campId != Guid.Empty ? campId : throw new ArgumentException("Camp ID is required.");
        Version = version >= 0 ? version : throw new ArgumentOutOfRangeException(nameof(version));
    }
    public Guid CampId { get; private set; }
    public long Version { get; private set; }
    public void Advance() => Version = checked(Version + 1);
}
