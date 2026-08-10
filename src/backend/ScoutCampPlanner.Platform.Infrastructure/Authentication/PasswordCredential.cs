namespace ScoutCampPlanner.Platform.Infrastructure.Authentication;

public sealed class PasswordCredential
{
    private PasswordCredential() { }

    public PasswordCredential(Guid userId, string verifier, DateTimeOffset changedAtUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(verifier);
        if (verifier.Length > 512) throw new ArgumentException("Verifier is too long.", nameof(verifier));
        if (changedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Password change time must use UTC.", nameof(changedAtUtc));

        UserId = userId;
        Verifier = verifier;
        SecurityVersion = 1;
        ChangedAtUtc = changedAtUtc;
    }

    public Guid UserId { get; private set; }

    public string Verifier { get; private set; } = string.Empty;

    public long SecurityVersion { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }
}
