using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Platform.Infrastructure.Authentication;

public sealed class InitialSetupService(
    PlatformDbContext database,
    IPasswordPolicy passwordPolicy,
    IPasswordVerifier passwordVerifier,
    TimeProvider timeProvider,
    IAuditedOperationExecutor auditedOperation,
    AuditRuntimeState auditRuntime) : IInitialSetupService
{
    public async Task<InitialSetupStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        new(!await database.UserAccounts.AnyAsync(cancellationToken));

    public async Task<InitialSetupResult> CompleteAsync(
        InitialSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string tenantName = request.TenantName?.Trim() ?? string.Empty;
        string email = request.Email?.Trim() ?? string.Empty;
        if (tenantName.Length is < 1 or > 200)
            return InitialSetupResult.Rejected(InitialSetupFailure.InvalidTenantName);
        if (email.Length is < 3 or > 320 || !email.Contains('@', StringComparison.Ordinal))
            return InitialSetupResult.Rejected(InitialSetupFailure.InvalidEmail);

        if (request.Password is null)
            return InitialSetupResult.Rejected(InitialSetupFailure.PasswordTooShort);
        PasswordPolicyResult policy = passwordPolicy.Evaluate(request.Password, [tenantName, email]);
        if (!policy.IsAccepted)
            return InitialSetupResult.Rejected(policy.Failure switch
            {
                PasswordPolicyFailure.TooShort => InitialSetupFailure.PasswordTooShort,
                PasswordPolicyFailure.TooLong => InitialSetupFailure.PasswordTooLong,
                _ => InitialSetupFailure.PasswordTooWeak,
            });

        string verifier = await passwordVerifier.CreateAsync(request.Password, cancellationToken);
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid membershipId = Guid.NewGuid();
        try
        {
            var auditEvent = new AuditEventDraft(
                Guid.NewGuid(), timeProvider.GetUtcNow(), "identity.initial-setup", "success",
                userId, tenantId, null, "user-account", userId, "server", auditRuntime.InstanceId,
                Guid.NewGuid(), 1, AuthorizationCatalogue.DefinitionVersion,
                new Dictionary<string, string> { ["membershipId"] = membershipId.ToString("D"), ["role"] = Roles.TenantOwner });
            await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
            {
                if (await database.UserAccounts.AnyAsync(operationCancellationToken))
                    throw new SetupAlreadyCompletedException();
                var user = new UserAccount(userId, email);
                user.ActivateAfterInitialSetup();
                database.AddRange(
                    new Tenant(tenantId, tenantName), user,
                    new TenantMembership(membershipId, userId, tenantId),
                    new TenantRoleAssignment(membershipId, Roles.TenantOwner),
                    new PasswordCredential(userId, verifier, timeProvider.GetUtcNow()));
            }, cancellationToken);
            return InitialSetupResult.Completed(userId, tenantId);
        }
        catch (SetupAlreadyCompletedException)
        {
            return InitialSetupResult.Rejected(InitialSetupFailure.AlreadyCompleted);
        }
    }

    private sealed class SetupAlreadyCompletedException : Exception { }
}
