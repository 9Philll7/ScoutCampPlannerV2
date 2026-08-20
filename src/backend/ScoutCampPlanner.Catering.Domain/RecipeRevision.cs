namespace ScoutCampPlanner.Catering.Domain;

public sealed class RecipeRevision
{
    public RecipeRevision(
        Guid id,
        Guid recipeId,
        int revisionNumber,
        DateTimeOffset publishedAtUtc,
        Guid publishedBy,
        int snapshotSchemaVersion,
        string snapshotJson,
        string? changeNote = null,
        Guid? restoredFromRevisionId = null)
    {
        Id = Required(id, nameof(id));
        RecipeId = Required(recipeId, nameof(recipeId));
        RevisionNumber = revisionNumber > 0 ? revisionNumber : throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        PublishedAtUtc = publishedAtUtc;
        PublishedBy = Required(publishedBy, nameof(publishedBy));
        SnapshotSchemaVersion = snapshotSchemaVersion > 0
            ? snapshotSchemaVersion
            : throw new ArgumentOutOfRangeException(nameof(snapshotSchemaVersion));
        SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson)
            ? throw new ArgumentException("A complete revision snapshot is required.", nameof(snapshotJson))
            : snapshotJson;
        ChangeNote = string.IsNullOrWhiteSpace(changeNote) ? null : changeNote.Trim();
        RestoredFromRevisionId = restoredFromRevisionId;
    }

    public Guid Id { get; }
    public Guid RecipeId { get; }
    public int RevisionNumber { get; }
    public DateTimeOffset PublishedAtUtc { get; }
    public Guid PublishedBy { get; }
    public int SnapshotSchemaVersion { get; }
    public string SnapshotJson { get; }
    public string? ChangeNote { get; }
    public Guid? RestoredFromRevisionId { get; }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
