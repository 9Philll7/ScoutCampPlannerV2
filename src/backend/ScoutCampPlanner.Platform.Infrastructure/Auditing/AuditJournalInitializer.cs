using Microsoft.EntityFrameworkCore;

namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class AuditJournalInitializer(PlatformDbContext database, IAuditSigningKeyProvider keys)
{
    public async Task InitializeAsync(
        Guid instanceId,
        Guid initialSegmentId,
        DateTimeOffset openedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (instanceId == Guid.Empty || initialSegmentId == Guid.Empty || openedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Audit journal initialization values are invalid.");
        if (await database.AuditJournalHeads.AnyAsync(x => x.InstanceId == instanceId, cancellationToken)) return;

        using AuditSigningKey key = await keys.GetActiveAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.AuditSegments.Add(new AuditSegmentRecord(
            instanceId, initialSegmentId, openedAtUtc, key.Id, AuditCanonicalEncoding.CurrentFormatVersion));
        database.AuditJournalHeads.Add(new AuditJournalHead(
            instanceId, initialSegmentId, key.Id, AuditCanonicalEncoding.CurrentFormatVersion));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
