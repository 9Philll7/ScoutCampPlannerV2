using System.Security.Cryptography;

namespace ScoutCampPlanner.AuditSecuritySpike;

public enum AuditKeyState
{
    Prepared,
    Active,
    Historical,
}

public sealed record AuditKey(string Id, byte[] Material, AuditKeyState State);

public sealed class AuditKeyBundle
{
    private readonly Dictionary<string, AuditKey> keys;

    public AuditKeyBundle(IEnumerable<AuditKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        this.keys = keys.ToDictionary(key => key.Id, StringComparer.Ordinal);
        Validate();
    }

    public IReadOnlyCollection<AuditKey> Keys => keys.Values;

    public AuditKey Active => keys.Values.Single(key => key.State == AuditKeyState.Active);

    public AuditKey? Prepared => keys.Values.SingleOrDefault(key => key.State == AuditKeyState.Prepared);

    public AuditKey Prepare(string keyId, byte[] material)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentNullException.ThrowIfNull(material);
        if (material.Length != 32) throw new ArgumentException("Audit keys must contain 32 bytes.", nameof(material));
        if (Prepared is not null) throw new InvalidOperationException("A prepared key already exists.");
        if (keys.ContainsKey(keyId)) throw new InvalidOperationException("Key ID already exists.");

        var prepared = new AuditKey(keyId, material.ToArray(), AuditKeyState.Prepared);
        keys.Add(keyId, prepared);
        return prepared;
    }

    public void ActivatePrepared()
    {
        AuditKey prepared = Prepared ?? throw new InvalidOperationException("No prepared key exists.");
        AuditKey active = Active;
        keys[active.Id] = active with { State = AuditKeyState.Historical };
        keys[prepared.Id] = prepared with { State = AuditKeyState.Active };
        Validate();
    }

    public void DiscardPrepared()
    {
        AuditKey prepared = Prepared ?? throw new InvalidOperationException("No prepared key exists.");
        keys.Remove(prepared.Id);
        CryptographicOperations.ZeroMemory(prepared.Material);
    }

    public byte[]? Resolve(string keyId) => keys.TryGetValue(keyId, out var key) ? key.Material : null;

    private void Validate()
    {
        if (keys.Values.Count(key => key.State == AuditKeyState.Active) != 1)
            throw new InvalidDataException("A key bundle must contain exactly one active key.");
        if (keys.Values.Count(key => key.State == AuditKeyState.Prepared) > 1)
            throw new InvalidDataException("A key bundle cannot contain more than one prepared key.");
        if (keys.Values.Any(key => string.IsNullOrWhiteSpace(key.Id) || key.Material.Length != 32))
            throw new InvalidDataException("A key bundle contains an invalid key.");
    }
}

public sealed record AuditRotationTransition(
    Guid OldSegmentId,
    Guid NewSegmentId,
    AuditChainEntry ClosingEntry,
    AuditChainEntry StartingEntry);

public static class AuditKeyRotation
{
    public static AuditRotationTransition CreateTransition(
        Guid oldSegmentId,
        Guid newSegmentId,
        long closingSequence,
        ReadOnlySpan<byte> oldHead,
        string oldKeyId,
        ReadOnlySpan<byte> oldKey,
        string newKeyId,
        ReadOnlySpan<byte> newKey,
        Guid instanceId,
        Guid correlationId,
        DateTimeOffset timestampUtc)
    {
        if (oldSegmentId == Guid.Empty || newSegmentId == Guid.Empty || oldSegmentId == newSegmentId)
            throw new ArgumentException("Rotation requires two distinct segment IDs.");

        var closingEvent = RotationEvent(
            "audit.key-rotation.closed", instanceId, correlationId, timestampUtc,
            new Dictionary<string, string>
            {
                ["oldSegmentId"] = oldSegmentId.ToString("D"),
                ["newSegmentId"] = newSegmentId.ToString("D"),
                ["newKeyId"] = newKeyId,
            });
        var closing = AuditHmacChain.Append(closingEvent, closingSequence, oldHead, oldKeyId, oldKey);

        var startingEvent = RotationEvent(
            "audit.key-rotation.started", instanceId, correlationId, timestampUtc,
            new Dictionary<string, string>
            {
                ["newSegmentId"] = newSegmentId.ToString("D"),
                ["oldSegmentId"] = oldSegmentId.ToString("D"),
                ["oldKeyId"] = oldKeyId,
                ["closingSequence"] = closing.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["closingHead"] = Convert.ToBase64String(closing.Hmac),
            });
        var starting = AuditHmacChain.Append(startingEvent, closingSequence + 1, closing.Hmac, newKeyId, newKey);
        return new AuditRotationTransition(oldSegmentId, newSegmentId, closing, starting);
    }

    private static AuditEventData RotationEvent(
        string action,
        Guid instanceId,
        Guid correlationId,
        DateTimeOffset timestampUtc,
        IReadOnlyDictionary<string, string> metadata) => new(
            Guid.NewGuid(), timestampUtc, action, "success", null, null, null,
            "audit-segment", null, "system", instanceId, correlationId, null, null, metadata);
}

public enum AuditRotationRecoveryAction
{
    None,
    DiscardPrepared,
    ActivatePrepared,
    Invalid,
}

public static class AuditRotationRecovery
{
    public static AuditRotationRecoveryAction Determine(
        AuditKeyBundle bundle,
        AuditCheckpoint databaseHead,
        IReadOnlyList<AuditChainEntry> transitionEntries)
    {
        AuditKey? prepared = bundle.Prepared;
        if (prepared is null) return AuditRotationRecoveryAction.None;

        if (databaseHead.KeyId == bundle.Active.Id)
        {
            return transitionEntries.Any(entry => entry.KeyId == prepared.Id)
                ? AuditRotationRecoveryAction.Invalid
                : AuditRotationRecoveryAction.DiscardPrepared;
        }

        if (databaseHead.KeyId != prepared.Id || transitionEntries.Count != 2)
            return AuditRotationRecoveryAction.Invalid;

        AuditChainEntry closing = transitionEntries[0];
        AuditChainEntry starting = transitionEntries[1];
        if (closing.KeyId != bundle.Active.Id || starting.KeyId != prepared.Id ||
            starting.Sequence != closing.Sequence + 1 ||
            databaseHead.Sequence != starting.Sequence ||
            !CryptographicOperations.FixedTimeEquals(databaseHead.Head, starting.Hmac))
            return AuditRotationRecoveryAction.Invalid;

        var verification = AuditHmacChain.Verify(
            transitionEntries,
            closing.PreviousHash,
            databaseHead.Head,
            bundle.Resolve);
        return verification.IsValid
            ? AuditRotationRecoveryAction.ActivatePrepared
            : AuditRotationRecoveryAction.Invalid;
    }
}
