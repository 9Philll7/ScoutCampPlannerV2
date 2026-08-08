namespace ScoutCampPlanner.Platform.Domain;

public sealed class Tenant
{
    private Tenant() { }

    public Tenant(Guid id, string name)
    {
        if (id == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tenant name is required.", nameof(name));
        Id = id;
        Name = name.Trim();
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
