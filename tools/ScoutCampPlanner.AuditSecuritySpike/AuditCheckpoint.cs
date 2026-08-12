using System.Security.Cryptography;

namespace ScoutCampPlanner.AuditSecuritySpike;

public sealed record AuditCheckpoint(
    Guid InstanceId,
    long Sequence,
    byte[] Head,
    string KeyId,
    int FormatVersion);

public enum CheckpointReconciliationStatus
{
    Current,
    AdvanceRequired,
    Invalid,
}

public sealed record CheckpointReconciliation(
    CheckpointReconciliationStatus Status,
    AuditCheckpoint? Checkpoint,
    string? Failure);

public static class AuditCheckpointReconciler
{
    public static CheckpointReconciliation Reconcile(
        AuditCheckpoint externalCheckpoint,
        IReadOnlyList<AuditChainEntry> databaseEntries,
        AuditCheckpoint databaseHead,
        Func<string, byte[]?> resolveKey)
    {
        ArgumentNullException.ThrowIfNull(externalCheckpoint);
        ArgumentNullException.ThrowIfNull(databaseEntries);
        ArgumentNullException.ThrowIfNull(databaseHead);

        if (externalCheckpoint.InstanceId != databaseHead.InstanceId)
            return Invalid("instance-mismatch");
        if (externalCheckpoint.FormatVersion != databaseHead.FormatVersion)
            return Invalid("format-mismatch");
        if (externalCheckpoint.Sequence > databaseHead.Sequence)
            return Invalid("checkpoint-ahead-of-database");

        if (externalCheckpoint.Sequence == databaseHead.Sequence)
        {
            return CryptographicOperations.FixedTimeEquals(externalCheckpoint.Head, databaseHead.Head)
                ? new CheckpointReconciliation(CheckpointReconciliationStatus.Current, externalCheckpoint, null)
                : Invalid("checkpoint-head-mismatch");
        }

        var suffix = databaseEntries
            .Where(entry => entry.Sequence > externalCheckpoint.Sequence)
            .OrderBy(entry => entry.Sequence)
            .ToArray();
        if (suffix.Length == 0 || suffix[0].Sequence != externalCheckpoint.Sequence + 1)
            return Invalid("database-suffix-missing");

        var verification = AuditHmacChain.Verify(
            suffix,
            externalCheckpoint.Head,
            databaseHead.Head,
            resolveKey);
        return verification.IsValid
            ? new CheckpointReconciliation(CheckpointReconciliationStatus.AdvanceRequired, databaseHead, null)
            : Invalid(verification.Failure ?? "database-suffix-invalid");
    }

    private static CheckpointReconciliation Invalid(string failure) =>
        new(CheckpointReconciliationStatus.Invalid, null, failure);
}
