using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace ScoutCampPlanner.AuditSecuritySpike;

public sealed record AuditEventData(
    Guid EventId,
    DateTimeOffset TimestampUtc,
    string Action,
    string Result,
    Guid? ActorUserId,
    Guid? TenantId,
    Guid? CampId,
    string? TargetType,
    Guid? TargetId,
    string Origin,
    Guid InstanceId,
    Guid CorrelationId,
    int? SecurityVersion,
    int? RoleDefinitionVersion,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record AuditChainEntry(
    long Sequence,
    byte[] PreviousHash,
    string KeyId,
    int FormatVersion,
    AuditEventData Event,
    byte[] Hmac);

public static class AuditCanonicalEncoding
{
    public const int CurrentFormatVersion = 1;

    public static byte[] Encode(
        long sequence,
        ReadOnlySpan<byte> previousHash,
        string keyId,
        AuditEventData auditEvent)
    {
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (previousHash.Length != SHA256.HashSizeInBytes)
            throw new ArgumentException("Previous hash must contain 32 bytes.", nameof(previousHash));
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentNullException.ThrowIfNull(auditEvent);
        if (auditEvent.TimestampUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Audit timestamp must use UTC.", nameof(auditEvent));

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", CurrentFormatVersion);
            writer.WriteNumber("sequence", sequence);
            writer.WriteBase64String("previousHash", previousHash);
            writer.WriteString("keyId", keyId);
            writer.WriteString("eventId", auditEvent.EventId.ToString("D"));
            writer.WriteString("timestampUtc", auditEvent.TimestampUtc.ToString("O"));
            writer.WriteString("action", auditEvent.Action);
            writer.WriteString("result", auditEvent.Result);
            WriteGuid(writer, "actorUserId", auditEvent.ActorUserId);
            WriteGuid(writer, "tenantId", auditEvent.TenantId);
            WriteGuid(writer, "campId", auditEvent.CampId);
            WriteString(writer, "targetType", auditEvent.TargetType);
            WriteGuid(writer, "targetId", auditEvent.TargetId);
            writer.WriteString("origin", auditEvent.Origin);
            writer.WriteString("instanceId", auditEvent.InstanceId.ToString("D"));
            writer.WriteString("correlationId", auditEvent.CorrelationId.ToString("D"));
            WriteInt32(writer, "securityVersion", auditEvent.SecurityVersion);
            WriteInt32(writer, "roleDefinitionVersion", auditEvent.RoleDefinitionVersion);
            writer.WriteStartObject("metadata");
            foreach (var item in auditEvent.Metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
                writer.WriteString(item.Key, item.Value);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteGuid(Utf8JsonWriter writer, string name, Guid? value)
    {
        if (value.HasValue) writer.WriteString(name, value.Value.ToString("D"));
        else writer.WriteNull(name);
    }

    private static void WriteString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null) writer.WriteString(name, value);
        else writer.WriteNull(name);
    }

    private static void WriteInt32(Utf8JsonWriter writer, string name, int? value)
    {
        if (value.HasValue) writer.WriteNumber(name, value.Value);
        else writer.WriteNull(name);
    }
}

public static class AuditHmacChain
{
    public static AuditChainEntry Append(
        AuditEventData auditEvent,
        long sequence,
        ReadOnlySpan<byte> previousHash,
        string keyId,
        ReadOnlySpan<byte> key)
    {
        byte[] canonical = AuditCanonicalEncoding.Encode(sequence, previousHash, keyId, auditEvent);
        byte[] hmac = HMACSHA256.HashData(key, canonical);
        return new AuditChainEntry(
            sequence,
            previousHash.ToArray(),
            keyId,
            AuditCanonicalEncoding.CurrentFormatVersion,
            auditEvent,
            hmac);
    }

    public static AuditChainVerification Verify(
        IReadOnlyList<AuditChainEntry> entries,
        ReadOnlySpan<byte> initialPreviousHash,
        ReadOnlySpan<byte> expectedHead,
        Func<string, byte[]?> resolveKey)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(resolveKey);
        byte[] previous = initialPreviousHash.ToArray();
        long expectedSequence = entries.Count == 0 ? 1 : entries[0].Sequence;

        foreach (var entry in entries)
        {
            if (entry.FormatVersion != AuditCanonicalEncoding.CurrentFormatVersion)
                return AuditChainVerification.Failed(entry.Sequence, "unsupported-format");
            if (entry.Sequence != expectedSequence)
                return AuditChainVerification.Failed(entry.Sequence, "sequence-discontinuity");
            if (!CryptographicOperations.FixedTimeEquals(entry.PreviousHash, previous))
                return AuditChainVerification.Failed(entry.Sequence, "previous-hash-mismatch");

            byte[]? key = resolveKey(entry.KeyId);
            if (key is null)
                return AuditChainVerification.Failed(entry.Sequence, "unknown-key");

            byte[] canonical = AuditCanonicalEncoding.Encode(entry.Sequence, entry.PreviousHash, entry.KeyId, entry.Event);
            byte[] actual = HMACSHA256.HashData(key, canonical);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(actual, entry.Hmac))
                    return AuditChainVerification.Failed(entry.Sequence, "hmac-mismatch");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
            }

            previous = entry.Hmac;
            expectedSequence++;
        }

        return CryptographicOperations.FixedTimeEquals(previous, expectedHead)
            ? AuditChainVerification.Valid
            : AuditChainVerification.Failed(entries.Count == 0 ? null : entries[^1].Sequence, "protected-head-mismatch");
    }
}

public sealed record AuditChainVerification(bool IsValid, long? Sequence, string? Failure)
{
    public static AuditChainVerification Valid { get; } = new(true, null, null);

    public static AuditChainVerification Failed(long? sequence, string failure) => new(false, sequence, failure);
}
