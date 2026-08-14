using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Application.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class AuditJournalAppender(PlatformDbContext database, IAuditSigningKeyProvider keys)
    : IAuditJournalAppender
{
    private static readonly SemaphoreSlim SqliteGate = new(1, 1);

    public async Task<AuditAppendReceipt> AppendAsync(
        AuditEventDraft auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        string providerName = database.Database.ProviderName ?? throw new InvalidOperationException("Database provider is unavailable.");
        bool sqlite = providerName.Contains("Sqlite", StringComparison.Ordinal);
        if (sqlite) await SqliteGate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            AuditJournalHead head = providerName.Contains("Npgsql", StringComparison.Ordinal)
                ? await database.AuditJournalHeads.FromSqlInterpolated(
                    $"SELECT * FROM platform.\"AuditJournalHeads\" WHERE \"InstanceId\" = {auditEvent.InstanceId} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Audit journal is not initialized.")
                : await database.AuditJournalHeads.SingleOrDefaultAsync(
                    value => value.InstanceId == auditEvent.InstanceId, cancellationToken)
                    ?? throw new InvalidOperationException("Audit journal is not initialized.");

            using AuditSigningKey key = await keys.GetActiveAsync(cancellationToken);
            if (head.KeyId != key.Id || head.FormatVersion != AuditCanonicalEncoding.CurrentFormatVersion)
                throw new InvalidOperationException("Audit head and active key state do not match.");

            long sequence = head.Sequence + 1;
            byte[] canonical = AuditCanonicalEncoding.Encode(sequence, head.Head, key.Id, auditEvent);
            byte[] hmac = HMACSHA256.HashData(key.Material, canonical);
            database.AuditEvents.Add(new AuditEventRecord(
                auditEvent, sequence, head.ActiveSegmentId, AuditCanonicalEncoding.EncodeMetadata(auditEvent.Metadata),
                head.Head, hmac, key.Id, AuditCanonicalEncoding.CurrentFormatVersion));
            head.Advance(sequence, hmac);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(auditEvent.InstanceId, auditEvent.EventId, sequence, head.ActiveSegmentId, hmac);
        }
        finally
        {
            if (sqlite) SqliteGate.Release();
        }
    }
}
