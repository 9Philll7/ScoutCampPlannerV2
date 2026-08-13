using System.Globalization;
using System.Security.Cryptography;

namespace ScoutCampPlanner.AuditSecuritySpike;

public sealed record AuditSegmentTransition(
    Guid ClosedSegmentId,
    Guid OpenedSegmentId,
    AuditChainEntry ClosingEntry,
    AuditChainEntry StartingEntry);

public static class AuditSegmentBoundary
{
    public static AuditSegmentTransition CreateMonthlyTransition(
        Guid closedSegmentId,
        Guid openedSegmentId,
        long closingSequence,
        ReadOnlySpan<byte> previousHead,
        string keyId,
        ReadOnlySpan<byte> key,
        Guid instanceId,
        Guid correlationId,
        DateTimeOffset timestampUtc)
    {
        if (closedSegmentId == Guid.Empty || openedSegmentId == Guid.Empty || closedSegmentId == openedSegmentId)
            throw new ArgumentException("A monthly boundary requires two distinct segment IDs.");
        if (timestampUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Segment boundary timestamp must use UTC.", nameof(timestampUtc));

        AuditChainEntry closing = AuditHmacChain.Append(
            BoundaryEvent("audit.segment.closed", instanceId, correlationId, timestampUtc,
                new Dictionary<string, string>
                {
                    ["closedSegmentId"] = closedSegmentId.ToString("D"),
                    ["openedSegmentId"] = openedSegmentId.ToString("D"),
                    ["reason"] = "monthly"
                }),
            closingSequence, previousHead, keyId, key);

        AuditChainEntry starting = AuditHmacChain.Append(
            BoundaryEvent("audit.segment.started", instanceId, correlationId, timestampUtc,
                new Dictionary<string, string>
                {
                    ["openedSegmentId"] = openedSegmentId.ToString("D"),
                    ["closedSegmentId"] = closedSegmentId.ToString("D"),
                    ["closingSequence"] = closing.Sequence.ToString(CultureInfo.InvariantCulture),
                    ["closingHead"] = Convert.ToBase64String(closing.Hmac),
                    ["reason"] = "monthly"
                }),
            closingSequence + 1, closing.Hmac, keyId, key);

        return new(closedSegmentId, openedSegmentId, closing, starting);
    }

    private static AuditEventData BoundaryEvent(
        string action,
        Guid instanceId,
        Guid correlationId,
        DateTimeOffset timestampUtc,
        IReadOnlyDictionary<string, string> metadata) => new(
            Guid.NewGuid(), timestampUtc, action, "success", null, null, null,
            "audit-segment", null, "system", instanceId, correlationId, null, null, metadata);
}

public sealed record AuditSegmentRetentionCandidate(
    Guid SegmentId,
    long FirstSequence,
    long LastSequence,
    DateTimeOffset LatestRetentionExpiryUtc,
    bool IsClosed,
    bool WasFullyVerified,
    bool HasLegalHold,
    bool HasActiveOfflineTransfer);

public sealed record AuditSegmentClosingProof(
    Guid SegmentId,
    long FirstSequence,
    long LastSequence,
    byte[] PredecessorHash,
    byte[] ClosingHash,
    string KeyId,
    int ChainFormatVersion);

public sealed record AuditSegmentDeletionDecision(
    bool MayDelete,
    string Reason,
    AuditSegmentClosingProof? Proof);

public static class AuditSegmentRetention
{
    public static AuditSegmentDeletionDecision Evaluate(
        AuditSegmentRetentionCandidate candidate,
        DateTimeOffset nowUtc,
        ReadOnlySpan<byte> predecessorHash,
        ReadOnlySpan<byte> closingHash,
        string keyId,
        int chainFormatVersion)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (nowUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Retention time must use UTC.", nameof(nowUtc));
        if (candidate.FirstSequence < 1 || candidate.LastSequence < candidate.FirstSequence)
            return Deny("invalid-sequence-range");
        if (!candidate.IsClosed) return Deny("segment-not-closed");
        if (!candidate.WasFullyVerified) return Deny("segment-not-verified");
        if (candidate.HasLegalHold) return Deny("legal-hold");
        if (candidate.HasActiveOfflineTransfer) return Deny("active-offline-transfer");
        if (candidate.LatestRetentionExpiryUtc > nowUtc) return Deny("retention-not-expired");
        if (predecessorHash.Length != SHA256.HashSizeInBytes || closingHash.Length != SHA256.HashSizeInBytes ||
            string.IsNullOrWhiteSpace(keyId) || chainFormatVersion < 1)
            return Deny("invalid-closing-proof");

        return new(true, "complete-segment-expired", new AuditSegmentClosingProof(
            candidate.SegmentId, candidate.FirstSequence, candidate.LastSequence,
            predecessorHash.ToArray(), closingHash.ToArray(), keyId, chainFormatVersion));
    }

    private static AuditSegmentDeletionDecision Deny(string reason) => new(false, reason, null);
}
