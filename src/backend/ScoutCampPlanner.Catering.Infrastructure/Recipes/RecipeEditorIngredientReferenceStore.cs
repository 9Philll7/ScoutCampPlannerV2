using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class RecipeEditorIngredientReferenceStore(CateringDbContext database)
    : IRecipeEditorIngredientReferenceStore
{
    public async Task<IReadOnlyList<RecipeEditorIngredientReference>> FindPublishedAsync(
        IReadOnlyCollection<Guid> revisionIds,
        CancellationToken cancellationToken = default)
    {
        if (revisionIds.Count == 0) return [];
        Guid[] ids = revisionIds.Distinct().ToArray();
        var headers = await (
            from revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
            join identity in database.Set<IngredientIdentityRecord>().AsNoTracking()
                on revision.IngredientId equals identity.Id
            where ids.Contains(revision.Id) && revision.State == (int)IngredientRevisionState.Published
            select new { Revision = revision, Scope = (IngredientScopeType)identity.ScopeType })
            .ToArrayAsync(cancellationToken);
        IngredientRevisionUnitConversionRecord[] conversions = await database
            .Set<IngredientRevisionUnitConversionRecord>().AsNoTracking()
            .Where(value => ids.Contains(value.IngredientRevisionId)).ToArrayAsync(cancellationToken);
        Guid[] unitIds = headers.Select(value => value.Revision.BaseUnitId)
            .Concat(conversions.Select(value => value.SourceUnitId)).Distinct().ToArray();
        Dictionary<Guid, MeasurementUnit> units = await database.MeasurementUnits.AsNoTracking()
            .Where(value => unitIds.Contains(value.Id) || value.Symbol == "g" || value.Symbol == "kg" ||
                            value.Symbol == "ml" || value.Symbol == "l")
            .ToDictionaryAsync(value => value.Id, cancellationToken);

        return headers.Select(header => new RecipeEditorIngredientReference(
                header.Revision.Id,
                header.Revision.Name,
                header.Scope,
                BuildUnits(header.Revision, conversions, units)))
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.RevisionId)
            .ToArray();
    }

    private static RecipeEditorIngredientUnitReference[] BuildUnits(
        IngredientRevisionRecord revision,
        IEnumerable<IngredientRevisionUnitConversionRecord> conversions,
        IReadOnlyDictionary<Guid, MeasurementUnit> definitions)
    {
        if (!definitions.TryGetValue(revision.BaseUnitId, out MeasurementUnit? baseUnit)) return [];
        RecipeEditorIngredientUnitReference[] automatic = definitions.Values
            .Where(value => value.Dimension == baseUnit.Dimension && value.Id != baseUnit.Id &&
                (IsMassPair(baseUnit.Symbol, value.Symbol) || IsVolumePair(baseUnit.Symbol, value.Symbol)))
            .Select(value => Map(value, value.BaseUnitFactor / baseUnit.BaseUnitFactor)).ToArray();
        HashSet<Guid> automaticIds = automatic.Select(value => value.UnitId).ToHashSet();
        return new[] { Map(baseUnit, 1m) }
            .Concat(automatic)
            .Concat(conversions.Where(value => value.IngredientRevisionId == revision.Id &&
                                               value.SourceUnitId != revision.BaseUnitId &&
                                               !automaticIds.Contains(value.SourceUnitId))
                .Select(value => definitions.TryGetValue(value.SourceUnitId, out MeasurementUnit? unit)
                    ? Map(unit, value.FactorToBaseUnit)
                    : null)
                .OfType<RecipeEditorIngredientUnitReference>())
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsMassPair(string first, string second) =>
        (first is "g" or "kg") && (second is "g" or "kg");

    private static bool IsVolumePair(string first, string second) =>
        (first is "ml" or "l") && (second is "ml" or "l");

    private static RecipeEditorIngredientUnitReference Map(MeasurementUnit unit, decimal factor) =>
        new(unit.Id, unit.Name, unit.Symbol, unit.Dimension, unit.BaseUnitFactor, factor);
}
