using ScoutCampPlanner.AuditSecuritySpike;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class AuditSegmentRetentionTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MonthlyBoundaryKeepsSequenceChainAndKeyContinuous()
    {
        Guid instanceId = Guid.NewGuid();
        AuditSegmentTransition transition = AuditSegmentBoundary.CreateMonthlyTransition(
            Guid.NewGuid(), Guid.NewGuid(), 41, new byte[32], "key-1", Key,
            instanceId, Guid.NewGuid(), Now);

        AuditChainVerification verification = AuditHmacChain.Verify(
            [transition.ClosingEntry, transition.StartingEntry], new byte[32],
            transition.StartingEntry.Hmac, keyId => keyId == "key-1" ? Key : null);

        Assert.True(verification.IsValid);
        Assert.Equal(41, transition.ClosingEntry.Sequence);
        Assert.Equal(42, transition.StartingEntry.Sequence);
        Assert.Equal("key-1", transition.ClosingEntry.KeyId);
        Assert.Equal("key-1", transition.StartingEntry.KeyId);
        Assert.Equal(transition.ClosingEntry.Hmac, transition.StartingEntry.PreviousHash);
    }

    [Theory]
    [InlineData(false, true, false, false, "segment-not-closed")]
    [InlineData(true, false, false, false, "segment-not-verified")]
    [InlineData(true, true, true, false, "legal-hold")]
    [InlineData(true, true, false, true, "active-offline-transfer")]
    public void RetentionRejectsIncompleteOrProtectedSegment(
        bool closed, bool verified, bool legalHold, bool activeTransfer, string reason)
    {
        AuditSegmentDeletionDecision decision = Evaluate(Candidate(closed, verified, legalHold, activeTransfer, Now.AddDays(-1)));
        Assert.False(decision.MayDelete);
        Assert.Equal(reason, decision.Reason);
        Assert.Null(decision.Proof);
    }

    [Fact]
    public void LongestEventRetentionControlsWholeSegment()
    {
        AuditSegmentDeletionDecision decision = Evaluate(Candidate(true, true, false, false, Now.AddSeconds(1)));
        Assert.False(decision.MayDelete);
        Assert.Equal("retention-not-expired", decision.Reason);
    }

    [Fact]
    public void ExpiredVerifiedCompleteSegmentProducesNonPersonalProof()
    {
        AuditSegmentRetentionCandidate candidate = Candidate(true, true, false, false, Now);
        AuditSegmentDeletionDecision decision = Evaluate(candidate);

        Assert.True(decision.MayDelete);
        Assert.Equal("complete-segment-expired", decision.Reason);
        Assert.Equal(candidate.SegmentId, decision.Proof!.SegmentId);
        Assert.Equal(candidate.FirstSequence, decision.Proof.FirstSequence);
        Assert.Equal(candidate.LastSequence, decision.Proof.LastSequence);
        Assert.Equal("key-1", decision.Proof.KeyId);
    }

    private static AuditSegmentDeletionDecision Evaluate(AuditSegmentRetentionCandidate candidate) =>
        AuditSegmentRetention.Evaluate(candidate, Now, new byte[32], Enumerable.Repeat((byte)7, 32).ToArray(), "key-1", 1);

    private static AuditSegmentRetentionCandidate Candidate(
        bool closed, bool verified, bool legalHold, bool activeTransfer, DateTimeOffset expiry) =>
        new(Guid.NewGuid(), 1, 100, expiry, closed, verified, legalHold, activeTransfer);
}
