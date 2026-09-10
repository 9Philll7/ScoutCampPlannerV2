using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class RecipeEditorStore(CateringDbContext database, RecipeDraftStore drafts) : IRecipeEditorStore
{
    public Task<RecipeDraft?> FindAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
        drafts.FindAsync(recipeId, cancellationToken);

    public async Task<RecipeDraft> CreateCampAsync(
        RecipeDraft draft, Guid campId, Guid actorUserId, DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        RecipeDraft created = await drafts.CreateAsync(draft, actorUserId, timestampUtc, cancellationToken);
        database.Add(new CampRecipeEntryRecord
        {
            Id = Guid.NewGuid(),
            CampId = campId,
            CampRecipeId = draft.Id,
            CreatedBy = actorUserId,
            CreatedAtUtc = timestampUtc,
            UpdatedBy = actorUserId,
            UpdatedAtUtc = timestampUtc,
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public Task<RecipeDraftSaveResult> SaveAsync(
        RecipeDraft draft, long expectedVersion, Guid actorUserId, DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default) =>
        drafts.SaveAsync(draft, expectedVersion, actorUserId, timestampUtc, cancellationToken);
}
