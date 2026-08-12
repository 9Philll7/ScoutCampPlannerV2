using System.Security.Cryptography;
using ScoutCampPlanner.AuditSecuritySpike;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class AuditHmacChainTests
{
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void CanonicalEncodingIsIndependentOfMetadataInsertionOrder()
    {
        var first = Fixture(new Dictionary<string, string>
        {
            ["newRole"] = "TenantAdmin",
            ["membershipId"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        });
        var second = Fixture(new Dictionary<string, string>
        {
            ["membershipId"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            ["newRole"] = "TenantAdmin",
        });

        byte[] firstBytes = AuditCanonicalEncoding.Encode(1, new byte[32], "key-1", first);
        byte[] secondBytes = AuditCanonicalEncoding.Encode(1, new byte[32], "key-1", second);

        Assert.Equal(firstBytes, secondBytes);
        Assert.Equal(
            "A4B35365C0824C46D93804CAC8CB33C98F632C2FB76D053ACA0F562E107B4C0E",
            Convert.ToHexString(SHA256.HashData(firstBytes)));
    }

    [Fact]
    public void CompleteChainVerifiesAgainstProtectedHead()
    {
        var entries = CreateChain(3);

        var result = Verify(entries, entries[^1].Hmac, Key);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("modified-event")]
    [InlineData("modified-hmac")]
    [InlineData("deleted-middle")]
    [InlineData("deleted-tail")]
    [InlineData("inserted")]
    [InlineData("reordered")]
    [InlineData("wrong-key")]
    public void ChainRejectsTampering(string mutation)
    {
        var entries = CreateChain(3).ToList();
        byte[] expectedHead = entries[^1].Hmac.ToArray();
        byte[] verificationKey = Key;

        switch (mutation)
        {
            case "modified-event":
                entries[1] = entries[1] with { Event = entries[1].Event with { Result = "denial" } };
                break;
            case "modified-hmac":
                entries[1].Hmac[0] ^= 0xff;
                break;
            case "deleted-middle":
                entries.RemoveAt(1);
                break;
            case "deleted-tail":
                entries.RemoveAt(2);
                break;
            case "inserted":
                entries.Insert(1, entries[0]);
                break;
            case "reordered":
                (entries[0], entries[1]) = (entries[1], entries[0]);
                break;
            case "wrong-key":
                verificationKey = RandomNumberGenerator.GetBytes(32);
                break;
        }

        var result = Verify(entries, expectedHead, verificationKey);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ChainRejectsUnknownFormatAndKey()
    {
        var entries = CreateChain(1);
        var unsupported = new[] { entries[0] with { FormatVersion = 2 } };

        Assert.Equal("unsupported-format", Verify(unsupported, entries[0].Hmac, Key).Failure);
        Assert.Equal(
            "unknown-key",
            AuditHmacChain.Verify(entries, new byte[32], entries[0].Hmac, _ => null).Failure);
    }

    private static IReadOnlyList<AuditChainEntry> CreateChain(int count)
    {
        var entries = new List<AuditChainEntry>();
        byte[] head = new byte[32];
        for (var sequence = 1; sequence <= count; sequence++)
        {
            var entry = AuditHmacChain.Append(Fixture(new Dictionary<string, string>
            {
                ["sequenceFixture"] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }), sequence, head, "key-1", Key);
            entries.Add(entry);
            head = entry.Hmac;
        }

        return entries;
    }

    private static AuditChainVerification Verify(
        IReadOnlyList<AuditChainEntry> entries,
        byte[] expectedHead,
        byte[] key) =>
        AuditHmacChain.Verify(entries, new byte[32], expectedHead, keyId => keyId == "key-1" ? key : null);

    private static AuditEventData Fixture(IReadOnlyDictionary<string, string> metadata) => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        new DateTimeOffset(2026, 8, 12, 12, 34, 56, TimeSpan.Zero),
        "tenant.role.changed",
        "success",
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        null,
        "tenant-membership",
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        "cloud",
        Guid.Parse("55555555-5555-5555-5555-555555555555"),
        Guid.Parse("66666666-6666-6666-6666-666666666666"),
        3,
        1,
        metadata);
}
