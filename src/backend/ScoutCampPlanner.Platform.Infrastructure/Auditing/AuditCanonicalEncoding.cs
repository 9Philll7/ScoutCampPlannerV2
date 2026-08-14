using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using ScoutCampPlanner.Platform.Application.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public static class AuditCanonicalEncoding
{
    public const int CurrentFormatVersion = 1;

    public static byte[] Encode(long sequence, ReadOnlySpan<byte> previousHash, string keyId, AuditEventDraft auditEvent)
    {
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (previousHash.Length != SHA256.HashSizeInBytes) throw new ArgumentException("Previous hash must contain 32 bytes.", nameof(previousHash));
        Validate(auditEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
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
        WriteInt(writer, "securityVersion", auditEvent.SecurityVersion);
        WriteInt(writer, "roleDefinitionVersion", auditEvent.RoleDefinitionVersion);
        writer.WriteStartObject("metadata");
        foreach ((string name, string value) in auditEvent.Metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
            writer.WriteString(name, value);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    internal static string EncodeMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach ((string name, string value) in metadata) sorted.Add(name, value);
        return JsonSerializer.Serialize(sorted);
    }

    private static void Validate(AuditEventDraft value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.EventId == Guid.Empty || value.InstanceId == Guid.Empty || value.CorrelationId == Guid.Empty)
            throw new ArgumentException("Audit identifiers must not be empty.", nameof(value));
        if (value.TimestampUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Audit timestamp must use UTC.", nameof(value));
        if (string.IsNullOrWhiteSpace(value.Action) || value.Action.Length > 128 ||
            string.IsNullOrWhiteSpace(value.Result) || value.Result.Length > 64 ||
            string.IsNullOrWhiteSpace(value.Origin) || value.Origin.Length > 64 ||
            value.TargetType?.Length > 128)
            throw new ArgumentException("Audit text fields are invalid.", nameof(value));
        if (value.Metadata.Any(item => string.IsNullOrWhiteSpace(item.Key) || item.Value is null))
            throw new ArgumentException("Audit metadata is invalid.", nameof(value));
    }

    private static void WriteGuid(Utf8JsonWriter writer, string name, Guid? value) { if (value.HasValue) writer.WriteString(name, value.Value.ToString("D")); else writer.WriteNull(name); }
    private static void WriteString(Utf8JsonWriter writer, string name, string? value) { if (value is null) writer.WriteNull(name); else writer.WriteString(name, value); }
    private static void WriteInt(Utf8JsonWriter writer, string name, int? value) { if (value.HasValue) writer.WriteNumber(name, value.Value); else writer.WriteNull(name); }
}
