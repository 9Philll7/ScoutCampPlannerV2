using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;

namespace ScoutCampPlanner.AuditSecuritySpike;

public sealed record AuditTransferContext(
    Guid TransferId,
    Guid TenantId,
    Guid CampId,
    long BaselineVersion);

public sealed record AuditTransferProvisioning(
    AuditTransferContext Context,
    byte[] PublicKey,
    byte[] EncryptedPrivateKey);

public sealed record AuditPackageV2Proof(
    int FormatVersion,
    AuditTransferContext Context,
    Guid SourceInstanceId,
    long FirstSequence,
    long LastSequence,
    byte[] FirstPredecessorHash,
    byte[] FinalHead,
    byte[] DomainPayloadHash,
    byte[] AuditSectionHash,
    byte[] Signature);

public static class AuditPackageV2Binding
{
    private static readonly byte[] SignaturePurpose = Encoding.UTF8.GetBytes("ScoutCampPlanner.CampPackage.v2.ReturnSignature");
    private static readonly byte[] EncryptionPurpose = Encoding.UTF8.GetBytes("ScoutCampPlanner.CampPackage.v2.TransferPrivateKey");
    private static readonly byte[] OutboundPackagePurpose = Encoding.UTF8.GetBytes("ScoutCampPlanner.CampPackage.v2.CloudToLocal");
    private static readonly byte[] ReturnPackagePurpose = Encoding.UTF8.GetBytes("ScoutCampPlanner.CampPackage.v2.LocalToCloud");

    public static async Task<AuditTransferProvisioning> ProvisionAsync(
        AuditTransferContext context,
        string transferPassword,
        CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] privateKey = signingKey.ExportPkcs8PrivateKey();
        try
        {
            byte[] encrypted = await EncryptPrivateKeyAsync(privateKey, transferPassword, cancellationToken);
            return new(context, signingKey.ExportSubjectPublicKeyInfo(), encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    public static async Task<byte[]> OpenPrivateKeyAsync(
        AuditTransferProvisioning provisioning,
        string transferPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provisioning);
        return await DecryptPrivateKeyAsync(provisioning.EncryptedPrivateKey, transferPassword, cancellationToken);
    }

    public static Task<byte[]> ProtectPackageAsync(
        ReadOnlyMemory<byte> plaintext,
        string transferPassword,
        bool isReturnPackage,
        CancellationToken cancellationToken = default)
    {
        if (plaintext.IsEmpty) throw new ArgumentException("Package payload is required.", nameof(plaintext));
        return EncryptBytesAsync(plaintext.ToArray(), transferPassword,
            isReturnPackage ? ReturnPackagePurpose : OutboundPackagePurpose, cancellationToken);
    }

    public static Task<byte[]> OpenPackageAsync(
        ReadOnlyMemory<byte> envelope,
        string transferPassword,
        bool isReturnPackage,
        CancellationToken cancellationToken = default) =>
        DecryptBytesAsync(envelope.ToArray(), transferPassword,
            isReturnPackage ? ReturnPackagePurpose : OutboundPackagePurpose, cancellationToken);

    public static AuditPackageV2Proof CreateReturnProof(
        AuditTransferContext context,
        Guid sourceInstanceId,
        ReadOnlySpan<byte> domainPayload,
        IReadOnlyList<AuditChainEntry> auditEntries,
        ReadOnlySpan<byte> privateKey)
    {
        ValidateContext(context);
        if (sourceInstanceId == Guid.Empty) throw new ArgumentException("Source instance is required.", nameof(sourceInstanceId));
        ValidateAuditSection(auditEntries, sourceInstanceId);

        byte[] auditHash = HashAuditSection(auditEntries);
        var unsigned = new AuditPackageV2Proof(
            2, context, sourceInstanceId, auditEntries[0].Sequence, auditEntries[^1].Sequence,
            auditEntries[0].PreviousHash.ToArray(), auditEntries[^1].Hmac.ToArray(),
            SHA256.HashData(domainPayload), auditHash, []);
        byte[] signedBytes = EncodeProof(unsigned);
        using ECDsa signer = ECDsa.Create();
        signer.ImportPkcs8PrivateKey(privateKey, out _);
        return unsigned with { Signature = signer.SignData(signedBytes, HashAlgorithmName.SHA256) };
    }

    public static bool VerifyReturnProof(
        AuditPackageV2Proof proof,
        AuditTransferContext expectedContext,
        ReadOnlySpan<byte> domainPayload,
        IReadOnlyList<AuditChainEntry> auditEntries,
        ReadOnlySpan<byte> publicKey)
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (proof.FormatVersion != 2 || proof.Context != expectedContext ||
            auditEntries.Count == 0 || proof.SourceInstanceId == Guid.Empty)
            return false;
        try { ValidateAuditSection(auditEntries, proof.SourceInstanceId); }
        catch (ArgumentException) { return false; }

        if (proof.FirstSequence != auditEntries[0].Sequence || proof.LastSequence != auditEntries[^1].Sequence ||
            !FixedEquals(proof.FirstPredecessorHash, auditEntries[0].PreviousHash) ||
            !FixedEquals(proof.FinalHead, auditEntries[^1].Hmac) ||
            !FixedEquals(proof.DomainPayloadHash, SHA256.HashData(domainPayload)) ||
            !FixedEquals(proof.AuditSectionHash, HashAuditSection(auditEntries)))
            return false;

        using ECDsa verifier = ECDsa.Create();
        try { verifier.ImportSubjectPublicKeyInfo(publicKey, out _); }
        catch (CryptographicException) { return false; }
        return verifier.VerifyData(EncodeProof(proof with { Signature = [] }), proof.Signature, HashAlgorithmName.SHA256);
    }

