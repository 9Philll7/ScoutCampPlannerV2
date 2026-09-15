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
            select new
            {
                Revision = revision,
                IngredientId = identity.Id,
                Scope = (IngredientScopeType)identity.ScopeType,
                IdentityStatus = (IngredientIdentityStatus)identity.Status,
                identity.CurrentPublishedRevisionId,
            })
            .ToArrayAsync(cancellationToken);
        Guid[] currentRevisionIds = headers
            .Where(value => value.IdentityStatus == IngredientIdentityStatus.Active &&
                            value.CurrentPublishedRevisionId.HasValue &&
                            value.CurrentPublishedRevisionId != value.Revision.Id)
            .Select(value => value.CurrentPublishedRevisionId!.Value).Distinct().ToArray();
        IngredientRevisionRecord[] currentRevisions = await database.Set<IngredientRevisionRecord>().AsNoTracking()
            .Where(value => currentRevisionIds.Contains(value.Id) &&
                            value.State == (int)IngredientRevisionState.Published)
            .ToArrayAsync(cancellationToken);
        IngredientRevisionRecord[] allRevisions = headers.Select(value => value.Revision)
            .Concat(currentRevisions).DistinctBy(value => value.Id).ToArray();
        Guid[] allRevisionIds = allRevisions.Select(value => value.Id).ToArray();
        IngredientRevisionUnitConversionRecord[] conversions = await database
            .Set<IngredientRevisionUnitConversionRecord>().AsNoTracking()
            .Where(value => allRevisionIds.Contains(value.IngredientRevisionId)).ToArrayAsync(cancellationToken);
        Guid[] unitIds = allRevisions.Select(value => value.BaseUnitId)
            .Concat(conversions.Select(value => value.SourceUnitId)).Distinct().ToArray();
        Dictionary<Guid, MeasurementUnit> units = await database.MeasurementUnits.AsNoTracking()
            .Where(value => unitIds.Contains(value.Id) || value.Symbol == "g" || value.Symbol == "kg" ||
                            value.Symbol == "ml" || value.Symbol == "l")
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        IngredientVariantRevisionRecord[] variants = await database.Set<IngredientVariantRevisionRecord>()
            .AsNoTracking().Where(value => allRevisionIds.Contains(value.IngredientRevisionId) && value.Status == 0)
            .ToArrayAsync(cancellationToken);
        Guid[] variantIds = variants.Select(value => value.Id).ToArray();
        IngredientVariantAllergenOverrideRecord[] variantAllergens = await database
            .Set<IngredientVariantAllergenOverrideRecord>().AsNoTracking()
            .Where(value => variantIds.Contains(value.VariantRevisionId) &&
                            value.State == (int)IngredientPropertyState.DoesNotContain)
            .ToArrayAsync(cancellationToken);
        IngredientVariantIntoleranceOverrideRecord[] variantIntolerances = await database
            .Set<IngredientVariantIntoleranceOverrideRecord>().AsNoTracking()
            .Where(value => variantIds.Contains(value.VariantRevisionId) &&
                            value.State == (int)IngredientPropertyState.DoesNotContain)
            .ToArrayAsync(cancellationToken);
        var preventingVariants = new Dictionary<(Guid RevisionId, ConflictType Type, Guid ConflictId), string[]>();
        foreach (Guid revisionId in allRevisionIds)
        {
            IngredientVariantRevisionRecord[] revisionVariants = variants
                .Where(value => value.IngredientRevisionId == revisionId).ToArray();
            foreach (var group in variantAllergens
                         .Join(revisionVariants, value => value.VariantRevisionId, variant => variant.Id,
                             (value, variant) => new { value.AllergenId, variant.Name })
                         .GroupBy(value => value.AllergenId))
                preventingVariants[(revisionId, ConflictType.Allergen, group.Key)] =
                    group.Select(value => value.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var group in variantIntolerances
                         .Join(revisionVariants, value => value.VariantRevisionId, variant => variant.Id,
                             (value, variant) => new { value.IntoleranceId, variant.Name })
                         .GroupBy(value => value.IntoleranceId))
                preventingVariants[(revisionId, ConflictType.Intolerance, group.Key)] =
                    group.Select(value => value.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        var allergenConflicts = await (
            from value in database.Set<IngredientRevisionAllergenRecord>().AsNoTracking()
            join definition in database.Set<IngredientAllergenDefinitionRecord>().AsNoTracking()
                on value.AllergenId equals definition.Id
            where allRevisionIds.Contains(value.IngredientRevisionId) &&
                  (value.State == (int)IngredientPropertyState.Contains ||
                   value.State == (int)IngredientPropertyState.MayContain)
            select new
            {
                value.IngredientRevisionId,
                definition.Id,
                definition.Name,
            }).ToArrayAsync(cancellationToken);
        var intoleranceConflicts = await (
            from value in database.Set<IngredientRevisionIntoleranceRecord>().AsNoTracking()
            join definition in database.Set<IngredientIntoleranceDefinitionRecord>().AsNoTracking()
                on value.IntoleranceId equals definition.Id
            where allRevisionIds.Contains(value.IngredientRevisionId) &&
                  (value.State == (int)IngredientPropertyState.Contains ||
                   value.State == (int)IngredientPropertyState.MayContain)
            select new
            {
                value.IngredientRevisionId,
                definition.Id,
                definition.Name,
            }).ToArrayAsync(cancellationToken);
        Dictionary<Guid, RecipeEditorConflictReference[]> conflicts = allRevisionIds.ToDictionary(
            revisionId => revisionId,
            revisionId => allergenConflicts
                .Where(value => value.IngredientRevisionId == revisionId)
                .Select(value => new RecipeEditorConflictReference(
                    ConflictType.Allergen, value.Id, value.Name,
                    preventingVariants.GetValueOrDefault((revisionId, ConflictType.Allergen, value.Id)) ?? []))
                .Concat(intoleranceConflicts.Where(value => value.IngredientRevisionId == revisionId)
                    .Select(value => new RecipeEditorConflictReference(
                        ConflictType.Intolerance, value.Id, value.Name,
                        preventingVariants.GetValueOrDefault((revisionId, ConflictType.Intolerance, value.Id)) ?? [])))
                .OrderBy(value => value.Type).ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        Dictionary<Guid, IngredientRevisionRecord> currentById = currentRevisions.ToDictionary(value => value.Id);

        return headers.Select(header => new RecipeEditorIngredientReference(
                header.Revision.Id,
                header.IngredientId,
                header.Revision.RevisionNumber,
                header.Revision.Name,
                header.Scope,
                BuildUnits(header.Revision, conversions, units),
                conflicts.GetValueOrDefault(header.Revision.Id) ?? [],
                header.CurrentPublishedRevisionId.HasValue &&
                currentById.TryGetValue(header.CurrentPublishedRevisionId.Value, out IngredientRevisionRecord? current)
                    ? new RecipeEditorIngredientUpdate(
                        current.Id,
                        current.RevisionNumber,
                        current.Name,
                        BuildUnits(current, conversions, units),
                        conflicts.GetValueOrDefault(current.Id) ?? [])
                    : null))
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
