namespace ScoutCampPlanner.Platform.Application.Auditing;

public sealed record AuditEventDraft(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    string Action,
    string Result,
    Guid? ActorUserId,
    Guid? TenantId,
    Guid? CampId,
    string? TargetType,
    Guid? TargetId,
    string Origin,
    Guid InstanceId,
    Guid CorrelationId,
    int? SecurityVersion,
    int? RoleDefinitionVersion,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record AuditAppendReceipt(
    Guid InstanceId,
    Guid EventId,
    long Sequence,
    Guid SegmentId,
    byte[] Head);

public interface IAuditJournalAppender
{
    Task<AuditAppendReceipt> AppendAsync(
        AuditEventDraft auditEvent,
        CancellationToken cancellationToken = default);
}
