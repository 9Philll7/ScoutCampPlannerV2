namespace ScoutCampPlanner.Platform.Application.Authentication;

public sealed record SignInRequest(string Email, string Password);

public sealed record AuthenticatedUser(Guid UserId, string Email);

public sealed record SignInResult(bool IsSuccessful, AuthenticatedUser? User, Guid? SessionId)
{
    public static SignInResult Success(AuthenticatedUser user, Guid sessionId) => new(true, user, sessionId);
    public static SignInResult Failed() => new(false, null, null);
}

public interface IPasswordAuthenticationService
{
    Task<SignInResult> SignInAsync(SignInRequest request, CancellationToken cancellationToken = default);
}

public interface ISessionTerminationService
{
    Task SignOutAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
}
