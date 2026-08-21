using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class EfRecipeReferences(CateringDbContext database) :
    IRecipeValidationReferences,
    IRecipeSnapshotReferences,
    IRecipeSnapshotSource
{
    public IngredientDescriptor? FindIngredient(Guid ingredientId) => database.BaseIngredients.AsNoTracking()
        .Where(value => value.Id == ingredientId)
        .Select(value => new IngredientDescriptor(value.Id, value.ScopeType, value.ScopeId))
        .SingleOrDefault();

    public bool IsUnitAvailableForIngredient(Guid ingredientId, Guid unitId) =>
        database.IngredientUnitConversions.AsNoTracking()
            .Any(value => value.BaseIngredientId == ingredientId && value.UnitId == unitId);

    public IReadOnlySet<ConflictReference> GetIngredientConflicts(Guid ingredientId)
    {
        var result = new HashSet<ConflictReference>();
        result.UnionWith(database.BaseIngredientAllergens.AsNoTracking()
            .Where(value => value.BaseIngredientId == ingredientId)
            .Select(value => new ConflictReference(ConflictType.Allergen, value.AllergenId)));
        result.UnionWith(database.BaseIngredientIntolerances.AsNoTracking()
            .Where(value => value.BaseIngredientId == ingredientId)
            .Select(value => new ConflictReference(ConflictType.Intolerance, value.IntoleranceId)));
        result.UnionWith(database.BaseIngredientDietaryRequirements.AsNoTracking()
            .Where(value => value.BaseIngredientId == ingredientId)
            .Select(value => new ConflictReference(ConflictType.DietaryRequirement, value.DietaryRequirementId)));
        return result;
    }

    public bool UnitExists(Guid unitId) => database.MeasurementUnits.AsNoTracking().Any(value => value.Id == unitId);

    public bool AreUnitsCompatible(Guid sourceUnitId, Guid targetUnitId)
    {
        if (sourceUnitId == targetUnitId) return UnitExists(sourceUnitId);
        MeasurementDimension[] dimensions = database.MeasurementUnits.AsNoTracking()
            .Where(value => value.Id == sourceUnitId || value.Id == targetUnitId)
            .Select(value => value.Dimension).ToArray();
        return dimensions.Length == 2 && dimensions[0] == dimensions[1];
    }

    public RecipeRevisionDescriptor? FindRevision(Guid revisionId)
    {
        var value = (from revision in database.Set<RecipeRevisionRecord>().AsNoTracking()
            join recipe in database.Set<RecipeRecord>().AsNoTracking() on revision.RecipeId equals recipe.Id
            where revision.Id == revisionId
            select new { Revision = revision, Recipe = recipe }).SingleOrDefault();
        if (value is null) return null;
        RecipeSnapshot snapshot = RecipeSnapshotBuilder.Deserialize(value.Revision.SnapshotJson);
        return new RecipeRevisionDescriptor(
            value.Revision.Id, value.Recipe.Id, snapshot.RecipeType,
            (RecipeStatus)value.Recipe.Status, snapshot.Reference.ReferenceUnit?.UnitId,
            snapshot.ExposedConflicts.ToHashSet());
    }

    public bool WouldCreateCycle(Guid recipeId, Guid referencedRecipeId)
    {
        if (recipeId == referencedRecipeId) return true;
        Dictionary<Guid, Guid> revisionRecipes = database.Set<RecipeRevisionRecord>().AsNoTracking()
            .ToDictionary(value => value.Id, value => value.RecipeId);
        var edges = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var edge in database.Set<RecipeSubrecipePositionRecord>().AsNoTracking()
                     .Where(value => value.RecipeRevisionId.HasValue)
                     .Select(value => new { value.RecipeId, RevisionId = value.RecipeRevisionId!.Value }))
            AddEdge(edges, edge.RecipeId, revisionRecipes.GetValueOrDefault(edge.RevisionId));
        foreach (var edge in from replacement in database.Set<RecipeSubrecipeReplacementRecord>().AsNoTracking()
                 join position in database.Set<RecipeSubrecipePositionRecord>().AsNoTracking()
                     on replacement.SubrecipePositionId equals position.Id
                 where replacement.ReplacementRecipeRevisionId.HasValue
                 select new { position.RecipeId, RevisionId = replacement.ReplacementRecipeRevisionId!.Value })
            AddEdge(edges, edge.RecipeId, revisionRecipes.GetValueOrDefault(edge.RevisionId));

        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        pending.Push(referencedRecipeId);
        while (pending.TryPop(out Guid current))
        {
            if (current == recipeId) return true;
            if (!visited.Add(current) || !edges.TryGetValue(current, out HashSet<Guid>? targets)) continue;
            foreach (Guid target in targets) pending.Push(target);
        }
        return false;
    }

    public IngredientSnapshotSource GetIngredient(Guid ingredientId)
    {
        BaseIngredient ingredient = database.BaseIngredients.AsNoTracking()
            .Single(value => value.Id == ingredientId);
        return new IngredientSnapshotSource(
            ingredient.Id, ingredient.Name,
            GetIngredientConflicts(ingredient.Id).OrderBy(value => value.Type).ThenBy(value => value.Id).ToArray());
    }

    public IngredientUnitSnapshot GetIngredientUnit(Guid ingredientId, Guid unitId)
    {
        IngredientUnitConversion conversion = database.IngredientUnitConversions.AsNoTracking()
            .Single(value => value.BaseIngredientId == ingredientId && value.UnitId == unitId);
        return new IngredientUnitSnapshot(GetUnit(unitId), conversion.ReferenceQuantityPerUnit);
    }

    public MeasurementUnitSnapshot GetUnit(Guid unitId)
    {
        MeasurementUnit unit = database.MeasurementUnits.AsNoTracking().Single(value => value.Id == unitId);
        return new MeasurementUnitSnapshot(
            unit.Id, unit.Name, unit.Symbol, unit.Dimension, unit.BaseUnitFactor);
    }

    public IReadOnlySet<ConflictReference> GetRevisionConflicts(Guid revisionId) =>
        GetRevision(revisionId).ExposedConflicts.ToHashSet();

    public RecipeSnapshot GetRevision(Guid revisionId) =>
        RecipeSnapshotBuilder.Deserialize(database.Set<RecipeRevisionRecord>().AsNoTracking()
            .Where(value => value.Id == revisionId).Select(value => value.SnapshotJson).Single());

    private static void AddEdge(Dictionary<Guid, HashSet<Guid>> edges, Guid source, Guid target)
    {
        if (target == Guid.Empty) return;
        if (!edges.TryGetValue(source, out HashSet<Guid>? targets)) edges[source] = targets = [];
        targets.Add(target);
    }
}
