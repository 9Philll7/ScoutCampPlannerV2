using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using ScoutCampPlanner.Platform.Application.Authentication;

namespace ScoutCampPlanner.Platform.Infrastructure.Authentication;

public enum Argon2idOperatingMode
{
    Server,
    SingleDevice,
}

public sealed class Argon2idPasswordVerifier : IPasswordVerifier, IDisposable
{
    private const int Argon2Version = 19;
    private readonly Argon2idProfile profile;
    private readonly SemaphoreSlim derivationGate;

    public Argon2idPasswordVerifier(Argon2idOperatingMode operatingMode, int maximumConcurrency = 2)
    {
        if (maximumConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
        }

        profile = operatingMode switch
        {
            Argon2idOperatingMode.Server => new Argon2idProfile(64 * 1024, 3, 1, 16, 32),
            Argon2idOperatingMode.SingleDevice => new Argon2idProfile(19 * 1024, 2, 1, 16, 32),
            _ => throw new ArgumentOutOfRangeException(nameof(operatingMode)),
        };
        derivationGate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
    }

    public async ValueTask<string> CreateAsync(string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);
        byte[] salt = RandomNumberGenerator.GetBytes(profile.SaltLength);
        try
        {
            byte[] hash = await DeriveAsync(password, salt, profile, cancellationToken);
            try
            {
                return Format(profile, salt, hash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    public async ValueTask<PasswordVerificationResult> VerifyAsync(
        string password,
        string storedVerifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(storedVerifier);
        if (!TryParse(storedVerifier, out var parsed))
        {
            return new PasswordVerificationResult(false, false);
        }

        try
        {
            byte[] actual = await DeriveAsync(password, parsed.Salt, parsed.Profile, cancellationToken);
            try
            {
                bool isValid = CryptographicOperations.FixedTimeEquals(actual, parsed.Hash);
                return new PasswordVerificationResult(isValid, isValid && parsed.Profile != profile);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(parsed.Salt);
            CryptographicOperations.ZeroMemory(parsed.Hash);
        }
    }

    public void Dispose() => derivationGate.Dispose();

    private async ValueTask<byte[]> DeriveAsync(
        string password,
        byte[] salt,
        Argon2idProfile derivationProfile,
        CancellationToken cancellationToken)
    {
        await derivationGate.WaitAsync(cancellationToken);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = derivationProfile.MemoryKiB,
                Iterations = derivationProfile.Iterations,
                DegreeOfParallelism = derivationProfile.Parallelism,
            };
            return argon2.GetBytes(derivationProfile.HashLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            derivationGate.Release();
        }
    }

    private static string Format(Argon2idProfile settings, byte[] salt, byte[] hash) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"$argon2id$v={Argon2Version}$m={settings.MemoryKiB},t={settings.Iterations},p={settings.Parallelism}${Convert.ToBase64String(salt).TrimEnd('=')}${Convert.ToBase64String(hash).TrimEnd('=')}");

    private static bool TryParse(string value, out ParsedVerifier parsed)
    {
        parsed = default!;
        string[] parts = value.Split('$');
        if (parts is not ["", "argon2id", "v=19", var parameters, var saltText, var hashText])
        {
            return false;
        }

        string[] parameterParts = parameters.Split(',');
        if (parameterParts.Length != 3 ||
            !TryParseParameter(parameterParts[0], "m=", out int memory) ||
            !TryParseParameter(parameterParts[1], "t=", out int iterations) ||
            !TryParseParameter(parameterParts[2], "p=", out int parallelism))
        {
            return false;
        }

        byte[]? salt = null;
        byte[]? hash = null;
        try
        {
            salt = Convert.FromBase64String(PadBase64(saltText));
            hash = Convert.FromBase64String(PadBase64(hashText));
            var parsedProfile = new Argon2idProfile(memory, iterations, parallelism, salt.Length, hash.Length);
            ValidateProfile(parsedProfile);
            parsed = new ParsedVerifier(parsedProfile, salt, hash);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException)
        {
            if (salt is not null) CryptographicOperations.ZeroMemory(salt);
            if (hash is not null) CryptographicOperations.ZeroMemory(hash);
            return false;
        }
    }

    private static bool TryParseParameter(string value, string prefix, out int result)
    {
        result = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(value.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    private static string PadBase64(string value) => value.PadRight((value.Length + 3) / 4 * 4, '=');

    private static void ValidateProfile(Argon2idProfile value)
    {
        if (value.MemoryKiB is < 8 * 1024 or > 1024 * 1024 ||
            value.Iterations is < 1 or > 20 ||
            value.Parallelism is < 1 or > 16 ||
            value.SaltLength is < 16 or > 64 ||
            value.HashLength is < 16 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Argon2id parameters are outside the supported safety bounds.");
        }
    }

    private sealed record ParsedVerifier(Argon2idProfile Profile, byte[] Salt, byte[] Hash);

    private sealed record Argon2idProfile(int MemoryKiB, int Iterations, int Parallelism, int SaltLength, int HashLength);
}
