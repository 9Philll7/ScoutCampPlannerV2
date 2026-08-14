using ScoutCampPlanner.Platform.Application.Authentication;
using Zxcvbn;

namespace ScoutCampPlanner.Platform.Infrastructure.Authentication;

public sealed class PasswordPolicy : IPasswordPolicy
{
    public PasswordPolicyResult Evaluate(string password, IReadOnlyCollection<string>? userInputs = null)
    {
        ArgumentNullException.ThrowIfNull(password);
        int length = password.EnumerateRunes().Count();
        if (length < 8)
        {
            return PasswordPolicyResult.Rejected(PasswordPolicyFailure.TooShort);
        }

        if (length > 128)
        {
            return PasswordPolicyResult.Rejected(PasswordPolicyFailure.TooLong);
        }

        var strength = Core.EvaluatePassword(password, userInputs?.ToArray() ?? []);
        return strength.Score >= 3
            ? PasswordPolicyResult.Accepted(strength.Score)
            : PasswordPolicyResult.Rejected(PasswordPolicyFailure.TooWeak, strength.Score);
    }
}
