using System.Security.Cryptography;
using System.Text;

if (!OperatingSystem.IsWindows())
    throw new PlatformNotSupportedException("This validation requires Windows DPAPI.");

byte[] plaintext = RandomNumberGenerator.GetBytes(32);
byte[] entropy = Encoding.UTF8.GetBytes("ScoutCampPlanner.AuditKeyBundle.v1");
byte[] protectedData = ProtectedData.Protect(plaintext, entropy, DataProtectionScope.CurrentUser);
byte[] restored = ProtectedData.Unprotect(protectedData, entropy, DataProtectionScope.CurrentUser);

try
{
    if (!CryptographicOperations.FixedTimeEquals(plaintext, restored))
        throw new InvalidOperationException("DPAPI roundtrip failed.");

    protectedData[^1] ^= 1;
    try
    {
        _ = ProtectedData.Unprotect(protectedData, entropy, DataProtectionScope.CurrentUser);
        throw new InvalidOperationException("Modified DPAPI payload was accepted.");
    }
    catch (CryptographicException)
    {
        Console.WriteLine("Windows CurrentUser DPAPI roundtrip and modification rejection succeeded.");
    }
}
finally
{
    CryptographicOperations.ZeroMemory(plaintext);
    CryptographicOperations.ZeroMemory(restored);
    CryptographicOperations.ZeroMemory(protectedData);
    CryptographicOperations.ZeroMemory(entropy);
}
