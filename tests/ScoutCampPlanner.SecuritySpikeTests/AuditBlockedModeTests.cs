using ScoutCampPlanner.AuditSecuritySpike;
using Xunit;

namespace ScoutCampPlanner.SecuritySpikeTests;

public sealed class AuditBlockedModeTests
{
    private static readonly AuditAccessContext Anonymous = new(false, false);
    private static readonly AuditAccessContext ExistingSession = new(true, false);
    private static readonly AuditAccessContext LocalOperator = new(false, true);

    [Theory]
    [InlineData(AuditOperation.SignIn)]
    [InlineData(AuditOperation.ChangeBusinessData)]
    [InlineData(AuditOperation.SensitiveAdministration)]
    [InlineData(AuditOperation.PrepareOfflineInstance)]
    [InlineData(AuditOperation.ExportPackage)]
    [InlineData(AuditOperation.ImportPackage)]
    public void BlockedModeRejectsOperationsThatRequireReliableAudit(AuditOperation operation)
    {
        var decision = AuditBlockedModePolicy.Evaluate(AuditOperatingMode.Blocked, operation, ExistingSession);
        Assert.False(decision.IsAllowed);
        Assert.Equal("audit-integrity-not-established", decision.Reason);
    }

    [Fact]
    public void BlockedModeAllowsOnlyExistingSessionsToReadNonSensitiveData()
    {
        var existing = AuditBlockedModePolicy.Evaluate(AuditOperatingMode.Blocked, AuditOperation.ReadNonSensitiveData, ExistingSession);
        var anonymous = AuditBlockedModePolicy.Evaluate(AuditOperatingMode.Blocked, AuditOperation.ReadNonSensitiveData, Anonymous);
        Assert.True(existing.IsAllowed);
        Assert.False(anonymous.IsAllowed);
    }

    [Fact]
    public void BlockedModeExposesOnlyGenericHealthStatusToAnonymousCallers()
    {
        var health = AuditBlockedModePolicy.Evaluate(AuditOperatingMode.Blocked, AuditOperation.HealthStatus, Anonymous);
        var diagnosis = AuditBlockedModePolicy.Evaluate(AuditOperatingMode.Blocked, AuditOperation.LocalDiagnosis, Anonymous);
        Assert.True(health.IsAllowed);
        Assert.Equal("degraded-health-status", health.Reason);
        Assert.False(diagnosis.IsAllowed);
    }

    [Theory]
    [InlineData(AuditOperation.LocalDiagnosis)]
    [InlineData(AuditOperation.RestoreProtectedState)]
    [InlineData(AuditOperation.FullVerification)]
    public void BlockedModeRestrictsRecoveryOperationsToLocalOperator(AuditOperation operation)
    {
        Assert.True(AuditBlockedModePolicy.Evaluate(AuditOperatingMode.Blocked, operation, LocalOperator).IsAllowed);
        Assert.False(AuditBlockedModePolicy.Evaluate(AuditOperatingMode.Blocked, operation, ExistingSession).IsAllowed);
    }

    [Fact]
    public void NormalModeDoesNotAddRestrictions()
    {
        foreach (var operation in Enum.GetValues<AuditOperation>())
            Assert.True(AuditBlockedModePolicy.Evaluate(AuditOperatingMode.Normal, operation, Anonymous).IsAllowed);
    }

    [Fact]
    public void FailedVerificationKeepsApplicationBlocked()
    {
        var state = new AuditOperatingState();
        state.RecordVerificationFailure("protected-head-mismatch");
        var recovered = state.RecordFullVerification(false);
        Assert.False(recovered);
        Assert.Equal(AuditOperatingMode.Blocked, state.Mode);
        Assert.Equal("protected-head-mismatch", state.FailureCode);
    }

    [Fact]
    public void OnlySuccessfulFullVerificationReturnsToNormalMode()
    {
        var state = new AuditOperatingState();
        state.RecordVerificationFailure("unknown-key");
        var recovered = state.RecordFullVerification(true);
        Assert.True(recovered);
        Assert.Equal(AuditOperatingMode.Normal, state.Mode);
        Assert.Null(state.FailureCode);
    }
}
