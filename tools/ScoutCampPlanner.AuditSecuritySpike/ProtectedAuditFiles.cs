using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScoutCampPlanner.AuditSecuritySpike;

public static class ProtectedAuditFiles
{
    private const string CheckpointPurpose = "ScoutCampPlanner.AuditCheckpoint.v1";

    public static byte[] SerializeKeyBundle(AuditKeyBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return JsonSerializer.SerializeToUtf8Bytes(new KeyBundleFile(
            1,
            bundle.Keys
                .OrderBy(key => key.Id, StringComparer.Ordinal)
                .Select(key => new KeyFile(key.Id, key.State.ToString(), Convert.ToBase64String(key.Material)))
                .ToArray()));
    }

    public static AuditKeyBundle DeserializeKeyBundle(ReadOnlySpan<byte> file)
    {
        KeyBundleFile value = JsonSerializer.Deserialize<KeyBundleFile>(file)
            ?? throw new InvalidDataException("Key bundle is empty.");
        if (value.Version != 1 || value.Keys is null || value.Keys.Length == 0)
            throw new InvalidDataException("Key bundle version or contents are invalid.");

        var keys = new List<AuditKey>(value.Keys.Length);
        try
        {
            foreach (var item in value.Keys)
            {
                if (!Enum.TryParse(item.State, ignoreCase: false, out AuditKeyState state) ||
                    !string.Equals(state.ToString(), item.State, StringComparison.Ordinal))
                    throw new InvalidDataException("Key bundle state is invalid.");
                byte[] material = Convert.FromBase64String(item.Material);
                keys.Add(new AuditKey(item.Id, material, state));
            }

            return new AuditKeyBundle(keys);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            foreach (var key in keys) CryptographicOperations.ZeroMemory(key.Material);
            throw new InvalidDataException("Key bundle encoding is invalid.", exception);
        }
    }

    public static byte[] SerializeCheckpoint(AuditCheckpoint checkpoint, ReadOnlySpan<byte> key)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new CheckpointPayload(
            1,
            checkpoint.InstanceId,
            checkpoint.Sequence,
            Convert.ToBase64String(checkpoint.Head),
            checkpoint.KeyId,
            checkpoint.FormatVersion));
        byte[] purpose = Encoding.UTF8.GetBytes(CheckpointPurpose);
        byte[] authenticated = new byte[purpose.Length + 1 + payload.Length];
        purpose.CopyTo(authenticated, 0);
        payload.CopyTo(authenticated, purpose.Length + 1);
        byte[] hmac = HMACSHA256.HashData(key, authenticated);
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(new CheckpointFile(
                1,
                Convert.ToBase64String(payload),
                Convert.ToBase64String(hmac)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(authenticated);
            CryptographicOperations.ZeroMemory(hmac);
        }
    }

    public static AuditCheckpoint DeserializeCheckpoint(ReadOnlySpan<byte> file, Func<string, byte[]?> resolveKey)
    {
        ArgumentNullException.ThrowIfNull(resolveKey);
        CheckpointFile envelope = JsonSerializer.Deserialize<CheckpointFile>(file)
            ?? throw new InvalidDataException("Checkpoint is empty.");
        if (envelope.Version != 1) throw new InvalidDataException("Checkpoint version is unsupported.");

        byte[] payload;
        byte[] storedHmac;
        try
        {
            payload = Convert.FromBase64String(envelope.Payload);
            storedHmac = Convert.FromBase64String(envelope.Hmac);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Checkpoint encoding is invalid.", exception);
        }

        CheckpointPayload value = JsonSerializer.Deserialize<CheckpointPayload>(payload)
            ?? throw new InvalidDataException("Checkpoint payload is empty.");
        byte[] key = resolveKey(value.KeyId) ?? throw new InvalidDataException("Checkpoint key is unavailable.");
        byte[] purpose = Encoding.UTF8.GetBytes(CheckpointPurpose);
        byte[] authenticated = new byte[purpose.Length + 1 + payload.Length];
        purpose.CopyTo(authenticated, 0);
        payload.CopyTo(authenticated, purpose.Length + 1);
        byte[] actualHmac = HMACSHA256.HashData(key, authenticated);
        try
        {
            if (storedHmac.Length != SHA256.HashSizeInBytes ||
                !CryptographicOperations.FixedTimeEquals(storedHmac, actualHmac))
                throw new InvalidDataException("Checkpoint authentication failed.");

            byte[] head = Convert.FromBase64String(value.Head);
            if (value.Version != 1 || value.Sequence < 0 || head.Length != SHA256.HashSizeInBytes)
                throw new InvalidDataException("Checkpoint payload is invalid.");
            return new AuditCheckpoint(value.InstanceId, value.Sequence, head, value.KeyId, value.ChainFormatVersion);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(storedHmac);
            CryptographicOperations.ZeroMemory(authenticated);
            CryptographicOperations.ZeroMemory(actualHmac);
        }
    }

    public static async Task WriteAtomicallyAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("Path has no directory.", nameof(path));
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private sealed record CheckpointFile(int Version, string Payload, string Hmac);
    private sealed record KeyBundleFile(int Version, KeyFile[] Keys);
    private sealed record KeyFile(string Id, string State, string Material);
    private sealed record CheckpointPayload(
        int Version,
        Guid InstanceId,
        long Sequence,
        string Head,
        string KeyId,
        int ChainFormatVersion);
}
