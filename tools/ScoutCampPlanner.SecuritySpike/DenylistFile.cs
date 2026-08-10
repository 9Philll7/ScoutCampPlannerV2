using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ScoutCampPlanner.SecuritySpike;

public static class DenylistFile
{
    private static readonly byte[] Magic = "SCPDLST1"u8.ToArray();
    private const ushort CurrentFormatVersion = 1;
    private const byte Sha1LookupAlgorithm = 1;
    private const int LookupHashLength = 20;
    private const int IntegrityHashLength = 32;
    private const int FixedHeaderLength = 20;
    private const int MaximumVersionByteLength = 64;
    private const int MaximumEntryCount = 1_000_000;
    private const int MaximumFileLength =
        FixedHeaderLength + MaximumVersionByteLength + sizeof(int) +
        MaximumEntryCount * LookupHashLength + IntegrityHashLength;

    public static byte[] Create(
        IEnumerable<DenylistSourceEntry> sourceEntries,
        string datasetVersion,
        DateOnly sourceDate,
        int maximumEntries)
    {
        ArgumentNullException.ThrowIfNull(sourceEntries);
        byte[] versionBytes = Encoding.UTF8.GetBytes(datasetVersion);
        if (versionBytes.Length is 0 or > MaximumVersionByteLength)
        {
            throw new ArgumentOutOfRangeException(nameof(datasetVersion));
        }

        if (maximumEntries is < 1 or > MaximumEntryCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        }

        byte[][] selectedHashes = sourceEntries
            .Select(ParseSourceEntry)
            .GroupBy(entry => Convert.ToHexString(entry.Hash), StringComparer.Ordinal)
            .Select(group => new ParsedEntry(group.First().Hash, group.Max(entry => entry.Occurrences)))
            .OrderByDescending(entry => entry.Occurrences)
            .ThenBy(entry => Convert.ToHexString(entry.Hash), StringComparer.Ordinal)
            .Take(maximumEntries)
            .Select(entry => entry.Hash)
            .Order(ByteArrayComparer.Instance)
            .ToArray();

        var contentLength = checked(
            FixedHeaderLength + versionBytes.Length + sizeof(int) + selectedHashes.Length * LookupHashLength);
        byte[] file = new byte[checked(contentLength + IntegrityHashLength)];
        var span = file.AsSpan();

        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt16BigEndian(span[8..], CurrentFormatVersion);
        span[10] = Sha1LookupAlgorithm;
        span[11] = 0;
        BinaryPrimitives.WriteInt32BigEndian(span[12..], sourceDate.Year * 10_000 + sourceDate.Month * 100 + sourceDate.Day);
        BinaryPrimitives.WriteUInt16BigEndian(span[16..], checked((ushort)versionBytes.Length));
        BinaryPrimitives.WriteUInt16BigEndian(span[18..], 0);
        versionBytes.CopyTo(span[FixedHeaderLength..]);

        var entryCountOffset = FixedHeaderLength + versionBytes.Length;
        BinaryPrimitives.WriteInt32BigEndian(span[entryCountOffset..], selectedHashes.Length);
        var hashesOffset = entryCountOffset + sizeof(int);
        foreach (byte[] hash in selectedHashes)
        {
            hash.CopyTo(span[hashesOffset..]);
            hashesOffset += LookupHashLength;
        }

        SHA256.HashData(span[..contentLength], span[contentLength..]);
        return file;
    }

