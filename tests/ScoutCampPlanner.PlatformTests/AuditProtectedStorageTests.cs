using System.Security.Cryptography;
using System.Runtime.Versioning;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using System.Text.Json;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class AuditProtectedStorageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"scoutcampplanner-audit-{Guid.NewGuid():N}");

    [Fact]
    public async Task ExistingInstanceWithMissingMaterialStartsBlockedWithoutCreatingKeys()
    {
        var store = new FileAuditProtectedMaterialStore(_directory, new PlainAuditKeyBundleProtection());
        var initializer = new AuditProtectedMaterialInitializer(store);
        var factoryCalled = false;

        AuditStartupResult result = await initializer.InitializeAsync(
            AuditInstanceStartMode.ExistingInstance,
            () => { factoryCalled = true; return Material(); },
            TestContext.Current.CancellationToken);

        Assert.False(result.IsReady);
        Assert.Equal("protected-material-missing", result.Status);
        Assert.False(factoryCalled);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task OnlyExplicitNewInstanceCreatesAndReloadsMaterial()
    {
        var store = new FileAuditProtectedMaterialStore(_directory, new PlainAuditKeyBundleProtection());
        var initializer = new AuditProtectedMaterialInitializer(store);
        AuditProtectedMaterial expected = Material();

        AuditStartupResult created = await initializer.InitializeAsync(
            AuditInstanceStartMode.ExplicitNewInstance, () => expected, TestContext.Current.CancellationToken);
        AuditStartupResult loaded = await initializer.InitializeAsync(
            AuditInstanceStartMode.ExistingInstance,
            () => throw new InvalidOperationException("Must not replace existing keys."),
            TestContext.Current.CancellationToken);

        Assert.True(created.IsReady);
        Assert.Equal("protected-material-created", created.Status);
        Assert.True(loaded.IsReady);
        Assert.Equal(expected.KeyBundle, loaded.Material!.KeyBundle);
        Assert.Equal(expected.Checkpoint, loaded.Material.Checkpoint);
    }

    [Fact]
    public async Task PartialFileStateIsInvalidAndCannotTriggerAutomaticRegeneration()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(Path.Combine(_directory, "audit-keys.bin"), [1, 2, 3],
            TestContext.Current.CancellationToken);
        var initializer = new AuditProtectedMaterialInitializer(
            new FileAuditProtectedMaterialStore(_directory, new PlainAuditKeyBundleProtection()));

        AuditStartupResult result = await initializer.InitializeAsync(
            AuditInstanceStartMode.ExplicitNewInstance, Material, TestContext.Current.CancellationToken);

        Assert.False(result.IsReady);
        Assert.Equal("protected-material-invalid", result.Status);
    }

    [Fact]
    public async Task ConfiguredStoreCannotReplaceDeploymentKey()
    {
        byte[] configuredKey = [1, 2, 3, 4];
        var store = new ConfiguredAuditProtectedMaterialStore(
            Convert.ToBase64String(configuredKey), Path.Combine(_directory, "checkpoint.json"));
        await store.SaveAsync(new AuditProtectedMaterial(configuredKey, [5, 6]), TestContext.Current.CancellationToken);

        AuditProtectedMaterialLoadResult loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AuditProtectedMaterialStatus.Available, loaded.Status);
        Assert.Equal(configuredKey, loaded.Material!.KeyBundle);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(
            new AuditProtectedMaterial([9, 9, 9, 9], [7]), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void WindowsDpapiRoundTripsAndRejectsModifiedPayload()
    {
        if (!OperatingSystem.IsWindows()) return;
        VerifyWindowsDpapi();
    }

    [Fact]
    public async Task ProductiveSigningKeyProviderLoadsExactlyOneActiveKey()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] bundle = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = 1,
            Keys = new[] { new { Id = "active-key", State = "Active", Material = Convert.ToBase64String(key) } }
        });
        var provider = new ProtectedMaterialAuditSigningKeyProvider(
            new FixedProtectedMaterialStore(new AuditProtectedMaterial(bundle, [1])));

        AuditSigningKey loaded = await provider.GetActiveAsync(TestContext.Current.CancellationToken);

        Assert.Equal("active-key", loaded.Id);
        Assert.Equal(key, loaded.Material);
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyWindowsDpapi()
    {
        var protection = new WindowsDpapiAuditKeyBundleProtection();
        byte[] plaintext = RandomNumberGenerator.GetBytes(32);
        byte[] protectedData = protection.Protect(plaintext);

        Assert.Equal(plaintext, protection.Unprotect(protectedData));
        protectedData[^1] ^= 1;
        Assert.Throws<CryptographicException>(() => protection.Unprotect(protectedData));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static AuditProtectedMaterial Material() => new([1, 2, 3, 4], [5, 6, 7, 8]);

    private sealed class FixedProtectedMaterialStore(AuditProtectedMaterial material) : IAuditProtectedMaterialStore
    {
        public Task<AuditProtectedMaterialLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuditProtectedMaterialLoadResult(AuditProtectedMaterialStatus.Available, material));

        public Task SaveAsync(AuditProtectedMaterial value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
