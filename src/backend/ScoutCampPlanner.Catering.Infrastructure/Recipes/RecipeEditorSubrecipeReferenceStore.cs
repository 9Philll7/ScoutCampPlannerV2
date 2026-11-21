using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class RecipeEditorSubrecipeReferenceStore(CateringDbContext database) : IRecipeEditorSubrecipeReferenceStore
{
    public async Task<IReadOnlyList<RecipeEditorSubrecipeReference>> FindAsync(Guid campId, Guid recipeId,
        IReadOnlyCollection<Guid> pinnedRevisionIds, CancellationToken cancellationToken = default)
    {
        // Limit both candidates and historical metadata to recipes in this camp's accessible library.
        var catalogue = await new RecipeCatalogStore(database).ListCampAsync(campId, cancellationToken);
        Guid[] recipes = catalogue.Select(value => value.RecipeId).Distinct().ToArray();
        Guid[] current = catalogue.Where(value => value.Status == RecipeStatus.Active && value.RevisionId.HasValue)
            .Select(value => value.RevisionId!.Value).ToArray();
        var rows = await database.Set<RecipeRevisionRecord>().AsNoTracking()
            .Where(value => recipes.Contains(value.RecipeId) && (current.Contains(value.Id) || pinnedRevisionIds.Contains(value.Id)))
            .ToArrayAsync(cancellationToken);
        var units = await database.MeasurementUnits.AsNoTracking().ToArrayAsync(cancellationToken);
        var references = new EfRecipeReferences(database);
        var snapshots = rows.ToDictionary(value => value.Id, value => RecipeSnapshotBuilder.Deserialize(value.SnapshotJson));
        var attempted = snapshots.Keys.ToHashSet();
        while (true)
        {
            Guid[] missing = snapshots.Values.SelectMany(value => value.SubrecipePositions).Select(value => value.RecipeRevisionId)
                .Where(value => attempted.Add(value)).Distinct().ToArray();
            if (missing.Length == 0) break;
            var nested = await database.Set<RecipeRevisionRecord>().AsNoTracking().Where(value => missing.Contains(value.Id)).ToArrayAsync(cancellationToken);
            foreach (var value in nested) snapshots[value.Id] = RecipeSnapshotBuilder.Deserialize(value.SnapshotJson);
        }
        Guid[] ingredientIds = snapshots.Values.SelectMany(value => value.IngredientPositions)
            .Select(value => value.Ingredient.IngredientRevisionId).Distinct().ToArray();
        var ingredientReferences = (await new RecipeEditorIngredientReferenceStore(database).FindPublishedAsync(ingredientIds, cancellationToken))
            .ToDictionary(value => value.RevisionId);
        return rows.Select(value =>
        {
            var snapshot = snapshots[value.Id];
            var unit = snapshot.Reference.ReferenceUnit;
            var compatible = unit is null ? [] : units.Where(candidate => candidate.Dimension == unit.Dimension)
                .Select(candidate => new MeasurementUnitSnapshot(candidate.Id, candidate.Name, candidate.Symbol, candidate.Dimension, candidate.BaseUnitFactor)).ToArray();
            bool selectable = current.Contains(value.Id) && value.RecipeId != recipeId && !references.WouldCreateCycle(recipeId, value.RecipeId);
            return new RecipeEditorSubrecipeReference(value.Id, value.RecipeId, value.RevisionNumber, snapshot.Name,
                snapshot.RecipeType, snapshot.Reference.StandardServings, snapshot.Reference.ReferenceQuantity, unit, compatible,
                selectable, snapshot.ExposedConflicts.Select(conflict => new RecipeEditorConflictReference(conflict.Type, conflict.Id,
                    references.FindConflictName(conflict) ?? "Ernährungshinweis",
                    RecipeVariantConflictSummary.Find(snapshot, conflict, id => snapshots.GetValueOrDefault(id), ingredientReferences))).ToArray());
        }).OrderBy(value => value.Name).ThenBy(value => value.RevisionNumber).ToArray();
    }
}
