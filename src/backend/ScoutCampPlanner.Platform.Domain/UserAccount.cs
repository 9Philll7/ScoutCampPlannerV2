namespace ScoutCampPlanner.Platform.Domain;

public enum UserAccountState
{
    PendingConfirmation = 0,
    Active = 1,
    Disabled = 2,
}

public sealed class UserAccount
{
    private UserAccount() { }

    public UserAccount(Guid id, string email)
    {
        if (id == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        string displayEmail = email.Trim();
        if (displayEmail.Length > 320)
            throw new ArgumentException("Email must not exceed 320 characters.", nameof(email));

        Id = id;
        Email = displayEmail;
        NormalizedEmail = displayEmail.ToUpperInvariant();
        if (NormalizedEmail.Length > 320)
            throw new ArgumentException("Normalized email must not exceed 320 characters.", nameof(email));
        State = UserAccountState.PendingConfirmation;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public UserAccountState State { get; private set; }
}
