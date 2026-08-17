namespace ScoutCampPlanner.Camp.Domain;

public enum CampStructureMode
{
    Free = 0,
    Fixed = 1,
}

public sealed class StructureNode
{
    private StructureNode() { }

    public StructureNode(Guid id, Guid campId, Guid? parentId, string name)
    {
        if (id == Guid.Empty) throw new ArgumentException("Structure node ID is required.", nameof(id));
        if (campId == Guid.Empty) throw new ArgumentException("Camp ID is required.", nameof(campId));
        if (parentId == Guid.Empty) throw new ArgumentException("Parent ID must be null or non-empty.", nameof(parentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string trimmedName = name.Trim();
        if (trimmedName.Length > 200)
            throw new ArgumentException("Structure node name must not exceed 200 characters.", nameof(name));

        Id = id;
        CampId = campId;
        ParentId = parentId;
        Name = trimmedName;
        NormalizedName = trimmedName.ToUpperInvariant();
    }

    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
}
