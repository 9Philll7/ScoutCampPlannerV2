namespace ScoutCampPlanner.Platform.Application.Authentication;

public sealed record InitialSetupStatus(bool IsRequired);

public sealed record InitialSetupRequest(string TenantName, string Email, string Password);

public enum InitialSetupFailure
{
    AlreadyCompleted,
    InvalidTenantName,
    InvalidEmail,
    PasswordTooShort,
    PasswordTooLong,
    PasswordTooWeak,
}

public sealed record InitialSetupResult(
    bool IsSuccessful,
    InitialSetupFailure? Failure,
    Guid? UserId,
    Guid? TenantId)
{
    public static InitialSetupResult Completed(Guid userId, Guid tenantId) =>
        new(true, null, userId, tenantId);

    public static InitialSetupResult Rejected(InitialSetupFailure failure) =>
        new(false, failure, null, null);
}

public interface IInitialSetupService
{
    Task<InitialSetupStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<InitialSetupResult> CompleteAsync(
        InitialSetupRequest request,
        CancellationToken cancellationToken = default);
}
