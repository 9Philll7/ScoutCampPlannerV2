namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class AuditJournalHead
{
    private AuditJournalHead() { }

    public Guid InstanceId { get; private set; }
    public long Sequence { get; private set; }
    public byte[] Head { get; private set; } = null!;
    public string KeyId { get; private set; } = null!;
    public int FormatVersion { get; private set; }
    public Guid ActiveSegmentId { get; private set; }
}
