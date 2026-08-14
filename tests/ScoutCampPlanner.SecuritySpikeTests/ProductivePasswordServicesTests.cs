using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Infrastructure.Authentication;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class ProductivePasswordServicesTests
{
    [Fact]
    public void Policy_usesUnicodeScalarLengthAndStrengthRule()
    {
        var policy = new PasswordPolicy();

        Assert.Equal(PasswordPolicyFailure.TooShort, policy.Evaluate("abcde😀f").Failure);
        Assert.Equal(PasswordPolicyFailure.TooWeak, policy.Evaluate("password1").Failure);
        Assert.True(policy.Evaluate("correct horse battery staple").IsAccepted);
    }

    [Fact]
    public void Policy_rejectsLongButPredictablePasswords()
    {
        var policy = new PasswordPolicy();

        Assert.Equal(PasswordPolicyFailure.TooWeak, policy.Evaluate("passwordpassword").Failure);
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
}
