using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Authentication;

public sealed class PasswordAuthenticationService(
    PlatformDbContext database,
    IPasswordVerifier passwordVerifier,
    TimeProvider timeProvider,
    IAuditedOperationExecutor auditedOperation,
    IAuditJournalAppender auditAppender,
    AuditRuntimeState auditRuntime) : IPasswordAuthenticationService
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
            await auditAppender.AppendAsync(Event("authentication.sign-in", "invalid-credentials", null, null), cancellationToken);
            return SignInResult.Failed();
        }

        PasswordVerificationResult verification = await passwordVerifier.VerifyAsync(
            password, credential.Verifier, cancellationToken);
        if (!verification.IsValid)
        {
            await auditAppender.AppendAsync(Event(
                "authentication.sign-in", "invalid-credentials", account.Id, credential.SecurityVersion), cancellationToken);
            return SignInResult.Failed();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        var session = new AuthenticationSession(
            Guid.NewGuid(), account.Id, credential.SecurityVersion, now, now.AddHours(12));
        await auditedOperation.ExecuteAsync(
            Event("authentication.sign-in", "success", account.Id, credential.SecurityVersion),
            async operationCancellationToken =>
            {
                if (verification.RequiresRehash)
                {
                    credential.ReplaceVerifier(
                        await passwordVerifier.CreateAsync(password, operationCancellationToken), now);
                }
                database.AuthenticationSessions.Add(session);
            },
            cancellationToken);
        return SignInResult.Success(new AuthenticatedUser(account.Id, account.Email), session.Id);
    }

    private AuditEventDraft Event(string action, string result, Guid? actorUserId, long? securityVersion) => new(
        Guid.NewGuid(), timeProvider.GetUtcNow(), action, result, actorUserId, null, null,
        "user-account", actorUserId, "server", auditRuntime.InstanceId, Guid.NewGuid(),
        securityVersion is null ? null : checked((int)securityVersion.Value), null,
        new Dictionary<string, string>());
}
