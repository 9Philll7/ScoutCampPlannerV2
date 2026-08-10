using System.Text;
using System.Security.Cryptography;
using ScoutCampPlanner.SecuritySpike;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class DenylistFileTests
{
    [Fact]
    public void ProductionEntryCount_CreatesCompactFileAndSupportsLookup()
    {
        const int entryCount = 100_000;
        DenylistSourceEntry[] source = Enumerable.Range(0, entryCount)
            .Select(index => new DenylistSourceEntry(
                Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"synthetic-{index}"))),
                entryCount - index))
            .ToArray();

        byte[] file = DenylistFile.Create(
            source,
            "scale-test",
            new DateOnly(2026, 8, 10),
            entryCount);
        var denylist = DenylistFile.Open(file);

        Assert.Equal(entryCount, denylist.EntryCount);
        Assert.InRange(file.Length, 2_000_000, 2_001_000);
        Assert.True(denylist.Contains("synthetic-99999"));
        Assert.False(denylist.Contains("not-in-synthetic-list"));
    }

    [Fact]
    public void Create_SelectsMostFrequentEntriesAndRoundTripsMetadata()
    {
        byte[] file = CreateFile(maximumEntries: 3);

        var denylist = DenylistFile.Open(file);

        Assert.Equal("synthetic-2026-08", denylist.DatasetVersion);
        Assert.Equal(new DateOnly(2026, 8, 10), denylist.SourceDate);
        Assert.Equal(3, denylist.EntryCount);
        Assert.True(denylist.Contains("password"));
        Assert.True(denylist.Contains("123456"));
        Assert.True(denylist.Contains("qwerty"));
        Assert.False(denylist.Contains("synthetic-test-only"));
    }

    [Fact]
    public void Contains_UsesExactUtf8PasswordWithoutCaseFoldingOrNormalization()
    {
        byte[] file = CreateFile(maximumEntries: 5);
        var denylist = DenylistFile.Open(file);

        Assert.True(denylist.Contains("ScoutCampPlanner"));
        Assert.False(denylist.Contains("scoutcampplanner"));
        Assert.False(denylist.Contains("ScoutCampPlanner "));
        Assert.False(denylist.Contains("ScöutCampPlanner"));
    }

    [Fact]
    public void Open_RejectsCorruptedFile()
    {
        byte[] file = CreateFile(maximumEntries: 5);
        file[25] ^= 0xFF;

        var exception = Assert.Throws<InvalidDataException>(() => DenylistFile.Open(file));

        Assert.Contains("integrity", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BinaryFile_DoesNotContainFixturePlaintext()
    {
        byte[] file = CreateFile(maximumEntries: 5);

        Assert.Equal(-1, file.AsSpan().IndexOf("password"u8));
        Assert.Equal(-1, file.AsSpan().IndexOf("ScoutCampPlanner"u8));
    }

    [Fact]
    public void Create_DeduplicatesHashAndKeepsHighestOccurrenceCountForSelection()
    {
        var source = LoadFixture().Append(new DenylistSourceEntry(
            "CD15D2A47598B870255A01171B537AC7F28EC872",
            1_000));

        byte[] file = DenylistFile.Create(source, "dedup-test", new DateOnly(2026, 8, 10), 1);
        var denylist = DenylistFile.Open(file);

        Assert.Equal(1, denylist.EntryCount);
        Assert.True(denylist.Contains("synthetic-test-only"));
    }

    private static byte[] CreateFile(int maximumEntries) =>
        DenylistFile.Create(
            LoadFixture(),
            "synthetic-2026-08",
            new DateOnly(2026, 8, 10),
            maximumEntries);

    private static IReadOnlyList<DenylistSourceEntry> LoadFixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "denylist-source.txt");
        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .Select(line => line.Split(':'))
            .Select(parts => new DenylistSourceEntry(parts[0], long.Parse(parts[1])))
            .ToArray();
    }
}
