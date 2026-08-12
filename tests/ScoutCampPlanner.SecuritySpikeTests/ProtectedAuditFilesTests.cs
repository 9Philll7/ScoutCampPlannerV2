using ScoutCampPlanner.AuditSecuritySpike;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class ProtectedAuditFilesTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void CheckpointRoundTripsAndRejectsModification()
    {
        var checkpoint = new AuditCheckpoint(Guid.NewGuid(), 42, Key.ToArray(), "key-1", 1);
        byte[] file = ProtectedAuditFiles.SerializeCheckpoint(checkpoint, Key);

        var restored = ProtectedAuditFiles.DeserializeCheckpoint(file, ResolveKey);
        Assert.Equal(checkpoint.InstanceId, restored.InstanceId);
        Assert.Equal(checkpoint.Sequence, restored.Sequence);
        Assert.Equal(checkpoint.Head, restored.Head);
        Assert.Equal(checkpoint.KeyId, restored.KeyId);
        Assert.Equal(checkpoint.FormatVersion, restored.FormatVersion);

        file[file.Length / 2] ^= 1;
        Assert.ThrowsAny<Exception>(() => ProtectedAuditFiles.DeserializeCheckpoint(file, ResolveKey));
    }

    [Fact]
    public void CheckpointRejectsWrongOrUnavailableKey()
    {
        var checkpoint = new AuditCheckpoint(Guid.NewGuid(), 1, new byte[32], "key-1", 1);
        byte[] file = ProtectedAuditFiles.SerializeCheckpoint(checkpoint, Key);

        Assert.Throws<InvalidDataException>(() =>
            ProtectedAuditFiles.DeserializeCheckpoint(file, _ => new byte[32]));
        Assert.Throws<InvalidDataException>(() =>
            ProtectedAuditFiles.DeserializeCheckpoint(file, _ => null));
    }

    [Fact]
    public void KeyBundleRoundTripsActivePreparedAndHistoricalKeys()
    {
        var bundle = new AuditKeyBundle(
        [
            new AuditKey("active", Key.ToArray(), AuditKeyState.Active),
            new AuditKey("historical", Enumerable.Repeat((byte)7, 32).ToArray(), AuditKeyState.Historical),
        ]);
        bundle.Prepare("prepared", Enumerable.Repeat((byte)9, 32).ToArray());

        byte[] file = ProtectedAuditFiles.SerializeKeyBundle(bundle);
        var restored = ProtectedAuditFiles.DeserializeKeyBundle(file);

        Assert.Equal("active", restored.Active.Id);
        Assert.Equal("prepared", restored.Prepared?.Id);
        Assert.Equal(3, restored.Keys.Count);
        Assert.Equal(Key, restored.Resolve("active"));
    }

    [Theory]
    [InlineData("{\"Version\":2,\"Keys\":[]}")]
    [InlineData("{\"Version\":1,\"Keys\":[]}")]
    [InlineData("{\"Version\":1,\"Keys\":[{\"Id\":\"x\",\"State\":\"Active\",\"Material\":\"AA==\"}]}")]
    public void KeyBundleRejectsUnsupportedOrInvalidContent(string file)
    {
        Assert.Throws<InvalidDataException>(() =>
            ProtectedAuditFiles.DeserializeKeyBundle(System.Text.Encoding.UTF8.GetBytes(file)));
    }

    [Fact]
    public async Task AtomicWriteReplacesCompleteFileAndLeavesNoTemporaryFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"scoutcampplanner-audit-files-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "checkpoint.json");
        try
        {
            await ProtectedAuditFiles.WriteAtomicallyAsync(
                path, "first"u8.ToArray(), TestContext.Current.CancellationToken);
            await ProtectedAuditFiles.WriteAtomicallyAsync(
                path, "second"u8.ToArray(), TestContext.Current.CancellationToken);

            Assert.Equal("second", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static byte[]? ResolveKey(string keyId) => keyId == "key-1" ? Key : null;
}
