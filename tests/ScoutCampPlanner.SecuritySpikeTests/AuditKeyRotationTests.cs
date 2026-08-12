using ScoutCampPlanner.AuditSecuritySpike;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class AuditKeyRotationTests
{
    private static readonly byte[] OldKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    private static readonly byte[] NewKey = Enumerable.Range(33, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void RotationCreatesContinuousEntriesSignedByOldAndNewKeys()
    {
        var transition = CreateTransition();
        var entries = new[] { transition.ClosingEntry, transition.StartingEntry };

        var result = AuditHmacChain.Verify(entries, new byte[32], transition.StartingEntry.Hmac, ResolveBoth);

        Assert.True(result.IsValid);
        Assert.Equal("old-key", transition.ClosingEntry.KeyId);
        Assert.Equal("new-key", transition.StartingEntry.KeyId);
        Assert.Equal(transition.ClosingEntry.Hmac, transition.StartingEntry.PreviousHash);
    }

    [Fact]
    public void RotationRequiresBothKeysAndRejectsModifiedBoundary()
    {
        var transition = CreateTransition();
        var entries = new[] { transition.ClosingEntry, transition.StartingEntry };

        Assert.False(AuditHmacChain.Verify(entries, new byte[32], transition.StartingEntry.Hmac,
            keyId => keyId == "new-key" ? NewKey : null).IsValid);

        entries[1] = entries[1] with
        {
            Event = entries[1].Event with
            {
                Metadata = new Dictionary<string, string>(entries[1].Event.Metadata)
                {
                    ["oldSegmentId"] = Guid.NewGuid().ToString("D"),
                },
            },
        };
        Assert.False(AuditHmacChain.Verify(entries, new byte[32], transition.StartingEntry.Hmac, ResolveBoth).IsValid);
    }

    [Fact]
    public void KeyBundleStagesThenActivatesRotationAndKeepsOldKeyForVerification()
    {
        var bundle = Bundle();

        bundle.Prepare("new-key", NewKey);
        Assert.Equal("old-key", bundle.Active.Id);
        Assert.Equal("new-key", bundle.Prepared?.Id);

        bundle.ActivatePrepared();

        Assert.Equal("new-key", bundle.Active.Id);
        Assert.Null(bundle.Prepared);
        Assert.Equal(AuditKeyState.Historical, bundle.Keys.Single(key => key.Id == "old-key").State);
        Assert.NotNull(bundle.Resolve("old-key"));
    }

    [Fact]
    public void UncommittedPreparedKeyCanBeDiscardedButOnlyOneCanExist()
    {
        var bundle = Bundle();
        bundle.Prepare("new-key", NewKey);

        Assert.Throws<InvalidOperationException>(() => bundle.Prepare("another-key", NewKey));

        bundle.DiscardPrepared();
        Assert.Null(bundle.Prepared);
        Assert.Equal("old-key", bundle.Active.Id);
    }

    [Fact]
    public void RecoveryDiscardsPreparationBeforeDatabaseCommit()
    {
        var bundle = Bundle();
        bundle.Prepare("new-key", NewKey);
        var databaseHead = new AuditCheckpoint(Guid.NewGuid(), 0, new byte[32], "old-key", 1);

        var action = AuditRotationRecovery.Determine(bundle, databaseHead, []);

        Assert.Equal(AuditRotationRecoveryAction.DiscardPrepared, action);
    }

    [Fact]
    public void RecoveryActivatesPreparedKeyAfterVerifiedDatabaseCommit()
    {
        var bundle = Bundle();
        bundle.Prepare("new-key", NewKey);
        var transition = CreateTransition();
        var databaseHead = new AuditCheckpoint(
            transition.StartingEntry.Event.InstanceId,
            transition.StartingEntry.Sequence,
            transition.StartingEntry.Hmac,
            "new-key",
            1);

        var action = AuditRotationRecovery.Determine(
            bundle,
            databaseHead,
            [transition.ClosingEntry, transition.StartingEntry]);

        Assert.Equal(AuditRotationRecoveryAction.ActivatePrepared, action);
    }

    [Fact]
    public void RecoveryRejectsIncompleteOrMismatchingCommittedTransition()
    {
        var bundle = Bundle();
        bundle.Prepare("new-key", NewKey);
        var transition = CreateTransition();
        var databaseHead = new AuditCheckpoint(Guid.NewGuid(), 2, transition.StartingEntry.Hmac, "new-key", 1);

        Assert.Equal(
            AuditRotationRecoveryAction.Invalid,
            AuditRotationRecovery.Determine(bundle, databaseHead, [transition.StartingEntry]));
    }

    private static AuditKeyBundle Bundle() => new(
        [new AuditKey("old-key", OldKey.ToArray(), AuditKeyState.Active)]);

    private static AuditRotationTransition CreateTransition() => AuditKeyRotation.CreateTransition(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        1,
        new byte[32],
        "old-key",
        OldKey,
        "new-key",
        NewKey,
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        new DateTimeOffset(2026, 8, 12, 20, 0, 0, TimeSpan.Zero));

    private static byte[]? ResolveBoth(string keyId) => keyId switch
    {
        "old-key" => OldKey,
        "new-key" => NewKey,
        _ => null,
    };
}
