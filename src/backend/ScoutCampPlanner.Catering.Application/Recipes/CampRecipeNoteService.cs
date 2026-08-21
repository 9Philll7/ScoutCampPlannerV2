namespace ScoutCampPlanner.Catering.Application.Recipes;

public sealed record CampRecipeNote(
    Guid Id,
    Guid CampRecipeEntryId,
    string Text,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc,
    Guid UpdatedBy,
    DateTimeOffset UpdatedAtUtc);

public enum CampRecipeNoteMutationStatus
{
    Created,
    Updated,
    Deleted,
    NotFound,
    TextRequired,
    Forbidden,
}

public sealed record CampRecipeNoteMutationResult(
    CampRecipeNoteMutationStatus Status,
    CampRecipeNote? Note = null);

public interface ICampRecipeNoteStore
{
    Task<Guid?> FindCampIdAsync(Guid campRecipeEntryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampRecipeNote>> ListAsync(
        Guid campRecipeEntryId, CancellationToken cancellationToken = default);
    Task<CampRecipeNoteMutationResult> CreateAsync(
        Guid noteId, Guid campRecipeEntryId, string text, Guid actorUserId,
        DateTimeOffset timestampUtc, CancellationToken cancellationToken = default);
    Task<CampRecipeNoteMutationResult> UpdateAsync(
        Guid campRecipeEntryId, Guid noteId, string text, Guid actorUserId, DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<CampRecipeNoteMutationResult> DeleteAsync(
        Guid campRecipeEntryId, Guid noteId, Guid actorUserId, DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
}

public interface ICampRecipeNoteAuthorization
{
    Task<bool> CanReadAsync(Guid actorUserId, Guid campId, CancellationToken cancellationToken = default);
    Task<bool> CanManageAsync(Guid actorUserId, Guid campId, CancellationToken cancellationToken = default);
}

public sealed class CampRecipeNoteService(
    ICampRecipeNoteStore store,
    ICampRecipeNoteAuthorization authorization)
{
    public async Task<IReadOnlyList<CampRecipeNote>> ListAsync(
        Guid campRecipeEntryId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(campRecipeEntryId, nameof(campRecipeEntryId));
        Required(actorUserId, nameof(actorUserId));
        Guid? campId = await store.FindCampIdAsync(campRecipeEntryId, cancellationToken);
        if (!campId.HasValue || !await authorization.CanReadAsync(actorUserId, campId.Value, cancellationToken))
            return [];
        return await store.ListAsync(campRecipeEntryId, cancellationToken);
    }

    public async Task<CampRecipeNoteMutationResult> CreateAsync(
        Guid campRecipeEntryId, string text, Guid actorUserId, DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        Guid? campId = await FindCampAsync(campRecipeEntryId, actorUserId, cancellationToken);
        if (!campId.HasValue) return new(CampRecipeNoteMutationStatus.NotFound);
        if (!await authorization.CanManageAsync(actorUserId, campId.Value, cancellationToken))
            return new(CampRecipeNoteMutationStatus.Forbidden);
        if (string.IsNullOrWhiteSpace(text)) return new(CampRecipeNoteMutationStatus.TextRequired);
        return await store.CreateAsync(
            Guid.NewGuid(), campRecipeEntryId, text.Trim(), actorUserId, timestampUtc, cancellationToken);
    }

    public async Task<CampRecipeNoteMutationResult> UpdateAsync(
        Guid campRecipeEntryId, Guid noteId, string text, Guid actorUserId,
        DateTimeOffset timestampUtc, CancellationToken cancellationToken = default)
    {
        Required(noteId, nameof(noteId));
        Guid? campId = await FindCampAsync(campRecipeEntryId, actorUserId, cancellationToken);
        if (!campId.HasValue) return new(CampRecipeNoteMutationStatus.NotFound);
        if (!await authorization.CanManageAsync(actorUserId, campId.Value, cancellationToken))
            return new(CampRecipeNoteMutationStatus.Forbidden);
        if (string.IsNullOrWhiteSpace(text)) return new(CampRecipeNoteMutationStatus.TextRequired);
        return await store.UpdateAsync(
            campRecipeEntryId, noteId, text.Trim(), actorUserId, timestampUtc, cancellationToken);
    }

    public async Task<CampRecipeNoteMutationResult> DeleteAsync(
        Guid campRecipeEntryId, Guid noteId, Guid actorUserId, DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        Required(noteId, nameof(noteId));
        Guid? campId = await FindCampAsync(campRecipeEntryId, actorUserId, cancellationToken);
        if (!campId.HasValue) return new(CampRecipeNoteMutationStatus.NotFound);
        if (!await authorization.CanManageAsync(actorUserId, campId.Value, cancellationToken))
            return new(CampRecipeNoteMutationStatus.Forbidden);
        return await store.DeleteAsync(
            campRecipeEntryId, noteId, actorUserId, timestampUtc, cancellationToken);
    }

    private async Task<Guid?> FindCampAsync(
        Guid entryId, Guid actorUserId, CancellationToken cancellationToken)
    {
        Required(entryId, nameof(entryId));
        Required(actorUserId, nameof(actorUserId));
        return await store.FindCampIdAsync(entryId, cancellationToken);
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
