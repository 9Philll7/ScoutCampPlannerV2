using System.Diagnostics;
using Xunit;
using Zxcvbn;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class PasswordStrengthCompatibilityTests
{
    [Theory]
    [InlineData("password", 0)]
    [InlineData("password1", 0)]
    [InlineData("qwerty123", 0)]
    [InlineData("aaaaaaaaaaaa", 0)]
    [InlineData("correct horse battery staple", 4)]
    [InlineData("z9!Kp2@Lm7#Qx", 4)]
    public void EvaluatePassword_ReturnsStableExpectedScores(string password, int expectedScore)
    {
        var first = Core.EvaluatePassword(password);
        var second = Core.EvaluatePassword(password);

        Assert.Equal(expectedScore, first.Score);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Guesses, second.Guesses);
    }

    [Fact]
    public void EvaluatePassword_AcceptsUnicodeAndSpaces()
    {
        const string password = "Pfadfinder ⚜ Wald 2026!";

        var exception = Record.Exception(() => Core.EvaluatePassword(password));

        Assert.Null(exception);
    }

    [Fact]
    public void UserInputs_ReduceStrengthOfContextSpecificPassword()
    {
        const string password = "ScoutCamp2026!";

        var withoutContext = Core.EvaluatePassword(password);
        var withContext = Core.EvaluatePassword(password, ["ScoutCamp"]);

        Assert.True(withContext.Score <= withoutContext.Score);
        Assert.True(withContext.Guesses <= withoutContext.Guesses);
    }

    [Fact]
    public void ResultContainsPlaintextAndMustNotEscapeInfrastructureAdapter()
    {
        const string password = "non-production-test-value";

        var result = Core.EvaluatePassword(password);

        Assert.Equal(password, result.Password);
    }

    [Fact]
    public void MaximumPolicyLength_CompletesWithinGenerousSpikeLimit()
    {
        string password = string.Concat(Enumerable.Repeat("A7!pfad", 19))[..128];
        var stopwatch = Stopwatch.StartNew();

        var result = Core.EvaluatePassword(password);

        stopwatch.Stop();
        Assert.InRange(result.Score, 0, 4);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }
}
