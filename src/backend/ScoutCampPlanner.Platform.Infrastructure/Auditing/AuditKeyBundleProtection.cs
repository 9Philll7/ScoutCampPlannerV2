using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;

namespace ScoutCampPlanner.Platform.Infrastructure.Auditing;

public interface IAuditKeyBundleProtection
{
    byte[] Protect(ReadOnlySpan<byte> plaintext);
    byte[] Unprotect(ReadOnlySpan<byte> protectedData);
}

public sealed class PlainAuditKeyBundleProtection : IAuditKeyBundleProtection
{
    public byte[] Protect(ReadOnlySpan<byte> plaintext) => plaintext.ToArray();
    public byte[] Unprotect(ReadOnlySpan<byte> protectedData) => protectedData.ToArray();
}

[SupportedOSPlatform("windows")]
public sealed class WindowsDpapiAuditKeyBundleProtection : IAuditKeyBundleProtection
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ScoutCampPlanner.AuditKeyBundle.v1");

    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        EnsureWindows();
        return ProtectedData.Protect(plaintext.ToArray(), Entropy, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(ReadOnlySpan<byte> protectedData)
    {
        EnsureWindows();
        return ProtectedData.Unprotect(protectedData.ToArray(), Entropy, DataProtectionScope.CurrentUser);
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows CurrentUser DPAPI is available only on Windows.");
    }
}
