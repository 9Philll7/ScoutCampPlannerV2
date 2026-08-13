using System.Security.Cryptography;
using ScoutCampPlanner.Platform.Application.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class ConfiguredAuditProtectedMaterialStore(
    string? base64KeyBundle,
    string checkpointPath) : IAuditProtectedMaterialStore
{
    private readonly string _checkpointPath = Path.GetFullPath(checkpointPath);

    public async Task<AuditProtectedMaterialLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        byte[] bundle;
        try
        {
            if (string.IsNullOrWhiteSpace(base64KeyBundle))
                return new(AuditProtectedMaterialStatus.Missing);
            bundle = Convert.FromBase64String(base64KeyBundle);
        }
        catch (FormatException)
        {
            return new(AuditProtectedMaterialStatus.Invalid);
        }

        if (!File.Exists(_checkpointPath)) return new(AuditProtectedMaterialStatus.Missing);
        try
        {
            byte[] checkpoint = await File.ReadAllBytesAsync(_checkpointPath, cancellationToken);
            return new(AuditProtectedMaterialStatus.Available, new AuditProtectedMaterial(bundle, checkpoint));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(AuditProtectedMaterialStatus.Invalid);
        }
    }

    public async Task SaveAsync(AuditProtectedMaterial material, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (string.IsNullOrWhiteSpace(base64KeyBundle))
            throw new InvalidOperationException("The deployment audit key is unavailable.");

        byte[] configured = Convert.FromBase64String(base64KeyBundle);
        if (!CryptographicOperations.FixedTimeEquals(configured, material.KeyBundle))
            throw new InvalidOperationException("The application cannot replace the deployment-provided audit key.");

        string directory = Path.GetDirectoryName(_checkpointPath)
            ?? throw new InvalidOperationException("The checkpoint path has no directory.");
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_checkpointPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, material.Checkpoint, cancellationToken);
            File.Move(temporaryPath, _checkpointPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            CryptographicOperations.ZeroMemory(configured);
        }
    }
}
