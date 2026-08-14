namespace ScoutCampPlanner.Platform.Application.Authentication;

public enum PasswordPolicyFailure
{
    TooShort,
    TooLong,
    TooWeak,
}

public sealed record PasswordPolicyResult(
    bool IsAccepted,
    PasswordPolicyFailure? Failure,
    int? StrengthScore)
{
    public static PasswordPolicyResult Accepted(int? strengthScore = null) =>
        new(true, null, strengthScore);

    public static PasswordPolicyResult Rejected(PasswordPolicyFailure failure, int? strengthScore = null) =>
        new(false, failure, strengthScore);
}

public sealed record PasswordVerificationResult(bool IsValid, bool RequiresRehash);

public interface IPasswordPolicy
{
    PasswordPolicyResult Evaluate(string password, IReadOnlyCollection<string>? userInputs = null);
}

public interface IPasswordVerifier
{
    ValueTask<string> CreateAsync(string password, CancellationToken cancellationToken = default);

    ValueTask<PasswordVerificationResult> VerifyAsync(
        string password,
        string storedVerifier,
        CancellationToken cancellationToken = default);
}
