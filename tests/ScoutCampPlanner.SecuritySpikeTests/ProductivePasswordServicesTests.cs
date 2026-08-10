using ScoutCampPlanner.PasswordDenylist;
using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Infrastructure.Authentication;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class ProductivePasswordServicesTests
{
    [Fact]
    public void Policy_usesUnicodeScalarLengthAndStrengthRule()
    {
        var policy = new PasswordPolicy(new StubDenylist());

        Assert.Equal(PasswordPolicyFailure.TooShort, policy.Evaluate("abcde😀f").Failure);
        Assert.Equal(PasswordPolicyFailure.TooWeak, policy.Evaluate("password1").Failure);
        Assert.True(policy.Evaluate("correct horse battery staple").IsAccepted);
    }

    [Fact]
    public void Policy_rejectsDenylistedPasswordsRegardlessOfLength()
    {
        var policy = new PasswordPolicy(new StubDenylist("a very long compromised password"));

        Assert.Equal(
            PasswordPolicyFailure.Denylisted,
            policy.Evaluate("a very long compromised password").Failure);
    }

    [Fact]
    public void BinaryDenylist_readsGeneratorFormat()
    {
        byte[] file = DenylistFile.Create(
            [new DenylistSourceEntry("5BAA61E4C9B93F3F0682250B6CF8331B7EE68FD8", 10)],
            "test",
            new DateOnly(2026, 8, 10),
            100);

        var denylist = BinaryPasswordDenylist.Load(file);

        Assert.True(denylist.Contains("password"));
        Assert.False(denylist.Contains("not-listed"));
    }

    [Fact]
    public async Task Verifier_usesIndependentSaltsAndVerifiesPassword()
    {
        using var verifier = new Argon2idPasswordVerifier(Argon2idOperatingMode.SingleDevice);
        var cancellationToken = TestContext.Current.CancellationToken;

        string first = await verifier.CreateAsync("valid test passphrase", cancellationToken);
        string second = await verifier.CreateAsync("valid test passphrase", cancellationToken);

        Assert.NotEqual(first, second);
        Assert.True((await verifier.VerifyAsync("valid test passphrase", first, cancellationToken)).IsValid);
        Assert.False((await verifier.VerifyAsync("wrong test passphrase", first, cancellationToken)).IsValid);
    }

    [Fact]
    public async Task Verifier_requestsRehashOnlyAfterSuccessfulVerificationWithDifferentProfile()
    {
        using var oldVerifier = new Argon2idPasswordVerifier(Argon2idOperatingMode.SingleDevice);
        using var currentVerifier = new Argon2idPasswordVerifier(Argon2idOperatingMode.Server);
        var cancellationToken = TestContext.Current.CancellationToken;
        string stored = await oldVerifier.CreateAsync("valid test passphrase", cancellationToken);

        var valid = await currentVerifier.VerifyAsync("valid test passphrase", stored, cancellationToken);
        var invalid = await currentVerifier.VerifyAsync("wrong", stored, cancellationToken);

        Assert.True(valid.IsValid);
        Assert.True(valid.RequiresRehash);
        Assert.False(invalid.IsValid);
        Assert.False(invalid.RequiresRehash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-verifier")]
    [InlineData("$argon2id$v=19$m=1,t=1,p=1$bad$bad")]
    public async Task Verifier_rejectsMalformedOrUnsafeStoredValues(string stored)
    {
        using var verifier = new Argon2idPasswordVerifier(Argon2idOperatingMode.SingleDevice);

        var result = await verifier.VerifyAsync("anything", stored, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.False(result.RequiresRehash);
    }

    private sealed class StubDenylist(params string[] passwords) : IPasswordDenylist
    {
        public bool Contains(string password) => passwords.Contains(password, StringComparer.Ordinal);
    }
}
