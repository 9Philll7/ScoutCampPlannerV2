using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;
using System.Linq.Expressions;

namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

public sealed class IngredientCatalogStore(CateringDbContext database) : IIngredientCatalogStore
{
    public Task<IReadOnlyList<IngredientCatalogEntry>> ListCentralAsync(
        CancellationToken cancellationToken = default) =>
        LoadAsync(
            value => value.ScopeType == IngredientScopeType.Central,
            value => value.ScopeType == (int)IngredientScopeType.Central,
            cancellationToken);

    public Task<IReadOnlyList<IngredientCatalogEntry>> ListTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        LoadAsync(
            value => value.ScopeType == IngredientScopeType.Central ||
                     value.ScopeType == IngredientScopeType.Tenant && value.ScopeId == tenantId,
            value => value.ScopeType == (int)IngredientScopeType.Central ||
                     value.ScopeType == (int)IngredientScopeType.Tenant && value.ScopeId == tenantId,
            cancellationToken);

    public Task<IReadOnlyList<IngredientCatalogEntry>> ListCampAsync(
        Guid tenantId, Guid campId, CancellationToken cancellationToken = default) =>
        LoadAsync(
            value => value.ScopeType == IngredientScopeType.Central ||
                     value.ScopeType == IngredientScopeType.Tenant && value.ScopeId == tenantId ||
                     value.ScopeType == IngredientScopeType.Camp && value.ScopeId == campId,
            value => value.ScopeType == (int)IngredientScopeType.Central ||
                     value.ScopeType == (int)IngredientScopeType.Tenant && value.ScopeId == tenantId ||
                     value.ScopeType == (int)IngredientScopeType.Camp && value.ScopeId == campId,
            cancellationToken);

