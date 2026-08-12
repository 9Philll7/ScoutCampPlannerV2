using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ScoutCampPlanner.AuditSecuritySpike;

const int eventCount = 10_000;
const string expectedGoldenHash = "A4B35365C0824C46D93804CAC8CB33C98F632C2FB76D053ACA0F562E107B4C0E";
var goldenEvent = new AuditEventData(
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
    new Dictionary<string, string>
    {
        ["newRole"] = "TenantAdmin",
        ["membershipId"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    });
string goldenHash = Convert.ToHexString(SHA256.HashData(
    AuditCanonicalEncoding.Encode(1, new byte[32], "key-1", goldenEvent)));
if (!string.Equals(goldenHash, expectedGoldenHash, StringComparison.Ordinal))
    throw new InvalidOperationException("Canonical audit encoding does not match the version 1 golden fixture.");

byte[] key = RandomNumberGenerator.GetBytes(32);
byte[] head = new byte[32];
var entries = new List<AuditChainEntry>(eventCount);
var instanceId = Guid.NewGuid();
var stopwatch = Stopwatch.StartNew();

for (var index = 1; index <= eventCount; index++)
{
    var auditEvent = new AuditEventData(
        Guid.NewGuid(),
        DateTimeOffset.UnixEpoch.AddSeconds(index),
        "spike.event",
        "success",
        null,
        null,
        null,
        null,
        null,
        "spike",
        instanceId,
        Guid.NewGuid(),
        null,
        null,
        new Dictionary<string, string> { ["fixture"] = "synthetic" });
    var entry = AuditHmacChain.Append(auditEvent, index, head, "spike-key-1", key);
    entries.Add(entry);
    head = entry.Hmac;
}

stopwatch.Stop();
var appendDuration = stopwatch.Elapsed;
stopwatch.Restart();
var verification = AuditHmacChain.Verify(entries, new byte[32], head, keyId => keyId == "spike-key-1" ? key : null);
stopwatch.Stop();
CryptographicOperations.ZeroMemory(key);

Console.WriteLine(JsonSerializer.Serialize(new
{
    eventCount,
    appendMilliseconds = appendDuration.TotalMilliseconds,
    verificationMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
    verification.IsValid,
    goldenCanonicalSha256 = goldenHash,
}, new JsonSerializerOptions { WriteIndented = true }));