    private static void ValidateAuditSection(IReadOnlyList<AuditChainEntry> entries, Guid sourceInstanceId)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0) throw new ArgumentException("Audit section is mandatory.", nameof(entries));
        for (var index = 0; index < entries.Count; index++)
        {
            AuditChainEntry entry = entries[index];
            if (entry.Event.InstanceId != sourceInstanceId)
                throw new ArgumentException("Audit section contains another source instance.", nameof(entries));
            if (index > 0 && (entry.Sequence != entries[index - 1].Sequence + 1 ||
                !FixedEquals(entry.PreviousHash, entries[index - 1].Hmac)))
                throw new ArgumentException("Audit section is incomplete or discontinuous.", nameof(entries));
        }
    }

    private static byte[] HashAuditSection(IReadOnlyList<AuditChainEntry> entries)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (AuditChainEntry entry in entries)
        {
            byte[] canonical = AuditCanonicalEncoding.Encode(entry.Sequence, entry.PreviousHash, entry.KeyId, entry.Event);
            BinaryPrimitives.WriteInt32BigEndian(length, canonical.Length);
            hash.AppendData(length);
            hash.AppendData(canonical);
            hash.AppendData(entry.Hmac);
        }
        return hash.GetHashAndReset();
    }

    private static byte[] EncodeProof(AuditPackageV2Proof proof)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(SignaturePurpose.Length);
        writer.Write(SignaturePurpose);
        writer.Write(proof.FormatVersion);
        writer.Write(proof.Context.TransferId.ToByteArray());
        writer.Write(proof.Context.TenantId.ToByteArray());
        writer.Write(proof.Context.CampId.ToByteArray());
        writer.Write(proof.Context.BaselineVersion);
        writer.Write(proof.SourceInstanceId.ToByteArray());
        writer.Write(proof.FirstSequence);
        writer.Write(proof.LastSequence);
        WriteBytes(writer, proof.FirstPredecessorHash);
        WriteBytes(writer, proof.FinalHead);
        WriteBytes(writer, proof.DomainPayloadHash);
        WriteBytes(writer, proof.AuditSectionHash);
        writer.Flush();
        return output.ToArray();
    }

    private static void WriteBytes(BinaryWriter writer, byte[] value)
    {
        writer.Write(value.Length);
        writer.Write(value);
    }

    private static async Task<byte[]> EncryptPrivateKeyAsync(byte[] privateKey, string password, CancellationToken cancellationToken)
        => await EncryptBytesAsync(privateKey, password, EncryptionPurpose, cancellationToken);

    private static async Task<byte[]> EncryptBytesAsync(
        byte[] plaintext,
        string password,
        byte[] purpose,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] key = await DeriveKeyAsync(password, salt, cancellationToken);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, purpose);
            return JsonSerializer.SerializeToUtf8Bytes(new PrivateKeyEnvelope(
                1, Convert.ToBase64String(salt), Convert.ToBase64String(nonce),
                Convert.ToBase64String(ciphertext), Convert.ToBase64String(tag)));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static async Task<byte[]> DecryptPrivateKeyAsync(byte[] envelopeBytes, string password, CancellationToken cancellationToken)
        => await DecryptBytesAsync(envelopeBytes, password, EncryptionPurpose, cancellationToken);

    private static async Task<byte[]> DecryptBytesAsync(
        byte[] envelopeBytes,
        string password,
        byte[] purpose,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        PrivateKeyEnvelope envelope;
        try { envelope = JsonSerializer.Deserialize<PrivateKeyEnvelope>(envelopeBytes)!; }
        catch (JsonException exception) { throw new InvalidDataException("Transfer key encoding is invalid.", exception); }
        if (envelope is null || envelope.Version != 1) throw new InvalidDataException("Transfer key version is unsupported.");
        try
        {
            byte[] salt = Convert.FromBase64String(envelope.Salt);
            byte[] nonce = Convert.FromBase64String(envelope.Nonce);
            byte[] ciphertext = Convert.FromBase64String(envelope.Ciphertext);
            byte[] tag = Convert.FromBase64String(envelope.Tag);
            if (salt.Length != 16 || nonce.Length != 12 || tag.Length != 16)
                throw new InvalidDataException("Transfer key parameters are invalid.");
            byte[] key = await DeriveKeyAsync(password, salt, cancellationToken);
            byte[] plaintext = new byte[ciphertext.Length];
            try
            {
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, purpose);
                return plaintext;
            }
            catch (AuthenticationTagMismatchException exception)
            {
                throw new InvalidDataException("Transfer password or key authentication is invalid.", exception);
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch (FormatException exception) { throw new InvalidDataException("Transfer key encoding is invalid.", exception); }
    }

    private static async Task<byte[]> DeriveKeyAsync(string password, byte[] salt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes) { Salt = salt, MemorySize = 19_456, Iterations = 2, DegreeOfParallelism = 1 };
            return await argon2.GetBytesAsync(32);
        }
        finally { CryptographicOperations.ZeroMemory(passwordBytes); }
    }

    private static bool FixedEquals(byte[] left, byte[] right) =>
        left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);

    private static void ValidateContext(AuditTransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TransferId == Guid.Empty || context.TenantId == Guid.Empty || context.CampId == Guid.Empty || context.BaselineVersion < 0)
            throw new ArgumentException("Transfer context is invalid.", nameof(context));
    }

    private sealed record PrivateKeyEnvelope(int Version, string Salt, string Nonce, string Ciphertext, string Tag);
}

public enum AuditTransferDuplicateStatus { New, Identical, Conflict }

public static class AuditTransferDeduplication
{
    public static AuditTransferDuplicateStatus Classify(
        AuditChainEntry incoming,
        AuditChainEntry? existing,
        Guid sourceInstanceId)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (existing is null) return AuditTransferDuplicateStatus.New;
        if (incoming.Event.InstanceId != sourceInstanceId || existing.Event.InstanceId != sourceInstanceId ||
            incoming.Event.EventId != existing.Event.EventId || incoming.Sequence != existing.Sequence)
            return AuditTransferDuplicateStatus.Conflict;
        byte[] incomingBytes = AuditCanonicalEncoding.Encode(incoming.Sequence, incoming.PreviousHash, incoming.KeyId, incoming.Event);
        byte[] existingBytes = AuditCanonicalEncoding.Encode(existing.Sequence, existing.PreviousHash, existing.KeyId, existing.Event);
        return CryptographicOperations.FixedTimeEquals(incomingBytes, existingBytes) &&
               CryptographicOperations.FixedTimeEquals(incoming.Hmac, existing.Hmac)
            ? AuditTransferDuplicateStatus.Identical
            : AuditTransferDuplicateStatus.Conflict;
    }
}