    private async Task<IReadOnlyList<IngredientCatalogEntry>> LoadAsync(
        Expression<Func<BaseIngredient, bool>> legacyPredicate,
        Expression<Func<IngredientIdentityRecord, bool>> revisionedPredicate,
        CancellationToken cancellationToken)
    {
        BaseIngredient[] ingredients = await database.BaseIngredients.AsNoTracking()
            .Where(legacyPredicate).ToArrayAsync(cancellationToken);
        Guid[] ids = ingredients.Select(value => value.Id).ToArray();
        IngredientVariant[] variants = await database.IngredientVariants.AsNoTracking()
            .Where(value => ids.Contains(value.BaseIngredientId)).ToArrayAsync(cancellationToken);
        var units = await (from conversion in database.IngredientUnitConversions.AsNoTracking()
            join unit in database.MeasurementUnits.AsNoTracking() on conversion.UnitId equals unit.Id
            where ids.Contains(conversion.BaseIngredientId)
            select new { conversion.BaseIngredientId, Conversion = conversion, Unit = unit })
            .ToArrayAsync(cancellationToken);
        BaseIngredientAllergen[] allergens = await database.BaseIngredientAllergens.AsNoTracking()
            .Where(value => ids.Contains(value.BaseIngredientId)).ToArrayAsync(cancellationToken);
        BaseIngredientIntolerance[] intolerances = await database.BaseIngredientIntolerances.AsNoTracking()
            .Where(value => ids.Contains(value.BaseIngredientId)).ToArrayAsync(cancellationToken);
        BaseIngredientDietaryRequirement[] requirements = await database.BaseIngredientDietaryRequirements.AsNoTracking()
            .Where(value => ids.Contains(value.BaseIngredientId)).ToArrayAsync(cancellationToken);
        Dictionary<Guid, string> allergenNames = await database.Allergens.AsNoTracking()
            .ToDictionaryAsync(value => value.Id, value => value.Name, cancellationToken);
        Dictionary<Guid, string> intoleranceNames = await database.Intolerances.AsNoTracking()
            .ToDictionaryAsync(value => value.Id, value => value.Name, cancellationToken);
        Dictionary<Guid, string> requirementNames = await database.DietaryRequirements.AsNoTracking()
            .ToDictionaryAsync(value => value.Id, value => value.Name, cancellationToken);

        IngredientCatalogEntry[] legacyEntries = ingredients.Select(ingredient => new IngredientCatalogEntry(
                ingredient.Id, null, ingredient.Name, ingredient.ScopeType, ingredient.ScopeId,
                ingredient.OriginInformation,
                variants.Where(value => value.BaseIngredientId == ingredient.Id)
                    .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(value => new IngredientVariantItem(value.Id, value.Name)).ToArray(),
                units.Where(value => value.BaseIngredientId == ingredient.Id)
                    .OrderBy(value => value.Unit.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(value => new IngredientUnitItem(
                        value.Unit.Id, value.Unit.Name, value.Unit.Symbol, value.Unit.Dimension,
                        value.Unit.BaseUnitFactor, value.Conversion.ReferenceQuantityPerUnit)).ToArray(),
                BuildConflicts(ingredient.Id, allergens, intolerances, requirements,
                    allergenNames, intoleranceNames, requirementNames)))
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.Id)
            .ToArray();

        IngredientCatalogEntry[] revisionedEntries = await LoadRevisionedAsync(
            revisionedPredicate, cancellationToken);
        Dictionary<Guid, IngredientCatalogEntry> legacyById = legacyEntries.ToDictionary(value => value.Id);
        IngredientCatalogEntry[] preferredRevisionedEntries = revisionedEntries
            .Select(value => value.OriginInformation is null && legacyById.TryGetValue(value.Id, out var legacy)
                ? value with { OriginInformation = legacy.OriginInformation }
                : value)
            .ToArray();
        HashSet<Guid> revisionedIds = preferredRevisionedEntries.Select(value => value.Id).ToHashSet();

        return legacyEntries.Where(value => !revisionedIds.Contains(value.Id))
            .Concat(preferredRevisionedEntries)
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Id)
            .ToArray();
    }

    private async Task<IngredientCatalogEntry[]> LoadRevisionedAsync(
        Expression<Func<IngredientIdentityRecord, bool>> predicate,
        CancellationToken cancellationToken)
    {
        IQueryable<IngredientIdentityRecord> visibleIdentities = database
            .Set<IngredientIdentityRecord>().AsNoTracking().Where(predicate);
        var headers = await (
            from identity in visibleIdentities
            join revision in database.Set<IngredientRevisionRecord>().AsNoTracking()
                on identity.CurrentPublishedRevisionId equals revision.Id
            where identity.Status == (int)IngredientIdentityStatus.Active &&
                  revision.State == (int)IngredientRevisionState.Published
            select new
            {
                IngredientId = identity.Id,
                Scope = (IngredientScopeType)identity.ScopeType,
                identity.ScopeId,
                Revision = revision,
            }).ToArrayAsync(cancellationToken);
        Guid[] revisionIds = headers.Select(value => value.Revision.Id).ToArray();
        if (revisionIds.Length == 0)
            return [];

        IngredientVariantRevisionRecord[] variants = await database.Set<IngredientVariantRevisionRecord>()
            .AsNoTracking()
            .Where(value => revisionIds.Contains(value.IngredientRevisionId) && value.Status == 0)
            .ToArrayAsync(cancellationToken);
        IngredientRevisionUnitConversionRecord[] conversions = await database
            .Set<IngredientRevisionUnitConversionRecord>().AsNoTracking()
            .Where(value => revisionIds.Contains(value.IngredientRevisionId))
            .ToArrayAsync(cancellationToken);
        Guid[] unitIds = headers.Select(value => value.Revision.BaseUnitId)
            .Concat(conversions.Select(value => value.SourceUnitId)).Distinct().ToArray();
        Dictionary<Guid, MeasurementUnit> unitDefinitions = await database.MeasurementUnits.AsNoTracking()
            .Where(value => unitIds.Contains(value.Id) || value.Symbol == "g" || value.Symbol == "kg" ||
                            value.Symbol == "ml" || value.Symbol == "l")
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var origins = await (
            from value in database.Set<IngredientRevisionOriginRecord>().AsNoTracking()
            join definition in database.Set<IngredientOriginPropertyRecord>().AsNoTracking()
                on value.OriginPropertyId equals definition.Id
            where revisionIds.Contains(value.IngredientRevisionId) &&
                  value.State == (int)IngredientPropertyState.Contains
            select new { value.IngredientRevisionId, definition.Name })
            .ToArrayAsync(cancellationToken);
        var allergenConflicts = await (
            from value in database.Set<IngredientRevisionAllergenRecord>().AsNoTracking()
            join definition in database.Set<IngredientAllergenDefinitionRecord>().AsNoTracking()
                on value.AllergenId equals definition.Id
            where revisionIds.Contains(value.IngredientRevisionId) &&
                  (value.State == (int)IngredientPropertyState.Contains ||
                   value.State == (int)IngredientPropertyState.MayContain)
            select new
            {
                value.IngredientRevisionId,
                Conflict = new IngredientConflictItem(ConflictType.Allergen, definition.Id, definition.Name),
            }).ToArrayAsync(cancellationToken);
        var intoleranceConflicts = await (
            from value in database.Set<IngredientRevisionIntoleranceRecord>().AsNoTracking()
            join definition in database.Set<IngredientIntoleranceDefinitionRecord>().AsNoTracking()
                on value.IntoleranceId equals definition.Id
            where revisionIds.Contains(value.IngredientRevisionId) &&
                  (value.State == (int)IngredientPropertyState.Contains ||
                   value.State == (int)IngredientPropertyState.MayContain)
            select new
            {
                value.IngredientRevisionId,
                Conflict = new IngredientConflictItem(ConflictType.Intolerance, definition.Id, definition.Name),
            }).ToArrayAsync(cancellationToken);

        return headers.Select(header =>
        {
            Guid revisionId = header.Revision.Id;
            IngredientUnitItem[] units = BuildRevisionedUnits(header.Revision, conversions, unitDefinitions);
            IngredientConflictItem[] conflicts = allergenConflicts
                .Where(value => value.IngredientRevisionId == revisionId).Select(value => value.Conflict)
                .Concat(intoleranceConflicts.Where(value => value.IngredientRevisionId == revisionId)
                    .Select(value => value.Conflict))
                .OrderBy(value => value.Type).ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string? originInformation = JoinNames(origins
                .Where(value => value.IngredientRevisionId == revisionId).Select(value => value.Name));

            return new IngredientCatalogEntry(
                header.IngredientId,
                header.Revision.Id,
                header.Revision.Name,
                header.Scope,
                header.ScopeId,
                originInformation,
                variants.Where(value => value.IngredientRevisionId == revisionId)
                    .OrderBy(value => value.SortOrder).ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(value => new IngredientVariantItem(value.Id, value.Name)).ToArray(),
                units,
                conflicts);
        }).ToArray();
    }

    private static IngredientUnitItem[] BuildRevisionedUnits(
        IngredientRevisionRecord revision,
        IEnumerable<IngredientRevisionUnitConversionRecord> conversions,
        IReadOnlyDictionary<Guid, MeasurementUnit> definitions)
    {
        if (!definitions.TryGetValue(revision.BaseUnitId, out MeasurementUnit? baseUnit))
            return [];

        IngredientUnitItem[] automaticUnits = definitions.Values
            .Where(value => value.Dimension == baseUnit.Dimension && value.Id != baseUnit.Id &&
                ((baseUnit.Symbol == "g" || baseUnit.Symbol == "kg") &&
                    (value.Symbol == "g" || value.Symbol == "kg") ||
                 (baseUnit.Symbol == "ml" || baseUnit.Symbol == "l") &&
                    (value.Symbol == "ml" || value.Symbol == "l")))
            .Select(value => UnitItem(value, value.BaseUnitFactor / baseUnit.BaseUnitFactor))
            .ToArray();
        HashSet<Guid> automaticUnitIds = automaticUnits.Select(value => value.UnitId).ToHashSet();

        return new[] { UnitItem(baseUnit, 1m) }
            .Concat(automaticUnits)
            .Concat(conversions.Where(value => value.IngredientRevisionId == revision.Id &&
                                               value.SourceUnitId != revision.BaseUnitId &&
                                               !automaticUnitIds.Contains(value.SourceUnitId))
                .Select(value => definitions.TryGetValue(value.SourceUnitId, out MeasurementUnit? unit)
                    ? UnitItem(unit, value.FactorToBaseUnit)
                    : null)
                .OfType<IngredientUnitItem>())
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IngredientUnitItem UnitItem(MeasurementUnit unit, decimal referenceQuantityPerUnit) =>
        new(unit.Id, unit.Name, unit.Symbol, unit.Dimension, unit.BaseUnitFactor, referenceQuantityPerUnit);

    private static string? JoinNames(IEnumerable<string> names)
    {
        string value = string.Join(", ", names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        return value.Length == 0 ? null : value;
    }

    private static IngredientConflictItem[] BuildConflicts(
        Guid ingredientId,
        IEnumerable<BaseIngredientAllergen> allergens,
        IEnumerable<BaseIngredientIntolerance> intolerances,
        IEnumerable<BaseIngredientDietaryRequirement> requirements,
        IReadOnlyDictionary<Guid, string> allergenNames,
        IReadOnlyDictionary<Guid, string> intoleranceNames,
        IReadOnlyDictionary<Guid, string> requirementNames) =>
        allergens.Where(value => value.BaseIngredientId == ingredientId)
            .Select(value => new IngredientConflictItem(
                ConflictType.Allergen, value.AllergenId, allergenNames[value.AllergenId]))
            .Concat(intolerances.Where(value => value.BaseIngredientId == ingredientId)
                .Select(value => new IngredientConflictItem(
                    ConflictType.Intolerance, value.IntoleranceId, intoleranceNames[value.IntoleranceId])))
            .Concat(requirements.Where(value => value.BaseIngredientId == ingredientId)
                .Select(value => new IngredientConflictItem(
                    ConflictType.DietaryRequirement, value.DietaryRequirementId,
                    requirementNames[value.DietaryRequirementId])))
            .OrderBy(value => value.Type).ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
