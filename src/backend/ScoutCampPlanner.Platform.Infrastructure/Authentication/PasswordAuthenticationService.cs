using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Domain;

namespace ScoutCampPlanner.Platform.Infrastructure.Authentication;

public sealed class PasswordAuthenticationService(
    PlatformDbContext database,
    IPasswordVerifier passwordVerifier,
    TimeProvider timeProvider) : IPasswordAuthenticationService
{
    public async Task<SignInResult> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string normalizedEmail = (request.Email ?? string.Empty).Trim().ToUpperInvariant();
        string password = request.Password ?? string.Empty;
        var account = await database.UserAccounts.SingleOrDefaultAsync(
            value => value.NormalizedEmail == normalizedEmail, cancellationToken);
        PasswordCredential? credential = account is null
            ? null
            : await database.PasswordCredentials.SingleOrDefaultAsync(
                value => value.UserId == account.Id, cancellationToken);

        if (account is null || account.State != UserAccountState.Active || credential is null)
        {
            _ = await passwordVerifier.CreateAsync(password, cancellationToken);
            return SignInResult.Failed();
        }

        PasswordVerificationResult verification = await passwordVerifier.VerifyAsync(
            password, credential.Verifier, cancellationToken);
        if (!verification.IsValid) return SignInResult.Failed();

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (verification.RequiresRehash)
        {
            credential.ReplaceVerifier(await passwordVerifier.CreateAsync(password, cancellationToken), now);
        }

        var session = new AuthenticationSession(
            Guid.NewGuid(), account.Id, credential.SecurityVersion, now, now.AddHours(12));
        database.AuthenticationSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        return SignInResult.Success(new AuthenticatedUser(account.Id, account.Email), session.Id);
    }
}
