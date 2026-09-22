namespace ScoutCampPlanner.Platform.Domain;

/// <summary>A device-local operator, deliberately separate from cloud user accounts.</summary>
public sealed class LocalDeviceIdentity
{
    private LocalDeviceIdentity() { }

    public LocalDeviceIdentity(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A local identity ID is required.", nameof(id));
        Id = id;
    }

    public Guid Id { get; private set; }
}
