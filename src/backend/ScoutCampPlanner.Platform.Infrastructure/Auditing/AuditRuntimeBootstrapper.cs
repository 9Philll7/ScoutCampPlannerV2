using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Application.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class AuditRuntimeState
{
    public AuditRuntimeState() { }

    public AuditRuntimeState(Guid instanceId)
    {
        if (instanceId == Guid.Empty) throw new ArgumentException("Audit instance ID is required.", nameof(instanceId));
        InstanceId = instanceId;
    }

    public Guid InstanceId { get; private set; }
    public bool IsReady => InstanceId != Guid.Empty;

    internal void MarkReady(Guid instanceId) => InstanceId = instanceId;
}

public sealed class AuditRuntimeBootstrapper(
    PlatformDbContext database,
    IAuditProtectedMaterialStore store,
    IAuditSigningKeyProvider keys,
    AuditRuntimeState state,
    TimeProvider timeProvider)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AuditProtectedMaterialLoadResult loaded = await store.LoadAsync(cancellationToken);
        Guid instanceId;
        if (loaded.Status == AuditProtectedMaterialStatus.Missing)
        {
            if (await database.AuditJournalHeads.AnyAsync(cancellationToken))
                throw new InvalidOperationException("Protected audit material is missing for an existing journal.");
            instanceId = Guid.NewGuid();
            byte[] key = RandomNumberGenerator.GetBytes(32);
            string keyId = $"key-{Guid.NewGuid():N}";
            try
            {
                byte[] keyBundle = JsonSerializer.SerializeToUtf8Bytes(new KeyBundleFile(
                    1, [new KeyFile(keyId, "Active", Convert.ToBase64String(key))]));
                byte[] checkpoint = SerializeGenesisCheckpoint(instanceId, keyId, key);
                await store.SaveAsync(new AuditProtectedMaterial(keyBundle, checkpoint), cancellationToken);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
        else if (loaded.Status == AuditProtectedMaterialStatus.Available && loaded.Material is not null)
        {
            try
            {
                instanceId = ReadCheckpointInstanceId(
                    loaded.Material.Checkpoint, loaded.Material.KeyBundle);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(loaded.Material.KeyBundle);
                CryptographicOperations.ZeroMemory(loaded.Material.Checkpoint);
            }
        }
        else
        {
            throw new InvalidOperationException("Protected audit material is invalid.");
        }

        await new AuditJournalInitializer(database, keys).InitializeAsync(
            instanceId, Guid.NewGuid(), timeProvider.GetUtcNow(), cancellationToken);
        state.MarkReady(instanceId);
    }

    private static byte[] SerializeGenesisCheckpoint(Guid instanceId, string keyId, ReadOnlySpan<byte> key)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new CheckpointPayload(
            1, instanceId, 0, Convert.ToBase64String(new byte[32]), keyId,
            AuditCanonicalEncoding.CurrentFormatVersion));
        byte[] purpose = Encoding.UTF8.GetBytes("ScoutCampPlanner.AuditCheckpoint.v1");
        byte[] authenticated = new byte[purpose.Length + 1 + payload.Length];
        purpose.CopyTo(authenticated, 0);
        payload.CopyTo(authenticated, purpose.Length + 1);
        byte[] hmac = HMACSHA256.HashData(key, authenticated);
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(new CheckpointFile(
                1, Convert.ToBase64String(payload), Convert.ToBase64String(hmac)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(authenticated);
            CryptographicOperations.ZeroMemory(hmac);
        }
    }

    private static Guid ReadCheckpointInstanceId(ReadOnlySpan<byte> checkpoint, ReadOnlySpan<byte> keyBundle)
    {
        CheckpointFile file = JsonSerializer.Deserialize<CheckpointFile>(checkpoint)
            ?? throw new InvalidDataException("Audit checkpoint is empty.");
        if (file.Version != 1) throw new InvalidDataException("Audit checkpoint version is unsupported.");
        byte[] payload = Convert.FromBase64String(file.Payload);
        byte[] storedHmac = Convert.FromBase64String(file.Hmac);
        try
        {
            CheckpointPayload value = JsonSerializer.Deserialize<CheckpointPayload>(payload)
                ?? throw new InvalidDataException("Audit checkpoint payload is empty.");
            if (value.Version != 1 || value.InstanceId == Guid.Empty)
                throw new InvalidDataException("Audit checkpoint identity is invalid.");
            KeyBundleFile bundle = JsonSerializer.Deserialize<KeyBundleFile>(keyBundle)
                ?? throw new InvalidDataException("Audit key bundle is empty.");
            KeyFile keyFile = bundle.Keys.Single(key => key.Id == value.KeyId);
            byte[] key = Convert.FromBase64String(keyFile.Material);
            byte[] purpose = Encoding.UTF8.GetBytes("ScoutCampPlanner.AuditCheckpoint.v1");
            byte[] authenticated = new byte[purpose.Length + 1 + payload.Length];
            purpose.CopyTo(authenticated, 0);
            payload.CopyTo(authenticated, purpose.Length + 1);
            byte[] actualHmac = HMACSHA256.HashData(key, authenticated);
            try
            {
                if (storedHmac.Length != 32 || !CryptographicOperations.FixedTimeEquals(storedHmac, actualHmac))
                    throw new InvalidDataException("Audit checkpoint authentication failed.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(authenticated);
                CryptographicOperations.ZeroMemory(actualHmac);
            }
            return value.InstanceId;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(storedHmac);
        }
    }

    private sealed record KeyBundleFile(int Version, KeyFile[] Keys);
    private sealed record KeyFile(string Id, string State, string Material);
    private sealed record CheckpointFile(int Version, string Payload, string Hmac);
    private sealed record CheckpointPayload(
        int Version, Guid InstanceId, long Sequence, string Head, string KeyId, int ChainFormatVersion);
}
