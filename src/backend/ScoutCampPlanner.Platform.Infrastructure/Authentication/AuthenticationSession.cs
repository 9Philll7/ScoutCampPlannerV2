namespace ScoutCampPlanner.Platform.Infrastructure.Authentication;

public sealed class AuthenticationSession
{
    private AuthenticationSession() { }

    public AuthenticationSession(
        Guid id,
        Guid userId,
        long securityVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset absoluteExpiresAtUtc)
    {
        if (id == Guid.Empty || userId == Guid.Empty)
            throw new ArgumentException("Session and user IDs are required.");
        if (securityVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(securityVersion));
        if (createdAtUtc.Offset != TimeSpan.Zero || absoluteExpiresAtUtc.Offset != TimeSpan.Zero ||
            absoluteExpiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Session timestamps must be a valid UTC interval.");

        Id = id;
        UserId = userId;
        SecurityVersion = securityVersion;
        CreatedAtUtc = createdAtUtc;
        LastSeenAtUtc = createdAtUtc;
        AbsoluteExpiresAtUtc = absoluteExpiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public long SecurityVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }
    public DateTimeOffset AbsoluteExpiresAtUtc { get; private set; }

    public bool IsValid(DateTimeOffset nowUtc, long currentSecurityVersion) =>
        nowUtc.Offset == TimeSpan.Zero &&
        SecurityVersion == currentSecurityVersion &&
        nowUtc >= LastSeenAtUtc &&
        nowUtc < AbsoluteExpiresAtUtc &&
        nowUtc - LastSeenAtUtc <= TimeSpan.FromMinutes(30);

    public bool Touch(DateTimeOffset nowUtc)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Session activity time must use UTC.", nameof(nowUtc));
        if (nowUtc < LastSeenAtUtc)
            throw new ArgumentException("Session activity cannot move backwards.", nameof(nowUtc));
        if (nowUtc - LastSeenAtUtc < TimeSpan.FromMinutes(1)) return false;
        LastSeenAtUtc = nowUtc;
        return true;
    }
}
