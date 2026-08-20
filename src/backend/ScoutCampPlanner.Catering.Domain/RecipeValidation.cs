namespace ScoutCampPlanner.Catering.Domain;

public enum RecipeValidationSeverity
{
    Error,
    Warning,
}

public sealed record RecipeValidationIssue
{
    public RecipeValidationIssue(
        string code,
        RecipeValidationSeverity severity,
        string message,
        IReadOnlyDictionary<string, string>? context = null)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Validation code is required.", nameof(code))
            : code.Trim();
        if (!Enum.IsDefined(severity)) throw new ArgumentOutOfRangeException(nameof(severity));
        Severity = severity;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Validation message is required.", nameof(message))
            : message.Trim();
        Context = context is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(context, StringComparer.Ordinal);
    }

    public string Code { get; }
    public RecipeValidationSeverity Severity { get; }
    public string Message { get; }
    public IReadOnlyDictionary<string, string> Context { get; }
}

public sealed class RecipeValidationResult
{
    private readonly IReadOnlyList<RecipeValidationIssue> issues;

    public RecipeValidationResult(IEnumerable<RecipeValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        this.issues = issues.ToArray();
    }

    public IReadOnlyList<RecipeValidationIssue> Issues => issues;
    public IReadOnlyList<RecipeValidationIssue> Errors =>
        issues.Where(value => value.Severity == RecipeValidationSeverity.Error).ToArray();
    public IReadOnlyList<RecipeValidationIssue> Warnings =>
        issues.Where(value => value.Severity == RecipeValidationSeverity.Warning).ToArray();
    public bool CanPublish(bool warningsAcknowledged) =>
        Errors.Count == 0 && (Warnings.Count == 0 || warningsAcknowledged);
}
