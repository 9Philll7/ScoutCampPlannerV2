namespace ScoutCampPlanner.AuditSecuritySpike;

public enum AuditOperatingMode { Normal, Blocked }

public enum AuditOperation
{
    HealthStatus,
    ReadNonSensitiveData,
    SignIn,
    ChangeBusinessData,
    SensitiveAdministration,
    PrepareOfflineInstance,
    ExportPackage,
    ImportPackage,
    LocalDiagnosis,
    RestoreProtectedState,
    FullVerification
}

public sealed record AuditAccessContext(bool HasExistingSession, bool IsLocalOperator);

public sealed record AuditOperationDecision(bool IsAllowed, string Reason)
{
    public static AuditOperationDecision Allow(string reason) => new(true, reason);
    public static AuditOperationDecision Deny(string reason) => new(false, reason);
}

public static class AuditBlockedModePolicy
{
    public static AuditOperationDecision Evaluate(
        AuditOperatingMode mode,
        AuditOperation operation,
        AuditAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (mode == AuditOperatingMode.Normal)
            return AuditOperationDecision.Allow("normal-operation");

        return operation switch
        {
            AuditOperation.HealthStatus => AuditOperationDecision.Allow("degraded-health-status"),
            AuditOperation.ReadNonSensitiveData when context.HasExistingSession =>
                AuditOperationDecision.Allow("existing-session-read-only"),
            AuditOperation.LocalDiagnosis when context.IsLocalOperator =>
                AuditOperationDecision.Allow("protected-local-diagnosis"),
            AuditOperation.RestoreProtectedState when context.IsLocalOperator =>
                AuditOperationDecision.Allow("protected-local-recovery"),
            AuditOperation.FullVerification when context.IsLocalOperator =>
                AuditOperationDecision.Allow("required-recovery-verification"),
            _ => AuditOperationDecision.Deny("audit-integrity-not-established")
        };
    }
}

public sealed class AuditOperatingState
{
    public AuditOperatingMode Mode { get; private set; } = AuditOperatingMode.Normal;
    public string? FailureCode { get; private set; }

    public void RecordVerificationFailure(string failureCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        Mode = AuditOperatingMode.Blocked;
        FailureCode = failureCode;
    }

    public bool RecordFullVerification(bool isValid)
    {
        if (!isValid)
        {
            Mode = AuditOperatingMode.Blocked;
            FailureCode ??= "full-verification-failed";
            return false;
        }

        Mode = AuditOperatingMode.Normal;
        FailureCode = null;
        return true;
    }
}
