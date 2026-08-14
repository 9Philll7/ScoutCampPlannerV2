using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Authentication;

public sealed class SessionTerminationService(
    PlatformDbContext database,
    IAuditedOperationExecutor auditedOperation,
    AuditRuntimeState auditRuntime,
    TimeProvider timeProvider) : ISessionTerminationService
{
    public async Task SignOutAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventDraft(
            Guid.NewGuid(), timeProvider.GetUtcNow(), "authentication.sign-out", "success",
            userId, null, null, "authentication-session", sessionId, "server", auditRuntime.InstanceId,
            Guid.NewGuid(), null, null, new Dictionary<string, string>());
        await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
        {
            await database.AuthenticationSessions.Where(value => value.Id == sessionId && value.UserId == userId)
                .ExecuteDeleteAsync(operationCancellationToken);
        }, cancellationToken);
    }
}
