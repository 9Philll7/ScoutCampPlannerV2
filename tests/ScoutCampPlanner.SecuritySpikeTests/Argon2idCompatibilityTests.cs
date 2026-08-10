using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class Argon2idCompatibilityTests
{
    [Fact]
    public void DeriveBytes_MatchesOfficialArgon2idVersion19TestVector()
    {
        byte[] password = Enumerable.Repeat((byte)0x01, 32).ToArray();
        byte[] salt = Enumerable.Repeat((byte)0x02, 16).ToArray();
        byte[] secret = Enumerable.Repeat((byte)0x03, 8).ToArray();
        byte[] associatedData = Enumerable.Repeat((byte)0x04, 12).ToArray();
        byte[] expected = Convert.FromHexString(
            "0D640DF58D78766C08C037A34A8B53C9D01EF0452D75B65EB52520E96B01E659");

        using var argon2 = new Argon2id(password)
        {
            Salt = salt,
            KnownSecret = secret,
            AssociatedData = associatedData,
            DegreeOfParallelism = 4,
            Iterations = 3,
            MemorySize = 32,
        };

        byte[] actual = argon2.GetBytes(32);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IndependentSalts_ProduceIndependentVerifiers()
    {
        byte[] password = "same-password"u8.ToArray();
        byte[] first = Derive(password, Convert.FromHexString("00112233445566778899AABBCCDDEEFF"));
        byte[] second = Derive(password, Convert.FromHexString("FFEEDDCCBBAA99887766554433221100"));

        Assert.False(CryptographicOperations.FixedTimeEquals(first, second));
    }

    [Fact]
    public void StoredParameters_ReproduceVerifierAndPermitUpgradeDetection()
    {
        var stored = new StoredParameters(19, 19 * 1024, 2, 1, 16, 32);
        var current = new StoredParameters(19, 64 * 1024, 3, 1, 16, 32);
        byte[] password = "parameter-test"u8.ToArray();
        byte[] salt = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

        byte[] original = Derive(password, salt, stored.MemoryKiB, stored.Iterations, stored.Parallelism);
        byte[] repeated = Derive(password, salt, stored.MemoryKiB, stored.Iterations, stored.Parallelism);

        Assert.True(CryptographicOperations.FixedTimeEquals(original, repeated));
        Assert.True(stored.RequiresUpgradeTo(current));
    }

    private static byte[] Derive(
        byte[] password,
        byte[] salt,
        int memoryKiB = 19 * 1024,
        int iterations = 2,
        int parallelism = 1)
    {
        using var argon2 = new Argon2id(password)
        {
            Salt = salt,
            MemorySize = memoryKiB,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };

        return argon2.GetBytes(32);
    }

    private sealed record StoredParameters(
        int Version,
        int MemoryKiB,
        int Iterations,
        int Parallelism,
        int SaltLength,
        int DerivedKeyLength)
    {
        public bool RequiresUpgradeTo(StoredParameters current) =>
            Version < current.Version ||
            MemoryKiB < current.MemoryKiB ||
            Iterations < current.Iterations ||
            Parallelism < current.Parallelism ||
            SaltLength < current.SaltLength ||
            DerivedKeyLength < current.DerivedKeyLength;
    }
}
