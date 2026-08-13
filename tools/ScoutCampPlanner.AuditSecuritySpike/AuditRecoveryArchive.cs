using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;

namespace ScoutCampPlanner.AuditSecuritySpike;

public sealed record AuditRecoverySet(byte[] Database, byte[] KeyBundle, byte[] Checkpoint);

public static class AuditRecoveryArchive
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int MemorySizeKib = 19_456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("ScoutCampPlanner.AuditRecoveryArchive.v1");

    public static async Task<byte[]> CreateAsync(
        AuditRecoverySet recoverySet,
        string password,
        CancellationToken cancellationToken = default)
    {
        ValidateRecoverySet(recoverySet);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        byte[] plaintext = CreateZip(recoverySet);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] key = await DeriveKeyAsync(password, salt, cancellationToken);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, Purpose);
            return JsonSerializer.SerializeToUtf8Bytes(new ArchiveEnvelope(
                1, "argon2id", MemorySizeKib, Iterations, Parallelism, "aes-256-gcm",
                Convert.ToBase64String(salt), Convert.ToBase64String(nonce),
                Convert.ToBase64String(ciphertext), Convert.ToBase64String(tag)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static async Task<AuditRecoverySet> RestoreAsync(
        ReadOnlyMemory<byte> archive,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArchiveEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ArchiveEnvelope>(archive.Span)
                ?? throw new InvalidDataException("Recovery archive is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Recovery archive encoding is invalid.", exception);
        }

        if (envelope.Version != 1 || envelope.Kdf != "argon2id" || envelope.Cipher != "aes-256-gcm" ||
            envelope.MemorySizeKib != MemorySizeKib || envelope.Iterations != Iterations ||
            envelope.Parallelism != Parallelism)
            throw new InvalidDataException("Recovery archive parameters are unsupported.");

        byte[] salt;
        byte[] nonce;
        byte[] ciphertext;
        byte[] tag;
        try
        {
            salt = Convert.FromBase64String(envelope.Salt);
            nonce = Convert.FromBase64String(envelope.Nonce);
            ciphertext = Convert.FromBase64String(envelope.Ciphertext);
            tag = Convert.FromBase64String(envelope.Tag);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Recovery archive encoding is invalid.", exception);
        }

        if (salt.Length != SaltSize || nonce.Length != NonceSize || tag.Length != TagSize)
            throw new InvalidDataException("Recovery archive parameters are invalid.");

        byte[] key = await DeriveKeyAsync(password, salt, cancellationToken);
        byte[] plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            try { aes.Decrypt(nonce, ciphertext, tag, plaintext, Purpose); }
            catch (AuthenticationTagMismatchException exception)
            {
                throw new InvalidDataException("Recovery archive password or authentication is invalid.", exception);
            }

            return ReadZip(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static async Task<byte[]> DeriveKeyAsync(string password, byte[] salt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = MemorySizeKib,
                Iterations = Iterations,
                DegreeOfParallelism = Parallelism
            };
            return await argon2.GetBytesAsync(KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static byte[] CreateZip(AuditRecoverySet recoverySet)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "database.bin", recoverySet.Database);
            WriteEntry(zip, "audit-keys.json", recoverySet.KeyBundle);
            WriteEntry(zip, "audit-checkpoint.json", recoverySet.Checkpoint);
        }
        return output.ToArray();
    }

    private static AuditRecoverySet ReadZip(byte[] plaintext)
    {
        try
        {
            using var input = new MemoryStream(plaintext, writable: false);
            using var zip = new ZipArchive(input, ZipArchiveMode.Read);
            if (zip.Entries.Count != 3 || zip.Entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() != 3)
                throw new InvalidDataException("Recovery archive must contain exactly three distinct entries.");
            return new AuditRecoverySet(
                ReadEntry(zip, "database.bin"),
                ReadEntry(zip, "audit-keys.json"),
                ReadEntry(zip, "audit-checkpoint.json"));
        }
        catch (InvalidDataException) { throw; }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            throw new InvalidDataException("Recovery archive contents are invalid.", exception);
        }
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        stream.Write(content);
    }

    private static byte[] ReadEntry(ZipArchive zip, string name)
    {
        ZipArchiveEntry entry = zip.GetEntry(name)
            ?? throw new InvalidDataException($"Recovery archive entry '{name}' is missing.");
        using Stream stream = entry.Open();
        using var output = new MemoryStream();
        stream.CopyTo(output);
        byte[] content = output.ToArray();
        if (content.Length == 0) throw new InvalidDataException($"Recovery archive entry '{name}' is empty.");
        return content;
    }

    private static void ValidateRecoverySet(AuditRecoverySet recoverySet)
    {
        ArgumentNullException.ThrowIfNull(recoverySet);
        if (recoverySet.Database.Length == 0 || recoverySet.KeyBundle.Length == 0 || recoverySet.Checkpoint.Length == 0)
            throw new ArgumentException("Recovery set components must not be empty.", nameof(recoverySet));
    }

    private sealed record ArchiveEnvelope(
        int Version,
        string Kdf,
        int MemorySizeKib,
        int Iterations,
        int Parallelism,
        string Cipher,
        string Salt,
        string Nonce,
        string Ciphertext,
        string Tag);
}
