namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class AuditSegmentRecord
{
    private AuditSegmentRecord() { }

    public Guid InstanceId { get; private set; }
    public Guid SegmentId { get; private set; }
    public long FirstSequence { get; private set; }
    public long? LastSequence { get; private set; }
    public DateTimeOffset OpenedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public string KeyId { get; private set; } = null!;
    public int FormatVersion { get; private set; }
    public byte[] FirstPredecessorHash { get; private set; } = null!;
    public byte[]? ClosingHash { get; private set; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }
    public DateTimeOffset? EventsDeletedAtUtc { get; private set; }
}
