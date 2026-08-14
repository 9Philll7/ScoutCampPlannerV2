using System.Data;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;

namespace ScoutCampPlanner.Platform.Infrastructure.Authentication;

public sealed class InitialSetupService(
    PlatformDbContext database,
    IPasswordPolicy passwordPolicy,
    IPasswordVerifier passwordVerifier,
    TimeProvider timeProvider) : IInitialSetupService
{
    private static readonly SemaphoreSlim SqliteGate = new(1, 1);

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
        bool sqlite = database.Database.ProviderName?.Contains("Sqlite", StringComparison.Ordinal) == true;
        if (sqlite) await SqliteGate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            if (!sqlite && database.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                await database.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(72118455371802);", cancellationToken);
            }
            if (await database.UserAccounts.AnyAsync(cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return InitialSetupResult.Rejected(InitialSetupFailure.AlreadyCompleted);
            }

            Guid userId = Guid.NewGuid();
            Guid tenantId = Guid.NewGuid();
            Guid membershipId = Guid.NewGuid();
            var user = new UserAccount(userId, email);
            user.ActivateAfterInitialSetup();
            database.AddRange(
                new Tenant(tenantId, tenantName),
                user,
                new TenantMembership(membershipId, userId, tenantId),
                new TenantRoleAssignment(membershipId, Roles.TenantOwner),
                new PasswordCredential(userId, verifier, timeProvider.GetUtcNow()));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return InitialSetupResult.Completed(userId, tenantId);
        }
        catch
        {
            database.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            if (sqlite) SqliteGate.Release();
        }
    }
}
