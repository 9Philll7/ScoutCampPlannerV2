using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class CampRecipeNoteStore(CateringDbContext database) : ICampRecipeNoteStore
{
    public Task<Guid?> FindCampIdAsync(
        Guid campRecipeEntryId, CancellationToken cancellationToken = default) =>
        database.Set<CampRecipeEntryRecord>().AsNoTracking()
            .Where(value => value.Id == campRecipeEntryId)
            .Select(value => (Guid?)value.CampId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CampRecipeNote>> ListAsync(
        Guid campRecipeEntryId, CancellationToken cancellationToken = default)
    {
        CampRecipeNote[] notes = await database.Set<CampRecipeNoteRecord>().AsNoTracking()
            .Where(value => value.CampRecipeEntryId == campRecipeEntryId && !value.DeletedAtUtc.HasValue)
            .Select(value => Map(value)).ToArrayAsync(cancellationToken);
        return notes.OrderBy(value => value.CreatedAtUtc).ThenBy(value => value.Id).ToArray();
    }

    public async Task<CampRecipeNoteMutationResult> CreateAsync(
        Guid noteId, Guid campRecipeEntryId, string text, Guid actorUserId,
        DateTimeOffset timestampUtc, CancellationToken cancellationToken = default)
    {
        if (!await database.Set<CampRecipeEntryRecord>().AsNoTracking()
                .AnyAsync(value => value.Id == campRecipeEntryId, cancellationToken))
            return new(CampRecipeNoteMutationStatus.NotFound);
        var record = new CampRecipeNoteRecord
        {
            Id = noteId, CampRecipeEntryId = campRecipeEntryId, Text = text,
            CreatedBy = actorUserId, CreatedAtUtc = timestampUtc,
            UpdatedBy = actorUserId, UpdatedAtUtc = timestampUtc,
        };
        database.Add(record);
        await database.SaveChangesAsync(cancellationToken);
        return new(CampRecipeNoteMutationStatus.Created, Map(record));
    }

    public async Task<CampRecipeNoteMutationResult> UpdateAsync(
        Guid campRecipeEntryId, Guid noteId, string text, Guid actorUserId, DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        CampRecipeNoteRecord? record = await FindActiveAsync(campRecipeEntryId, noteId, cancellationToken);
        if (record is null) return new(CampRecipeNoteMutationStatus.NotFound);
        record.Text = text;
        record.UpdatedBy = actorUserId;
        record.UpdatedAtUtc = timestampUtc;
        await database.SaveChangesAsync(cancellationToken);
        return new(CampRecipeNoteMutationStatus.Updated, Map(record));
    }

    public async Task<CampRecipeNoteMutationResult> DeleteAsync(
        Guid campRecipeEntryId, Guid noteId, Guid actorUserId, DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        CampRecipeNoteRecord? record = await FindActiveAsync(campRecipeEntryId, noteId, cancellationToken);
        if (record is null) return new(CampRecipeNoteMutationStatus.NotFound);
        record.DeletedBy = actorUserId;
        record.DeletedAtUtc = timestampUtc;
        await database.SaveChangesAsync(cancellationToken);
        return new(CampRecipeNoteMutationStatus.Deleted);
    }

    private Task<CampRecipeNoteRecord?> FindActiveAsync(
        Guid campRecipeEntryId, Guid noteId, CancellationToken cancellationToken) =>
        database.Set<CampRecipeNoteRecord>()
            .SingleOrDefaultAsync(value => value.Id == noteId &&
                value.CampRecipeEntryId == campRecipeEntryId && !value.DeletedAtUtc.HasValue, cancellationToken);

    private static CampRecipeNote Map(CampRecipeNoteRecord value) => new(
        value.Id, value.CampRecipeEntryId, value.Text, value.CreatedBy, value.CreatedAtUtc,
        value.UpdatedBy, value.UpdatedAtUtc);
}
