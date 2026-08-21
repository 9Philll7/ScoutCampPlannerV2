using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public sealed class RecipeDraftService(IRecipeDraftStore store)
{
    public Task<RecipeDraft?> FindAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
        store.FindAsync(Required(recipeId, nameof(recipeId)), cancellationToken);

    public Task<RecipeDraft> CreateAsync(
        RecipeDraft draft,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return store.CreateAsync(draft, Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);
    }

    public Task<RecipeDraftSaveResult> SaveAsync(
        RecipeDraft draft,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (expectedVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        return store.SaveAsync(
            draft, expectedVersion, Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("User or recipe ID is required.", parameterName) : value;
}
