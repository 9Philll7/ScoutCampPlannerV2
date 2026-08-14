using ScoutCampPlanner.Platform.Application.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class AuditEventRecord
{
    private AuditEventRecord() { }

    internal AuditEventRecord(
        AuditEventDraft source,
        long sequence,
        Guid segmentId,
        string metadataJson,
        byte[] previousHash,
        byte[] hmac,
        string keyId,
        int formatVersion)
    {
        InstanceId = source.InstanceId;
        Sequence = sequence;
        EventId = source.EventId;
        SegmentId = segmentId;
        TimestampUtc = source.TimestampUtc;
        Action = source.Action;
        Result = source.Result;
        ActorUserId = source.ActorUserId;
        TenantId = source.TenantId;
        CampId = source.CampId;
        TargetType = source.TargetType;
        TargetId = source.TargetId;
        Origin = source.Origin;
        CorrelationId = source.CorrelationId;
        SecurityVersion = source.SecurityVersion;
        RoleDefinitionVersion = source.RoleDefinitionVersion;
        MetadataJson = metadataJson;
        PreviousHash = previousHash.ToArray();
        Hmac = hmac.ToArray();
        KeyId = keyId;
        FormatVersion = formatVersion;
    }

    public Guid InstanceId { get; private set; }
    public long Sequence { get; private set; }
    public Guid EventId { get; private set; }
    public Guid SegmentId { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }
    public string Action { get; private set; } = null!;
    public string Result { get; private set; } = null!;
    public Guid? ActorUserId { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? CampId { get; private set; }
    public string? TargetType { get; private set; }
    public Guid? TargetId { get; private set; }
    public string Origin { get; private set; } = null!;
    public Guid CorrelationId { get; private set; }
    public int? SecurityVersion { get; private set; }
    public int? RoleDefinitionVersion { get; private set; }
    public string MetadataJson { get; private set; } = null!;
    public byte[] PreviousHash { get; private set; } = null!;
    public byte[] Hmac { get; private set; } = null!;
    public string KeyId { get; private set; } = null!;
    public int FormatVersion { get; private set; }
}
