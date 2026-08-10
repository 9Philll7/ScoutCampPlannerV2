using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;

var profiles = new[]
{
    new Argon2Profile("interactive-minimum", 19 * 1024, 2, 1, 16, 32),
    new Argon2Profile("server-candidate", 64 * 1024, 3, 1, 16, 32),
};

var results = new List<ProfileResult>();
foreach (var profile in profiles)
{
    results.Add(RunProfile(profile));
}

var concurrency = await RunConcurrencyProbe(profiles[0], attempts: 8, maximumConcurrency: 2);
var report = new SpikeReport(
    Environment.OSVersion.ToString(),
    Environment.Version.ToString(),
    Environment.ProcessorCount,
    results,
    concurrency);

Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

static ProfileResult RunProfile(Argon2Profile profile)
{
    byte[] password = Encoding.UTF8.GetBytes("ScoutCampPlanner spike password");
    byte[] salt = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

    try
    {
        _ = Derive(password, salt, profile);
        var timings = new List<double>();

        for (var index = 0; index < 5; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            byte[] derived = Derive(password, salt, profile);
            stopwatch.Stop();
            CryptographicOperations.ZeroMemory(derived);
            timings.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        timings.Sort();
        return new ProfileResult(profile, timings[0], timings[2], timings[^1]);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(password);
        CryptographicOperations.ZeroMemory(salt);
    }
}

static async Task<ConcurrencyResult> RunConcurrencyProbe(
    Argon2Profile profile,
    int attempts,
    int maximumConcurrency)
{
    using var gate = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
    var active = 0;
    var observedPeak = 0;
    var stopwatch = Stopwatch.StartNew();

    var tasks = Enumerable.Range(0, attempts).Select(attempt =>
        Task.Run(async () =>
        {
            await gate.WaitAsync();
            var current = Interlocked.Increment(ref active);
            UpdateMaximum(ref observedPeak, current);

            byte[] password = Encoding.UTF8.GetBytes($"concurrency-probe-{attempt}");
            byte[] salt = SHA256.HashData(Encoding.UTF8.GetBytes($"salt-{attempt}"))[..profile.SaltLength];
            try
            {
                byte[] derived = Derive(password, salt, profile);
                CryptographicOperations.ZeroMemory(derived);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(password);
                CryptographicOperations.ZeroMemory(salt);
                Interlocked.Decrement(ref active);
                gate.Release();
            }
        }));

    await Task.WhenAll(tasks);
    stopwatch.Stop();
    return new ConcurrencyResult(attempts, maximumConcurrency, observedPeak, stopwatch.Elapsed.TotalMilliseconds);
}

static void UpdateMaximum(ref int target, int candidate)
{
    var current = Volatile.Read(ref target);
    while (candidate > current)
    {
        var observed = Interlocked.CompareExchange(ref target, candidate, current);
        if (observed == current)
        {
            return;
        }

        current = observed;
    }
}

static byte[] Derive(byte[] password, byte[] salt, Argon2Profile profile)
{
    using var argon2 = new Argon2id(password)
    {
        Salt = salt,
        MemorySize = profile.MemoryKiB,
        Iterations = profile.Iterations,
        DegreeOfParallelism = profile.Parallelism,
    };

    return argon2.GetBytes(profile.DerivedKeyLength);
}

internal sealed record Argon2Profile(
    string Name,
    int MemoryKiB,
    int Iterations,
    int Parallelism,
    int SaltLength,
    int DerivedKeyLength);

internal sealed record ProfileResult(
    Argon2Profile Profile,
    double MinimumMilliseconds,
    double MedianMilliseconds,
    double MaximumMilliseconds);

internal sealed record ConcurrencyResult(
    int Attempts,
    int ConfiguredMaximumConcurrency,
    int ObservedPeakConcurrency,
    double TotalMilliseconds);

internal sealed record SpikeReport(
    string OperatingSystem,
    string Runtime,
    int ProcessorCount,
    IReadOnlyList<ProfileResult> Profiles,
    ConcurrencyResult Concurrency);
