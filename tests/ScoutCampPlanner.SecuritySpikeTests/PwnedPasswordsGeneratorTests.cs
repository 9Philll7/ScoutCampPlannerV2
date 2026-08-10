using System.Globalization;
using System.Text;
using ScoutCampPlanner.PasswordDenylist;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class PwnedPasswordsGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_SelectsMostFrequentAndAddsProductSpecificValues()
    {
        const string source = """
0000000000000000000000000000000000000001:10
0000000000000000000000000000000000000002:50
0000000000000000000000000000000000000003:20
0000000000000000000000000000000000000004:40
0000000000000000000000000000000000000005:30

""";

        var (result, file) = await Generate(source, 3, ["ScoutCampPlanner"]);
        var denylist = DenylistFile.Open(file);

        Assert.Equal(5, result.SourceRecordCount);
        Assert.Equal(3, result.SelectedHibpEntryCount);
        Assert.Equal(1, result.ProductSpecificEntryCount);
        Assert.Equal(4, denylist.EntryCount);
        Assert.True(denylist.Contains("ScoutCampPlanner"));
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file)), result.Sha256);
    }

    [Fact]
    public async Task GenerateAsync_IsDeterministicAndUsesHashOrderAsTieBreaker()
    {
        const string source = """
0000000000000000000000000000000000000001:10
0000000000000000000000000000000000000002:10
0000000000000000000000000000000000000003:10

""";

        var (_, first) = await Generate(source, 2, []);
        var (_, second) = await Generate(source, 2, []);

        Assert.Equal(first, second);
        byte[] expectedFirstHash = Convert.FromHexString("0000000000000000000000000000000000000001");
        byte[] expectedSecondHash = Convert.FromHexString("0000000000000000000000000000000000000002");
        Assert.True(first.AsSpan().IndexOf(expectedFirstHash) >= 0);
        Assert.True(first.AsSpan().IndexOf(expectedSecondHash) >= 0);
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000002:1\n0000000000000000000000000000000000000001:2\n")]
    [InlineData("0000000000000000000000000000000000000001:1\n0000000000000000000000000000000000000001:2\n")]
    public async Task GenerateAsync_RejectsUnsortedOrDuplicateSource(string source)
    {
        var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await Generate(source, 1, []));

        Assert.Contains("sorted and unique", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-a-hash:1\n")]
    [InlineData("0000000000000000000000000000000000000001:0\n")]
    [InlineData("000000000000000000000000000000000000000g:1\n")]
    [InlineData("0000000000000000000000000000000000000001:1:2\n")]
    public async Task GenerateAsync_RejectsMalformedSource(string source)
    {
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await Generate(source, 1, []));
    }

    [Fact]
    public async Task GenerateAsync_StreamsLargeSourceThroughBoundedTopNSelection()
    {
        const int sourceCount = 100_000;
        const int selectedCount = 1_000;
        var source = new StringBuilder(capacity: sourceCount * 45);
        for (var index = 0; index < sourceCount; index++)
        {
            source.Append(index.ToString("X40", CultureInfo.InvariantCulture));
            source.Append(':');
            source.Append((index + 1).ToString(CultureInfo.InvariantCulture));
            source.Append('\n');
        }

        var (result, file) = await Generate(source.ToString(), selectedCount, []);
        var denylist = DenylistFile.Open(file);

        Assert.Equal(sourceCount, result.SourceRecordCount);
        Assert.Equal(selectedCount, result.SelectedHibpEntryCount);
        Assert.Equal(selectedCount, denylist.EntryCount);
        Assert.InRange(file.Length, 20_000, 21_000);
    }

    private static async Task<(DenylistGenerationResult Result, byte[] File)> Generate(
        string source,
        int entryCount,
        string[] productSpecificPasswords)
    {
        using var reader = new StringReader(source);
        using var destination = new MemoryStream();
        var result = await PwnedPasswordsGenerator.GenerateAsync(
            reader,
            destination,
            new DenylistGenerationOptions("fixture-v1", new DateOnly(2026, 8, 10), entryCount),
            productSpecificPasswords);
        return (result, destination.ToArray());
    }
}
