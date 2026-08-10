using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ScoutCampPlanner.PasswordDenylist;

public static class PwnedPasswordsGenerator
{
    private const int Sha1HexLength = 40;

    public static async Task<DenylistGenerationResult> GenerateAsync(
        TextReader source,
        Stream destination,
        DenylistGenerationOptions options,
        IEnumerable<string> productSpecificPasswords,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(productSpecificPasswords);

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        }

        if (options.HibpEntryCount is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "HIBP entry count must be between 1 and 1,000,000.");
        }

        string[] distinctProductSpecificPasswords = productSpecificPasswords
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (distinctProductSpecificPasswords.Any(password => password is null) ||
            options.HibpEntryCount > 1_000_000 - distinctProductSpecificPasswords.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(productSpecificPasswords),
                "HIBP and product-specific entries must fit within the 1,000,000-entry format limit.");
        }

        var candidates = new PriorityQueue<DenylistSourceEntry, CandidatePriority>(
            options.HibpEntryCount,
            CandidatePriorityComparer.Instance);
        string? previousHash = null;
        long sourceRecordCount = 0;
        long lineNumber = 0;

        while (await source.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var entry = ParseLine(line, lineNumber);
            if (previousHash is not null && string.CompareOrdinal(previousHash, entry.Sha1Hash) >= 0)
            {
                throw new InvalidDataException(
                    $"HIBP source must be strictly sorted and unique; violation at line {lineNumber}.");
            }

            previousHash = entry.Sha1Hash;
            sourceRecordCount++;
            AddCandidate(candidates, entry, options.HibpEntryCount);
        }

        if (sourceRecordCount == 0)
        {
            throw new InvalidDataException("HIBP source contains no records.");
        }

        DenylistSourceEntry[] selected = candidates.UnorderedItems
            .Select(item => item.Element)
            .ToArray();
        var selectedHashes = selected
            .Select(entry => entry.Sha1Hash)
            .ToHashSet(StringComparer.Ordinal);
        var additions = new List<DenylistSourceEntry>();

        foreach (string password in distinctProductSpecificPasswords)
        {
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            try
            {
                string hash = Convert.ToHexString(SHA1.HashData(passwordBytes));
                if (selectedHashes.Add(hash))
                {
                    additions.Add(new DenylistSourceEntry(hash, long.MaxValue));
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordBytes);
            }
        }

        DenylistSourceEntry[] outputEntries = selected.Concat(additions).ToArray();
        byte[] file = DenylistFile.Create(
            outputEntries,
            options.DatasetVersion,
            options.SourceDate,
            outputEntries.Length);
        await destination.WriteAsync(file, cancellationToken);

        return new DenylistGenerationResult(
            sourceRecordCount,
            selected.Length,
            additions.Count,
            outputEntries.Length,
            file.Length,
            Convert.ToHexString(SHA256.HashData(file)));
    }

    private static DenylistSourceEntry ParseLine(string line, long lineNumber)
    {
        var separator = line.IndexOf(':');
        if (separator != Sha1HexLength || line.IndexOf(':', separator + 1) >= 0)
        {
            throw new InvalidDataException($"HIBP source line {lineNumber} does not use SHA1:count format.");
        }

        string hash = line[..separator];
        if (hash.Any(character => character is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
        {
            throw new InvalidDataException($"HIBP source line {lineNumber} contains a non-canonical SHA-1 hash.");
        }

        if (!long.TryParse(
                line.AsSpan(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var occurrences) || occurrences < 1)
        {
            throw new InvalidDataException($"HIBP source line {lineNumber} contains an invalid occurrence count.");
        }

        return new DenylistSourceEntry(hash, occurrences);
    }

    private static void AddCandidate(
        PriorityQueue<DenylistSourceEntry, CandidatePriority> candidates,
        DenylistSourceEntry entry,
        int maximumCount)
    {
        var priority = new CandidatePriority(entry.Occurrences, entry.Sha1Hash);
        if (candidates.Count < maximumCount)
        {
            candidates.Enqueue(entry, priority);
            return;
        }

        candidates.TryPeek(out _, out var currentWorst);
        if (CandidatePriorityComparer.Instance.Compare(priority, currentWorst) > 0)
        {
            candidates.DequeueEnqueue(entry, priority);
        }
    }

    private readonly record struct CandidatePriority(long Occurrences, string Hash);

    private sealed class CandidatePriorityComparer : IComparer<CandidatePriority>
    {
        public static CandidatePriorityComparer Instance { get; } = new();

        public int Compare(CandidatePriority left, CandidatePriority right)
        {
            var occurrenceComparison = left.Occurrences.CompareTo(right.Occurrences);
            return occurrenceComparison != 0
                ? occurrenceComparison
                : -string.CompareOrdinal(left.Hash, right.Hash);
        }
    }
}

public sealed record DenylistGenerationOptions(
    string DatasetVersion,
    DateOnly SourceDate,
    int HibpEntryCount);

public sealed record DenylistGenerationResult(
    long SourceRecordCount,
    int SelectedHibpEntryCount,
    int ProductSpecificEntryCount,
    int OutputEntryCount,
    int OutputByteLength,
    string Sha256);
