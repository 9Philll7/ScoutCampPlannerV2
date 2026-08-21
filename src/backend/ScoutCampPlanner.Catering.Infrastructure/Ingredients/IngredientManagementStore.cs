using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

public sealed class IngredientManagementStore(CateringDbContext database) : IIngredientManagementStore
{
    public async Task<IngredientMutationResult> CreateAsync(
        Guid ingredientId,
        IngredientScopeType scope,
        Guid? scopeId,
        CreateIngredientRequest request,
        CancellationToken cancellationToken = default)
    {
        var ingredient = new BaseIngredient(
            ingredientId, scope, scopeId, request.Name, request.OriginInformation);
        if (await database.BaseIngredients.AsNoTracking().AnyAsync(value =>
                value.ScopeType == scope && value.ScopeId == scopeId &&
                value.NormalizedName == ingredient.NormalizedName, cancellationToken))
            return new(IngredientMutationStatus.DuplicateName);

        IngredientVariant[] variants = request.Variants
            .Select(value => new IngredientVariant(Guid.NewGuid(), ingredientId, value)).ToArray();
        database.Add(ingredient);
        database.AddRange(variants);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            return new(IngredientMutationStatus.DuplicateName);
        }

        return new(IngredientMutationStatus.Created, new IngredientCatalogEntry(
            ingredient.Id, ingredient.Name, ingredient.ScopeType, ingredient.ScopeId,
            ingredient.OriginInformation,
            variants.Select(value => new IngredientVariantItem(value.Id, value.Name)).ToArray(),
            [], []));
    }
}
