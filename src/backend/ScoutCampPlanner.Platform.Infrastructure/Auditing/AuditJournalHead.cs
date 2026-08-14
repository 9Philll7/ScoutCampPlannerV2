namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class AuditJournalHead
{
    private AuditJournalHead() { }

    internal AuditJournalHead(Guid instanceId, Guid segmentId, string keyId, int formatVersion)
    {
        InstanceId = instanceId;
        Sequence = 0;
        Head = new byte[32];
        KeyId = keyId;
        FormatVersion = formatVersion;
        ActiveSegmentId = segmentId;
    }

    internal void Advance(long sequence, byte[] head)
    {
        if (sequence != Sequence + 1) throw new InvalidOperationException("Audit sequence is not contiguous.");
        Sequence = sequence;
        Head = head.ToArray();
    }

    public Guid InstanceId { get; private set; }
    public long Sequence { get; private set; }
    public byte[] Head { get; private set; } = null!;
    public string KeyId { get; private set; } = null!;
    public int FormatVersion { get; private set; }
    public Guid ActiveSegmentId { get; private set; }
}
