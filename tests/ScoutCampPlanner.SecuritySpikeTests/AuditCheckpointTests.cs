using ScoutCampPlanner.AuditSecuritySpike;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class AuditCheckpointTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    private static readonly Guid InstanceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void ReconciliationAdvancesAfterVerifiedCommittedSuffix()
    {
        var entries = CreateChain(3);
        var external = Checkpoint(1, entries[0].Hmac);
        var database = Checkpoint(3, entries[2].Hmac);

        var result = AuditCheckpointReconciler.Reconcile(external, entries, database, ResolveKey);

        Assert.Equal(CheckpointReconciliationStatus.AdvanceRequired, result.Status);
        Assert.Equal(database, result.Checkpoint);
    }

    [Fact]
    public void ReconciliationIsIdempotentWhenCheckpointIsCurrent()
    {
        var entries = CreateChain(2);
        var checkpoint = Checkpoint(2, entries[1].Hmac);

        var result = AuditCheckpointReconciler.Reconcile(checkpoint, entries, checkpoint, ResolveKey);

        Assert.Equal(CheckpointReconciliationStatus.Current, result.Status);
    }

    [Theory]
    [InlineData("ahead")]
    [InlineData("wrong-head")]
    [InlineData("missing-suffix")]
    [InlineData("modified-suffix")]
    public void ReconciliationRejectsUnsafeState(string mutation)
    {
        var entries = CreateChain(3).ToList();
        var external = Checkpoint(1, entries[0].Hmac);
        var database = Checkpoint(3, entries[2].Hmac);

        switch (mutation)
        {
            case "ahead":
                external = Checkpoint(4, entries[2].Hmac);
                break;
            case "wrong-head":
                external = Checkpoint(1, new byte[32]);
                break;
            case "missing-suffix":
                entries.RemoveAt(1);
                break;
            case "modified-suffix":
                entries[1] = entries[1] with { Event = entries[1].Event with { Result = "denial" } };
                break;
        }

        var result = AuditCheckpointReconciler.Reconcile(external, entries, database, ResolveKey);

        Assert.Equal(CheckpointReconciliationStatus.Invalid, result.Status);
    }

    private static AuditCheckpoint Checkpoint(long sequence, byte[] head) =>
        new(InstanceId, sequence, head, "key-1", AuditCanonicalEncoding.CurrentFormatVersion);

    private static IReadOnlyList<AuditChainEntry> CreateChain(int count)
    {
        var entries = new List<AuditChainEntry>();
        byte[] head = new byte[32];
        for (var sequence = 1; sequence <= count; sequence++)
        {
            var auditEvent = new AuditEventData(
                Guid.NewGuid(), DateTimeOffset.UnixEpoch.AddSeconds(sequence), "spike.event", "success",
                null, null, null, null, null, "spike", InstanceId, Guid.NewGuid(), null, null,
                new Dictionary<string, string>());
            var entry = AuditHmacChain.Append(auditEvent, sequence, head, "key-1", Key);
            entries.Add(entry);
            head = entry.Hmac;
        }

        return entries;
    }

    private static byte[]? ResolveKey(string keyId) => keyId == "key-1" ? Key : null;
}
