using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ScoutCampPlanner.Platform.Application.Authentication;

namespace ScoutCampPlanner.Platform.Infrastructure.Authentication;

public sealed class BinaryPasswordDenylist : IPasswordDenylist
{
    private static readonly byte[] Magic = "SCPDLST1"u8.ToArray();
    private const int HashLength = 20;
    private readonly byte[] hashes;

    private BinaryPasswordDenylist(byte[] hashes, int entryCount)
    {
        this.hashes = hashes;
        EntryCount = entryCount;
    }

    public int EntryCount { get; }

    public static BinaryPasswordDenylist Load(ReadOnlySpan<byte> file)
    {
        const int fixedHeaderLength = 20;
        const int integrityLength = 32;
        if (file.Length < fixedHeaderLength + sizeof(int) + integrityLength ||
            file.Length > fixedHeaderLength + 64 + sizeof(int) + 1_000_000 * HashLength + integrityLength)
        {
            throw new InvalidDataException("Denylist file length is invalid.");
        }

        var content = file[..^integrityLength];
        Span<byte> integrity = stackalloc byte[integrityLength];
        SHA256.HashData(content, integrity);
        if (!CryptographicOperations.FixedTimeEquals(integrity, file[^integrityLength..]))
        {
            throw new InvalidDataException("Denylist integrity check failed.");
        }

        if (!file[..Magic.Length].SequenceEqual(Magic) ||
            BinaryPrimitives.ReadUInt16BigEndian(file[8..]) != 1 ||
            file[10] != 1 || file[11] != 0 ||
            BinaryPrimitives.ReadUInt16BigEndian(file[18..]) != 0)
        {
            throw new InvalidDataException("Denylist header is not supported.");
        }

        int sourceDateValue = BinaryPrimitives.ReadInt32BigEndian(file[12..]);
        try
        {
            _ = new DateOnly(sourceDateValue / 10_000, sourceDateValue / 100 % 100, sourceDateValue % 100);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("Denylist source date is invalid.", exception);
        }

        int versionLength = BinaryPrimitives.ReadUInt16BigEndian(file[16..]);
        int countOffset = fixedHeaderLength + versionLength;
        if (versionLength is < 1 or > 64 || countOffset + sizeof(int) > content.Length)
        {
            throw new InvalidDataException("Denylist header is truncated.");
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetString(file.Slice(fixedHeaderLength, versionLength));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Denylist dataset version is not valid UTF-8.", exception);
        }

        int entryCount = BinaryPrimitives.ReadInt32BigEndian(file[countOffset..]);
        if (entryCount is < 0 or > 1_000_000)
        {
            throw new InvalidDataException("Denylist entry count is invalid.");
        }

        int hashesOffset = countOffset + sizeof(int);
        if (content.Length != hashesOffset + entryCount * HashLength)
        {
            throw new InvalidDataException("Denylist file length does not match its entry count.");
        }

        byte[] hashes = content[hashesOffset..].ToArray();
        for (var index = 1; index < entryCount; index++)
        {
            if (hashes.AsSpan((index - 1) * HashLength, HashLength)
                    .SequenceCompareTo(hashes.AsSpan(index * HashLength, HashLength)) >= 0)
            {
                throw new InvalidDataException("Denylist hashes are not strictly sorted and unique.");
            }
        }

        return new BinaryPasswordDenylist(hashes, entryCount);
    }

    public bool Contains(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        Span<byte> lookupHash = stackalloc byte[HashLength];
        try
        {
            SHA1.HashData(passwordBytes, lookupHash);
            var lower = 0;
            var upper = EntryCount - 1;
            while (lower <= upper)
            {
                var middle = lower + (upper - lower) / 2;
                int comparison = hashes.AsSpan(middle * HashLength, HashLength).SequenceCompareTo(lookupHash);
                if (comparison == 0)
                {
                    return true;
                }

                if (comparison < 0) lower = middle + 1;
                else upper = middle - 1;
            }

            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(lookupHash);
        }
    }
}
