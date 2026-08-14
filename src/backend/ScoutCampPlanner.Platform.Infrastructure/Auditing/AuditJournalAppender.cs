using ScoutCampPlanner.Platform.Application.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class AuditJournalAppender(PlatformDbContext database, IAuditSigningKeyProvider keys)
    : IAuditJournalAppender
{
    public Task<AuditAppendReceipt> AppendAsync(
        AuditEventDraft auditEvent,
        CancellationToken cancellationToken = default) =>
        new AuditedOperationExecutor(database, keys).ExecuteAsync(
            auditEvent, _ => Task.CompletedTask, cancellationToken);
}
