using System.Security.Cryptography;
using System.Text.Json;
using ScoutCampPlanner.Platform.Application.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class AuditSigningKey(string id, byte[] material) : IDisposable
{
    public string Id { get; } = id;
    public byte[] Material { get; } = material;

    public void Dispose() => CryptographicOperations.ZeroMemory(Material);
}

public interface IAuditSigningKeyProvider
{
    Task<AuditSigningKey> GetActiveAsync(CancellationToken cancellationToken = default);
}

public sealed class ProtectedMaterialAuditSigningKeyProvider(IAuditProtectedMaterialStore store)
    : IAuditSigningKeyProvider
{
    public async Task<AuditSigningKey> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        AuditProtectedMaterialLoadResult loaded = await store.LoadAsync(cancellationToken);
        if (loaded.Status != AuditProtectedMaterialStatus.Available || loaded.Material is null)
            throw new InvalidOperationException("Protected audit key material is unavailable.");

        try
        {
            KeyBundleFile bundle = JsonSerializer.Deserialize<KeyBundleFile>(loaded.Material.KeyBundle)
                ?? throw new InvalidDataException("Audit key bundle is empty.");
            if (bundle.Version != 1 || bundle.Keys is null)
                throw new InvalidDataException("Audit key bundle version is unsupported.");
            KeyFile active = bundle.Keys.Single(key => key.State == "Active");
            byte[] material = Convert.FromBase64String(active.Material);
            if (string.IsNullOrWhiteSpace(active.Id) || material.Length != 32)
                throw new InvalidDataException("Active audit key is invalid.");
            return new AuditSigningKey(active.Id, material);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            throw new InvalidDataException("Audit key bundle is invalid.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(loaded.Material.KeyBundle);
        }
    }

    private sealed record KeyBundleFile(int Version, KeyFile[] Keys);
    private sealed record KeyFile(string Id, string State, string Material);
}
