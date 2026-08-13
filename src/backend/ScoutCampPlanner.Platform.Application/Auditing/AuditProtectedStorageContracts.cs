namespace ScoutCampPlanner.Platform.Application.Auditing;

public sealed record AuditProtectedMaterial(byte[] KeyBundle, byte[] Checkpoint);

public enum AuditProtectedMaterialStatus
{
    Available,
    Missing,
    Invalid
}

public sealed record AuditProtectedMaterialLoadResult(
    AuditProtectedMaterialStatus Status,
    AuditProtectedMaterial? Material = null);

public interface IAuditProtectedMaterialStore
{
    Task<AuditProtectedMaterialLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AuditProtectedMaterial material, CancellationToken cancellationToken = default);
}

public enum AuditInstanceStartMode
{
    ExistingInstance,
    ExplicitNewInstance
}

public sealed record AuditStartupResult(bool IsReady, AuditProtectedMaterial? Material, string Status);

public sealed class AuditProtectedMaterialInitializer(IAuditProtectedMaterialStore store)
{
    public async Task<AuditStartupResult> InitializeAsync(
        AuditInstanceStartMode startMode,
        Func<AuditProtectedMaterial> createInitialMaterial,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createInitialMaterial);
        AuditProtectedMaterialLoadResult loaded = await store.LoadAsync(cancellationToken);
        if (loaded.Status == AuditProtectedMaterialStatus.Available && loaded.Material is not null)
            return new AuditStartupResult(true, loaded.Material, "protected-material-loaded");

        if (loaded.Status == AuditProtectedMaterialStatus.Invalid)
            return new AuditStartupResult(false, null, "protected-material-invalid");

        if (startMode != AuditInstanceStartMode.ExplicitNewInstance)
            return new AuditStartupResult(false, null, "protected-material-missing");

        AuditProtectedMaterial created = createInitialMaterial();
        await store.SaveAsync(created, cancellationToken);
        return new AuditStartupResult(true, created, "protected-material-created");
    }
}
