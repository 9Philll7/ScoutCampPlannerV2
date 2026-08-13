using System.Security.Cryptography;
using ScoutCampPlanner.Platform.Application.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public sealed class FileAuditProtectedMaterialStore(
    string directory,
    IAuditKeyBundleProtection keyBundleProtection) : IAuditProtectedMaterialStore
{
    private readonly string _directory = Path.GetFullPath(directory);
    private readonly IAuditKeyBundleProtection _keyBundleProtection = keyBundleProtection;

    public async Task<AuditProtectedMaterialLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        string keyPath = Path.Combine(_directory, "audit-keys.bin");
        string checkpointPath = Path.Combine(_directory, "audit-checkpoint.json");
        bool hasKey = File.Exists(keyPath);
        bool hasCheckpoint = File.Exists(checkpointPath);
        if (!hasKey && !hasCheckpoint) return new(AuditProtectedMaterialStatus.Missing);
        if (!hasKey || !hasCheckpoint) return new(AuditProtectedMaterialStatus.Invalid);

        try
        {
            byte[] protectedBundle = await File.ReadAllBytesAsync(keyPath, cancellationToken);
            byte[] bundle = _keyBundleProtection.Unprotect(protectedBundle);
            byte[] checkpoint = await File.ReadAllBytesAsync(checkpointPath, cancellationToken);
            return new(AuditProtectedMaterialStatus.Available, new AuditProtectedMaterial(bundle, checkpoint));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return new(AuditProtectedMaterialStatus.Invalid);
        }
    }

    public async Task SaveAsync(AuditProtectedMaterial material, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(material);
        Directory.CreateDirectory(_directory);
        byte[] protectedBundle = _keyBundleProtection.Protect(material.KeyBundle);
        string keyPath = Path.Combine(_directory, "audit-keys.bin");
        string checkpointPath = Path.Combine(_directory, "audit-checkpoint.json");
        await WriteAtomicallyAsync(keyPath, protectedBundle, cancellationToken);
        await WriteAtomicallyAsync(checkpointPath, material.Checkpoint, cancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(checkpointPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static async Task WriteAtomicallyAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        string temporaryPath = Path.Combine(
            Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Close();
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
