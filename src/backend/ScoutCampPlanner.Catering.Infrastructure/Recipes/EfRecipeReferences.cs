using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class EfRecipeReferences(CateringDbContext database) :
    IRecipeValidationReferences,
    IRecipeSnapshotReferences,
    IRecipeSnapshotSource,
    IRecipeRevisionSource
{
    public IngredientDescriptor? FindIngredient(Guid ingredientRevisionId) =>
        (from revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
         join identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
             on revision.IngredientId equals identity.Id
         where revision.Id == ingredientRevisionId &&
               revision.State == (int)IngredientRevisionState.Published
         select new IngredientDescriptor(
             revision.Id, (IngredientScopeType)identity.ScopeType, identity.ScopeId))
        .SingleOrDefault();

    public bool IsUnitAvailableForIngredient(Guid ingredientRevisionId, Guid unitId)
    {
        IngredientRevisionRecord? revision = database.Set<IngredientRevisionRecord>().AsNoTracking()
            .SingleOrDefault(value => value.Id == ingredientRevisionId &&
                                      value.State == (int)IngredientRevisionState.Published);
        if (revision is null) return false;
        if (revision.BaseUnitId == unitId) return true;
        if (database.Set<IngredientRevisionUnitConversionRecord>().AsNoTracking()
            .Any(value => value.IngredientRevisionId == ingredientRevisionId && value.SourceUnitId == unitId))
            return true;
        MeasurementUnit[] units = database.MeasurementUnits.AsNoTracking()
            .Where(value => value.Id == revision.BaseUnitId || value.Id == unitId).ToArray();
        return units.Length == 2 && IsAutomaticUnitPair(units[0], units[1]);
    }

    public IReadOnlySet<ConflictReference> GetIngredientConflicts(Guid ingredientRevisionId)
    {
        var result = new HashSet<ConflictReference>();
        result.UnionWith(database.Set<IngredientRevisionAllergenRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == ingredientRevisionId &&
                (value.State == (int)IngredientPropertyState.Contains ||
                 value.State == (int)IngredientPropertyState.MayContain))
            .Select(value => new ConflictReference(ConflictType.Allergen, value.AllergenId)));
        result.UnionWith(database.Set<IngredientRevisionIntoleranceRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == ingredientRevisionId &&
                (value.State == (int)IngredientPropertyState.Contains ||
                 value.State == (int)IngredientPropertyState.MayContain))
            .Select(value => new ConflictReference(ConflictType.Intolerance, value.IntoleranceId)));
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

    public IngredientSnapshotSource GetIngredient(Guid ingredientRevisionId)
    {
        IngredientRevisionRecord ingredient = database.Set<IngredientRevisionRecord>().AsNoTracking()
            .Single(value => value.Id == ingredientRevisionId &&
                             value.State == (int)IngredientRevisionState.Published);
        return new IngredientSnapshotSource(
            ingredient.Id, ingredient.Name,
            GetIngredientConflicts(ingredient.Id).OrderBy(value => value.Type).ThenBy(value => value.Id).ToArray());
    }

    public IngredientUnitSnapshot GetIngredientUnit(Guid ingredientRevisionId, Guid unitId)
    {
        IngredientRevisionRecord revision = database.Set<IngredientRevisionRecord>().AsNoTracking()
            .Single(value => value.Id == ingredientRevisionId &&
                             value.State == (int)IngredientRevisionState.Published);
        if (revision.BaseUnitId == unitId)
            return new IngredientUnitSnapshot(GetUnit(unitId), 1m);
        decimal? explicitFactor = database.Set<IngredientRevisionUnitConversionRecord>().AsNoTracking()
            .Where(value => value.IngredientRevisionId == ingredientRevisionId && value.SourceUnitId == unitId)
            .Select(value => (decimal?)value.FactorToBaseUnit).SingleOrDefault();
        if (explicitFactor.HasValue)
            return new IngredientUnitSnapshot(GetUnit(unitId), explicitFactor.Value);
        MeasurementUnit baseUnit = database.MeasurementUnits.AsNoTracking()
            .Single(value => value.Id == revision.BaseUnitId);
        MeasurementUnit selectedUnit = database.MeasurementUnits.AsNoTracking().Single(value => value.Id == unitId);
        if (!IsAutomaticUnitPair(baseUnit, selectedUnit))
            throw new InvalidOperationException("The unit is not available for this ingredient revision.");
        return new IngredientUnitSnapshot(
            GetUnit(unitId), selectedUnit.BaseUnitFactor / baseUnit.BaseUnitFactor);
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

    public RecipeRevisionSnapshot GetRevisionSnapshot(Guid revisionId)
    {
        var revision = database.Set<RecipeRevisionRecord>().AsNoTracking()
            .Where(value => value.Id == revisionId)
            .Join(database.Set<RecipeRecord>().AsNoTracking(), revision => revision.RecipeId, recipe => recipe.Id,
                (revision, recipe) => new { revision.RecipeId, revision.SnapshotJson, recipe.ScopeType })
            .Single();
        return new RecipeRevisionSnapshot(
            revision.RecipeId, RecipeSnapshotBuilder.Deserialize(revision.SnapshotJson),
            (RecipeScopeType)revision.ScopeType);
    }

    private static void AddEdge(Dictionary<Guid, HashSet<Guid>> edges, Guid source, Guid target)
    {
        if (target == Guid.Empty) return;
        if (!edges.TryGetValue(source, out HashSet<Guid>? targets)) edges[source] = targets = [];
        targets.Add(target);
    }

    private static bool IsAutomaticUnitPair(MeasurementUnit first, MeasurementUnit second) =>
        first.Dimension == second.Dimension &&
        ((first.Symbol is "g" or "kg" && second.Symbol is "g" or "kg") ||
         (first.Symbol is "ml" or "l" && second.Symbol is "ml" or "l"));
}
