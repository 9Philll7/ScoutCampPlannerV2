using ScoutCampPlanner.AuditSecuritySpike;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class AuditPackageV2Tests
{
    private const string Password = "correct horse battery staple";
    private static readonly byte[] AuditKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public async Task ReturnSignatureBindsContextDomainPayloadAndAuditSection()
    {
        AuditTransferContext context = Context();
        AuditTransferProvisioning provisioning = await AuditPackageV2Binding.ProvisionAsync(
            context, Password, TestContext.Current.CancellationToken);
        byte[] privateKey = await AuditPackageV2Binding.OpenPrivateKeyAsync(
            provisioning, Password, TestContext.Current.CancellationToken);
        IReadOnlyList<AuditChainEntry> entries = Chain(3);
        byte[] domain = "domain payload"u8.ToArray();
        AuditPackageV2Proof proof = AuditPackageV2Binding.CreateReturnProof(
            context, entries[0].Event.InstanceId, domain, entries, privateKey);

        Assert.True(AuditPackageV2Binding.VerifyReturnProof(
            proof, context, domain, entries, provisioning.PublicKey));
    }

    [Fact]
    public async Task VerificationRejectsChangedContextDomainAuditAndPublicKey()
    {
        AuditTransferContext context = Context();
        AuditTransferProvisioning provisioning = await AuditPackageV2Binding.ProvisionAsync(
            context, Password, TestContext.Current.CancellationToken);
        byte[] privateKey = await AuditPackageV2Binding.OpenPrivateKeyAsync(
            provisioning, Password, TestContext.Current.CancellationToken);
        IReadOnlyList<AuditChainEntry> entries = Chain(3);
        byte[] domain = "domain payload"u8.ToArray();
        AuditPackageV2Proof proof = AuditPackageV2Binding.CreateReturnProof(
            context, entries[0].Event.InstanceId, domain, entries, privateKey);
        AuditTransferProvisioning other = await AuditPackageV2Binding.ProvisionAsync(
            Context(), Password, TestContext.Current.CancellationToken);

        Assert.False(AuditPackageV2Binding.VerifyReturnProof(proof, context with { BaselineVersion = 2 }, domain, entries, provisioning.PublicKey));
        Assert.False(AuditPackageV2Binding.VerifyReturnProof(proof, context, "changed"u8, entries, provisioning.PublicKey));
        Assert.False(AuditPackageV2Binding.VerifyReturnProof(proof, context, domain, entries.Take(2).ToArray(), provisioning.PublicKey));
        Assert.False(AuditPackageV2Binding.VerifyReturnProof(proof, context, domain, entries, other.PublicKey));
    }

    [Fact]
    public async Task PrivateTransferKeyRequiresCorrectPassword()
    {
        AuditTransferProvisioning provisioning = await AuditPackageV2Binding.ProvisionAsync(
            Context(), Password, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => AuditPackageV2Binding.OpenPrivateKeyAsync(
            provisioning, "wrong transfer password", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletePackageEnvelopeIsEncryptedAndDirectionBound(bool isReturnPackage)
    {
        byte[] plaintext = "manifest, domain and audit bytes"u8.ToArray();
        byte[] envelope = await AuditPackageV2Binding.ProtectPackageAsync(
            plaintext, Password, isReturnPackage, TestContext.Current.CancellationToken);

        byte[] restored = await AuditPackageV2Binding.OpenPackageAsync(
            envelope, Password, isReturnPackage, TestContext.Current.CancellationToken);

        Assert.Equal(plaintext, restored);
        await Assert.ThrowsAsync<InvalidDataException>(() => AuditPackageV2Binding.OpenPackageAsync(
            envelope, Password, !isReturnPackage, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingOrDiscontinuousAuditSectionIsRejectedBeforeSigning()
    {
        AuditTransferContext context = Context();
        AuditTransferProvisioning provisioning = await AuditPackageV2Binding.ProvisionAsync(
            context, Password, TestContext.Current.CancellationToken);
        byte[] privateKey = await AuditPackageV2Binding.OpenPrivateKeyAsync(
            provisioning, Password, TestContext.Current.CancellationToken);
        IReadOnlyList<AuditChainEntry> entries = Chain(3);

        Assert.Throws<ArgumentException>(() => AuditPackageV2Binding.CreateReturnProof(
            context, entries[0].Event.InstanceId, [1], [], privateKey));
        Assert.Throws<ArgumentException>(() => AuditPackageV2Binding.CreateReturnProof(
            context, entries[0].Event.InstanceId, [1], [entries[0], entries[2]], privateKey));
    }

    [Fact]
    public void DeduplicationAcceptsOnlyByteIdenticalIdentity()
    {
        AuditChainEntry entry = Chain(1)[0];
        AuditChainEntry changed = entry with { Event = entry.Event with { Result = "denied" } };

        Assert.Equal(AuditTransferDuplicateStatus.New,
            AuditTransferDeduplication.Classify(entry, null, entry.Event.InstanceId));
        Assert.Equal(AuditTransferDuplicateStatus.Identical,
            AuditTransferDeduplication.Classify(entry, entry, entry.Event.InstanceId));
        Assert.Equal(AuditTransferDuplicateStatus.Conflict,
            AuditTransferDeduplication.Classify(changed, entry, entry.Event.InstanceId));
    }

    private static AuditTransferContext Context() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

    private static IReadOnlyList<AuditChainEntry> Chain(int count)
    {
        Guid instanceId = Guid.NewGuid();
        var entries = new List<AuditChainEntry>();
        byte[] head = new byte[32];
        for (var sequence = 1; sequence <= count; sequence++)
        {
            var auditEvent = new AuditEventData(
                Guid.NewGuid(), DateTimeOffset.UnixEpoch.AddSeconds(sequence), "offline.event", "success",
                null, Guid.NewGuid(), Guid.NewGuid(), null, null, "local", instanceId, Guid.NewGuid(), null, null,
                new Dictionary<string, string>());
            AuditChainEntry entry = AuditHmacChain.Append(auditEvent, sequence, head, "audit-key", AuditKey);
            entries.Add(entry);
            head = entry.Hmac;
        }
        return entries;
    }
}