    public static Denylist Open(ReadOnlySpan<byte> file)
    {
        if (file.Length > MaximumFileLength)
        {
            throw new InvalidDataException("Denylist file exceeds the supported size.");
        }

        if (file.Length < FixedHeaderLength + sizeof(int) + IntegrityHashLength)
        {
            throw new InvalidDataException("Denylist file is truncated.");
        }

        var content = file[..^IntegrityHashLength];
        Span<byte> actualIntegrityHash = stackalloc byte[IntegrityHashLength];
        SHA256.HashData(content, actualIntegrityHash);
        if (!CryptographicOperations.FixedTimeEquals(actualIntegrityHash, file[^IntegrityHashLength..]))
        {
            throw new InvalidDataException("Denylist integrity check failed.");
        }

        if (!file[..Magic.Length].SequenceEqual(Magic))
        {
            throw new InvalidDataException("Denylist magic value is invalid.");
        }

        var formatVersion = BinaryPrimitives.ReadUInt16BigEndian(file[8..]);
        if (formatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException($"Denylist format version {formatVersion} is not supported.");
        }

        if (file[10] != Sha1LookupAlgorithm || file[11] != 0 || BinaryPrimitives.ReadUInt16BigEndian(file[18..]) != 0)
        {
            throw new InvalidDataException("Denylist header contains unsupported values.");
        }

        int sourceDateValue = BinaryPrimitives.ReadInt32BigEndian(file[12..]);
        DateOnly sourceDate;
        try
        {
            sourceDate = new DateOnly(sourceDateValue / 10_000, sourceDateValue / 100 % 100, sourceDateValue % 100);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("Denylist source date is invalid.", exception);
        }

        int versionLength = BinaryPrimitives.ReadUInt16BigEndian(file[16..]);
        if (versionLength is < 1 or > MaximumVersionByteLength)
        {
            throw new InvalidDataException("Denylist dataset version length is invalid.");
        }

        var entryCountOffset = FixedHeaderLength + versionLength;
        if (entryCountOffset + sizeof(int) > content.Length)
        {
            throw new InvalidDataException("Denylist header is truncated.");
        }

        string datasetVersion;
        try
        {
            datasetVersion = new UTF8Encoding(false, true).GetString(file.Slice(FixedHeaderLength, versionLength));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Denylist dataset version is not valid UTF-8.", exception);
        }

        int entryCount = BinaryPrimitives.ReadInt32BigEndian(file[entryCountOffset..]);
        if (entryCount is < 0 or > MaximumEntryCount)
        {
            throw new InvalidDataException("Denylist entry count is invalid.");
        }

        var hashesOffset = entryCountOffset + sizeof(int);
        var expectedContentLength = checked(hashesOffset + entryCount * LookupHashLength);
        if (content.Length != expectedContentLength)
        {
            throw new InvalidDataException("Denylist file length does not match its entry count.");
        }

        byte[] hashes = content[hashesOffset..].ToArray();
        EnsureStrictlySorted(hashes, entryCount);
        return new Denylist(datasetVersion, sourceDate, hashes, entryCount);
    }

    private static ParsedEntry ParseSourceEntry(DenylistSourceEntry entry)
    {
        if (entry.Occurrences < 1)
        {
            throw new InvalidDataException("Denylist occurrence count must be positive.");
        }

        byte[] hash;
        try
        {
            hash = Convert.FromHexString(entry.Sha1Hash);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Denylist source hash is not hexadecimal.", exception);
        }

        if (hash.Length != LookupHashLength)
        {
            throw new InvalidDataException("Denylist source hash must contain 20 bytes.");
        }

        return new ParsedEntry(hash, entry.Occurrences);
    }

    private static void EnsureStrictlySorted(byte[] hashes, int entryCount)
    {
        for (var index = 1; index < entryCount; index++)
        {
            var previous = hashes.AsSpan((index - 1) * LookupHashLength, LookupHashLength);
            var current = hashes.AsSpan(index * LookupHashLength, LookupHashLength);
            if (previous.SequenceCompareTo(current) >= 0)
            {
                throw new InvalidDataException("Denylist hashes are not strictly sorted and unique.");
            }
        }
    }

    private sealed record ParsedEntry(byte[] Hash, long Occurrences);

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public int Compare(byte[]? left, byte[]? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            return left.AsSpan().SequenceCompareTo(right);
        }
    }
}

public sealed class Denylist
{
    private const int LookupHashLength = 20;
    private readonly byte[] hashes;

    internal Denylist(string datasetVersion, DateOnly sourceDate, byte[] hashes, int entryCount)
    {
        DatasetVersion = datasetVersion;
        SourceDate = sourceDate;
        this.hashes = hashes;
        EntryCount = entryCount;
    }

    public string DatasetVersion { get; }

    public DateOnly SourceDate { get; }

    public int EntryCount { get; }

    public bool Contains(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        Span<byte> lookupHash = stackalloc byte[LookupHashLength];
        try
        {
            SHA1.HashData(passwordBytes, lookupHash);
            return BinarySearch(lookupHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(lookupHash);
        }
    }

    private bool BinarySearch(ReadOnlySpan<byte> lookupHash)
    {
        var lower = 0;
        var upper = EntryCount - 1;
        while (lower <= upper)
        {
            var middle = lower + ((upper - lower) / 2);
            var candidate = hashes.AsSpan(middle * LookupHashLength, LookupHashLength);
            var comparison = candidate.SequenceCompareTo(lookupHash);
            if (comparison == 0)
            {
                return true;
            }

            if (comparison < 0)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle - 1;
            }
        }

        return false;
    }
}

public sealed record DenylistSourceEntry(string Sha1Hash, long Occurrences);
