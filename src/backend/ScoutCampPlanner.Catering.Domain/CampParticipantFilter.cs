using System.Text.Json;

namespace ScoutCampPlanner.Catering.Domain;

/// <summary>Camp-owned selection configuration in Catering, not a participant health profile.</summary>
public sealed class CampParticipantFilter
{
    private CampParticipantFilter() { }

    public CampParticipantFilter(Guid id, Guid campId, string name, ParticipantFilterDefinition definition)
    {
        if (id == Guid.Empty || campId == Guid.Empty) throw new ArgumentException("Filter and camp IDs are required.");
        Id = id;
        CampId = campId;
        Update(0, name, definition);
    }

    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string DefinitionJson { get; private set; } = string.Empty;

    public ParticipantFilterDefinition ReadDefinition()
    {
        var definition = JsonSerializer.Deserialize<ParticipantFilterDefinition>(DefinitionJson)
            ?? throw new ArgumentException("Missing participant filter definition.");
        definition.Validate();
        return definition;
    }

    // False means only metadata changed: callers must not use Version as a calculation dependency.
    public bool Update(int expectedVersion, string name, ParticipantFilterDefinition definition)
    {
        if (expectedVersion != Version) throw new InvalidOperationException("Participant filter version conflict.");
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 200) throw new ArgumentException("Invalid filter name.");
        ArgumentNullException.ThrowIfNull(definition);
        var key = definition.CanonicalKey();
        bool selectionChanged = Version == 0 || ReadDefinition().CanonicalKey() != key;
        var json = JsonSerializer.Serialize(definition);
        int nextVersion = checked(Version + 1);
        Name = trimmed;
        DefinitionJson = json;
        Version = nextVersion;
        return selectionChanged;
    }
}
