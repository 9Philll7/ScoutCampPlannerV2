using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;

namespace ScoutCampPlanner.Catering.Infrastructure.Offline;

/// <summary>
/// Exports and imports the immutable Catering references required by a camp's upstream recipe library.
/// The cloud remains authoritative: importing only adds records that are not present locally.
/// </summary>
public sealed class CampOfflineReferenceStore(CateringDbContext database)
{
    private const int SchemaVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement CreateEmptyPackageData() => JsonSerializer.SerializeToElement(new Payload(
        SchemaVersion, [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], []), JsonOptions);

    public static void Validate(JsonElement json, Guid campId)
    {
        Payload payload = Read(json);
        if (payload.SchemaVersion != SchemaVersion || payload.Entries.Any(value => value.CampId != campId))
            throw new InvalidDataException("Catering reference data has an invalid schema or camp identity.");
        if (HasDuplicate(payload.Entries.Select(value => value.Id)) ||
            HasDuplicate(payload.Recipes.Select(value => value.Id)) ||
            HasDuplicate(payload.RecipeRevisions.Select(value => value.Id)) ||
            HasDuplicate(payload.Ingredients.Select(value => value.Id)) ||
            HasDuplicate(payload.IngredientRevisions.Select(value => value.Id)) ||
            HasDuplicate(payload.Variants.Select(value => value.Id)))
            throw new InvalidDataException("Catering reference data contains duplicate identities.");

        HashSet<Guid> recipeIds = payload.Recipes.Select(value => value.Id).ToHashSet();
        HashSet<Guid> recipeRevisionIds = payload.RecipeRevisions.Select(value => value.Id).ToHashSet();
        HashSet<Guid> ingredientIds = payload.Ingredients.Select(value => value.Id).ToHashSet();
        HashSet<Guid> ingredientRevisionIds = payload.IngredientRevisions.Select(value => value.Id).ToHashSet();
        HashSet<Guid> unitIds = payload.Units.Select(value => value.Id).ToHashSet();
        HashSet<Guid> categoryIds = payload.Categories.Select(value => value.Id).ToHashSet();
        HashSet<Guid> allergenIds = payload.Allergens.Select(value => value.Id).ToHashSet();
        HashSet<Guid> intoleranceIds = payload.Intolerances.Select(value => value.Id).ToHashSet();
        HashSet<Guid> originIds = payload.Origins.Select(value => value.Id).ToHashSet();
        HashSet<Guid> variantIds = payload.Variants.Select(value => value.Id).ToHashSet();
        if (payload.Entries.Any(value => !recipeRevisionIds.Contains(value.RevisionId)) ||
            payload.RecipeRevisions.Any(value => !recipeIds.Contains(value.RecipeId)) ||
            payload.Ingredients.Any(value => value.CurrentPublishedRevisionId.HasValue &&
                !ingredientRevisionIds.Contains(value.CurrentPublishedRevisionId.Value)) ||
            payload.IngredientRevisions.Any(value => !ingredientIds.Contains(value.IngredientId) ||
                value.State != (int)IngredientRevisionState.Published ||
                !unitIds.Contains(value.BaseUnitId) || !categoryIds.Contains(value.CategoryId)) ||
            payload.Variants.Any(value => !ingredientRevisionIds.Contains(value.IngredientRevisionId)) ||
            payload.RevisionNutrition.Any(value => !ingredientRevisionIds.Contains(value.OwnerId) || !unitIds.Contains(value.ReferenceUnitId)) ||
            payload.VariantNutrition.Any(value => !variantIds.Contains(value.OwnerId) || !unitIds.Contains(value.ReferenceUnitId)) ||
            payload.RevisionSubstanceContents.Concat(payload.VariantSubstanceContents).Any(value =>
                !intoleranceIds.Contains(value.SubstanceId) || !unitIds.Contains(value.AmountUnitId) ||
                !unitIds.Contains(value.ReferenceUnitId)) ||
            payload.RevisionConversions.Concat(payload.VariantConversions).Any(value => !unitIds.Contains(value.DefinitionId)))
            throw new InvalidDataException("Catering reference data contains a missing identity reference.");

        if (payload.RevisionAllergens.Any(value => !ingredientRevisionIds.Contains(value.OwnerId) || !allergenIds.Contains(value.DefinitionId)) ||
            payload.RevisionIntolerances.Any(value => !ingredientRevisionIds.Contains(value.OwnerId) || !intoleranceIds.Contains(value.DefinitionId)) ||
            payload.RevisionSubstanceContents.Any(value => !ingredientRevisionIds.Contains(value.OwnerId)) ||
            payload.RevisionOrigins.Any(value => !ingredientRevisionIds.Contains(value.OwnerId) || !originIds.Contains(value.DefinitionId)) ||
            payload.RevisionConversions.Any(value => !ingredientRevisionIds.Contains(value.OwnerId)) ||
            payload.VariantAllergens.Any(value => !variantIds.Contains(value.OwnerId) || !allergenIds.Contains(value.DefinitionId)) ||
            payload.VariantIntolerances.Any(value => !variantIds.Contains(value.OwnerId) || !intoleranceIds.Contains(value.DefinitionId)) ||
            payload.VariantSubstanceContents.Any(value => !variantIds.Contains(value.OwnerId)) ||
            payload.VariantOrigins.Any(value => !variantIds.Contains(value.OwnerId) || !originIds.Contains(value.DefinitionId)) ||
            payload.VariantConversions.Any(value => !variantIds.Contains(value.OwnerId)))
            throw new InvalidDataException("Catering reference data contains an invalid detail reference.");

        var snapshots = new Dictionary<Guid, RecipeSnapshot>();
        foreach (RecipeRevisionData revision in payload.RecipeRevisions)
        {
            RecipeSnapshot snapshot;
            try { snapshot = RecipeSnapshotBuilder.Deserialize(revision.SnapshotJson); }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            { throw new InvalidDataException("Catering reference data contains an invalid recipe snapshot.", exception); }
            snapshots.Add(revision.Id, snapshot);
            IEnumerable<Guid> ingredients = snapshot.IngredientPositions.Select(value => value.Ingredient.IngredientRevisionId)
                .Concat(snapshot.IngredientPositions.SelectMany(value => value.Replacements)
                    .Select(value => value.Ingredient.IngredientRevisionId));
            IEnumerable<Guid> subrecipes = snapshot.SubrecipePositions.Select(value => value.RecipeRevisionId)
                .Concat(snapshot.SubrecipePositions.SelectMany(value => value.Replacements)
                    .Select(value => value.RecipeRevisionId));
            if (ingredients.Any(value => !ingredientRevisionIds.Contains(value)) ||
                subrecipes.Any(value => !recipeRevisionIds.Contains(value)))
                throw new InvalidDataException("Catering reference data is not transitively complete.");
        }

        var reachableRecipeRevisions = new HashSet<Guid>();
        var referencedIngredients = new HashSet<Guid>();
        var pending = new Queue<Guid>(payload.Entries.Select(value => value.RevisionId));
        while (pending.TryDequeue(out Guid revisionId))
        {
            if (!reachableRecipeRevisions.Add(revisionId)) continue;
            RecipeSnapshot snapshot = snapshots[revisionId];
            foreach (Guid ingredientId in snapshot.IngredientPositions
                         .Select(value => value.Ingredient.IngredientRevisionId)
                         .Concat(snapshot.IngredientPositions.SelectMany(value => value.Replacements)
                             .Select(value => value.Ingredient.IngredientRevisionId)))
                referencedIngredients.Add(ingredientId);
            foreach (Guid dependencyId in snapshot.SubrecipePositions.Select(value => value.RecipeRevisionId)
                         .Concat(snapshot.SubrecipePositions.SelectMany(value => value.Replacements)
                             .Select(value => value.RecipeRevisionId)))
                pending.Enqueue(dependencyId);
        }
        HashSet<Guid> referencedIngredientIdentities = payload.IngredientRevisions
            .Where(value => referencedIngredients.Contains(value.Id)).Select(value => value.IngredientId).ToHashSet();
        HashSet<Guid> allowedIngredientRevisions = referencedIngredients.Concat(payload.Ingredients
                .Where(value => referencedIngredientIdentities.Contains(value.Id) && value.CurrentPublishedRevisionId.HasValue)
                .Select(value => value.CurrentPublishedRevisionId!.Value)).ToHashSet();
        if (!reachableRecipeRevisions.SetEquals(recipeRevisionIds) ||
            !payload.RecipeRevisions.Select(value => value.RecipeId).ToHashSet().SetEquals(recipeIds) ||
            !referencedIngredientIdentities.SetEquals(ingredientIds) ||
            !allowedIngredientRevisions.SetEquals(ingredientRevisionIds))
            throw new InvalidDataException("Catering reference data contains records outside the dependency closure.");
    }

    public static IReadOnlySet<Guid> ReadRecipeRevisionIds(JsonElement json) =>
        Read(json).RecipeRevisions.Select(value => value.Id).ToHashSet();

    public async Task<JsonElement> ExportAsync(Guid campId, CancellationToken cancellationToken = default)
    {
        CampRecipeEntryRecord[] entries = await database.Set<CampRecipeEntryRecord>().AsNoTracking()
            .Where(value => value.CampId == campId)
            .OrderBy(value => value.Id).ToArrayAsync(cancellationToken);
        var entryData = new List<CampEntryData>(entries.Length);
        foreach (CampRecipeEntryRecord entry in entries)
        {
            Guid revisionId = entry.UpstreamRecipeRevisionId ?? await database.Set<RecipeRevisionRecord>()
                .AsNoTracking().Where(value => value.RecipeId == entry.CampRecipeId)
                .OrderByDescending(value => value.RevisionNumber).Select(value => value.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (revisionId == Guid.Empty)
                throw new InvalidOperationException("A camp-local library recipe has no published revision.");
            entryData.Add(new CampEntryData(entry.Id, entry.CampId, revisionId,
                entry.CreatedBy, entry.CreatedAtUtc, entry.UpdatedBy, entry.UpdatedAtUtc));
        }

        var revisionIds = new HashSet<Guid>(entryData.Select(value => value.RevisionId));
        var pending = new Queue<Guid>(revisionIds);
        var revisions = new List<RecipeRevisionRecord>();
        while (pending.TryDequeue(out Guid revisionId))
        {
            RecipeRevisionRecord revision = await database.Set<RecipeRevisionRecord>().AsNoTracking()
                .SingleAsync(value => value.Id == revisionId, cancellationToken);
            revisions.Add(revision);
            RecipeSnapshot snapshot = RecipeSnapshotBuilder.Deserialize(revision.SnapshotJson);
            foreach (Guid dependencyId in snapshot.SubrecipePositions
                         .Select(value => value.RecipeRevisionId)
                         .Concat(snapshot.SubrecipePositions.SelectMany(value => value.Replacements)
                             .Select(value => value.RecipeRevisionId)))
                if (revisionIds.Add(dependencyId)) pending.Enqueue(dependencyId);
        }

        Guid[] recipeIds = revisions.Select(value => value.RecipeId).Distinct().ToArray();
        RecipeRecord[] recipes = await database.Set<RecipeRecord>().AsNoTracking()
            .Where(value => recipeIds.Contains(value.Id)).ToArrayAsync(cancellationToken);
        Guid[] referencedIngredientRevisionIds = revisions.Select(value => RecipeSnapshotBuilder.Deserialize(value.SnapshotJson))
            .SelectMany(snapshot => snapshot.IngredientPositions.Select(value => value.Ingredient.IngredientRevisionId)
                .Concat(snapshot.IngredientPositions.SelectMany(value => value.Replacements)
                    .Select(value => value.Ingredient.IngredientRevisionId)))
            .Distinct().ToArray();
        IngredientRevisionRecord[] referencedIngredientRevisions = await database.Set<IngredientRevisionRecord>().AsNoTracking()
            .Where(value => referencedIngredientRevisionIds.Contains(value.Id)).ToArrayAsync(cancellationToken);
        if (referencedIngredientRevisions.Length != referencedIngredientRevisionIds.Length)
            throw new InvalidOperationException("A recipe references an ingredient revision that does not exist.");
        Guid[] ingredientIds = referencedIngredientRevisions.Select(value => value.IngredientId).Distinct().ToArray();
        IngredientIdentityRecord[] ingredients = await database.Set<IngredientIdentityRecord>().AsNoTracking()
            .Where(value => ingredientIds.Contains(value.Id)).ToArrayAsync(cancellationToken);
        Guid[] ingredientRevisionIds = referencedIngredientRevisionIds
            .Concat(ingredients.Where(value => value.CurrentPublishedRevisionId.HasValue)
                .Select(value => value.CurrentPublishedRevisionId!.Value)).Distinct().ToArray();
        IngredientRevisionRecord[] ingredientRevisions = await database.Set<IngredientRevisionRecord>().AsNoTracking()
            .Where(value => ingredientRevisionIds.Contains(value.Id)).ToArrayAsync(cancellationToken);
        if (ingredientRevisions.Length != ingredientRevisionIds.Length)
            throw new InvalidOperationException("An ingredient identity references a current revision that does not exist.");
        IngredientVariantRevisionRecord[] variants = await database.Set<IngredientVariantRevisionRecord>().AsNoTracking()
            .Where(value => ingredientRevisionIds.Contains(value.IngredientRevisionId)).ToArrayAsync(cancellationToken);
        Guid[] variantIds = variants.Select(value => value.Id).ToArray();

        var payload = new Payload(
            SchemaVersion,
            entryData, recipes.Select(Map).ToArray(), revisions.Select(Map).ToArray(),
            (await database.MeasurementUnits.AsNoTracking().ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientCategoryRecord>().AsNoTracking().ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientAllergenDefinitionRecord>().AsNoTracking().ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientIntoleranceDefinitionRecord>().AsNoTracking().ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientOriginPropertyRecord>().AsNoTracking().ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            ingredients.Select(Map).ToArray(),
            ingredientRevisions.Select(Map).ToArray(),
            (await database.Set<IngredientRevisionNutritionProfileRecord>().AsNoTracking().Where(value => ingredientRevisionIds.Contains(value.IngredientRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientRevisionAllergenRecord>().AsNoTracking().Where(value => ingredientRevisionIds.Contains(value.IngredientRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientRevisionIntoleranceRecord>().AsNoTracking().Where(value => ingredientRevisionIds.Contains(value.IngredientRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientRevisionOriginRecord>().AsNoTracking().Where(value => ingredientRevisionIds.Contains(value.IngredientRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientRevisionUnitConversionRecord>().AsNoTracking().Where(value => ingredientRevisionIds.Contains(value.IngredientRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientRevisionSubstanceContentRecord>().AsNoTracking().Where(value => ingredientRevisionIds.Contains(value.IngredientRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            variants.Select(Map).ToArray(),
            (await database.Set<IngredientVariantNutritionProfileRecord>().AsNoTracking().Where(value => variantIds.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientVariantAllergenOverrideRecord>().AsNoTracking().Where(value => variantIds.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientVariantIntoleranceOverrideRecord>().AsNoTracking().Where(value => variantIds.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientVariantOriginOverrideRecord>().AsNoTracking().Where(value => variantIds.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientVariantUnitConversionOverrideRecord>().AsNoTracking().Where(value => variantIds.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray(),
            (await database.Set<IngredientVariantSubstanceContentOverrideRecord>().AsNoTracking().Where(value => variantIds.Contains(value.VariantRevisionId)).ToArrayAsync(cancellationToken)).Select(Map).ToArray());
        return JsonSerializer.SerializeToElement(payload, JsonOptions);
    }

    public async Task ImportAsync(JsonElement json, Guid campId, CancellationToken cancellationToken = default)
    {
        Validate(json, campId);
        Payload payload = Read(json);

        await AddMissingAsync(payload.Units, value => value.Id, value => new MeasurementUnit(value.Id, value.Name, value.Symbol, (MeasurementDimension)value.Dimension, value.BaseUnitFactor), value => value.Id, cancellationToken);
        await AddMissingAsync(payload.Categories, value => value.Id, value => new IngredientCategoryRecord { Id = value.Id, ParentCategoryId = value.ParentId, Code = value.Code, Name = value.Name, NormalizedName = value.NormalizedName, Status = value.Status }, value => value.Id, cancellationToken);
        await AddMissingAsync(payload.Allergens, value => value.Id, value => new IngredientAllergenDefinitionRecord { Id = value.Id, ParentAllergenId = value.ParentId, Code = value.Code, Name = value.Name, IsEuMajorAllergen = value.Flag, Status = value.Status }, value => value.Id, cancellationToken);
        await AddMissingAsync(payload.Intolerances, value => value.Id, value => new IngredientIntoleranceDefinitionRecord { Id = value.Id, Code = value.Code, Name = value.Name, IsQuantityDependent = value.Flag, Status = value.Status }, value => value.Id, cancellationToken);
        await AddMissingAsync(payload.Origins, value => value.Id, value => new IngredientOriginPropertyRecord { Id = value.Id, Code = value.Code, Name = value.Name, IsAnimalOrigin = value.Flag, Status = value.Status }, value => value.Id, cancellationToken);

        Guid[] existingIngredientIds = await database.Set<IngredientIdentityRecord>().Where(value => payload.Ingredients.Select(item => item.Id).Contains(value.Id)).Select(value => value.Id).ToArrayAsync(cancellationToken);
        foreach (IngredientIdentityData value in payload.Ingredients.Where(value => !existingIngredientIds.Contains(value.Id)))
            database.Add(new IngredientIdentityRecord { Id = value.Id, ScopeType = value.ScopeType, ScopeId = value.ScopeId, Status = value.Status });
        await database.SaveChangesAsync(cancellationToken);
        Guid[] existingRevisionIds = await database.Set<IngredientRevisionRecord>().Where(value => payload.IngredientRevisions.Select(item => item.Id).Contains(value.Id)).Select(value => value.Id).ToArrayAsync(cancellationToken);
        HashSet<Guid> newRevisionIds = payload.IngredientRevisions.Select(value => value.Id).Except(existingRevisionIds).ToHashSet();
        database.AddRange(payload.IngredientRevisions.Where(value => newRevisionIds.Contains(value.Id)).Select(Map));
        await database.SaveChangesAsync(cancellationToken);
        foreach (IngredientIdentityData value in payload.Ingredients.Where(value => value.CurrentPublishedRevisionId.HasValue))
        {
            IngredientIdentityRecord record = await database.Set<IngredientIdentityRecord>().SingleAsync(item => item.Id == value.Id, cancellationToken);
            if (record.CurrentPublishedRevisionId is null) record.CurrentPublishedRevisionId = value.CurrentPublishedRevisionId;
        }
        database.AddRange(payload.RevisionNutrition.Where(value => newRevisionIds.Contains(value.OwnerId)).Select(MapRevisionNutrition));
        database.AddRange(payload.RevisionAllergens.Where(value => newRevisionIds.Contains(value.OwnerId)).Select(value => new IngredientRevisionAllergenRecord { IngredientRevisionId = value.OwnerId, AllergenId = value.DefinitionId, State = value.State, Source = value.Source }));
        database.AddRange(payload.RevisionIntolerances.Where(value => newRevisionIds.Contains(value.OwnerId)).Select(value => new IngredientRevisionIntoleranceRecord { IngredientRevisionId = value.OwnerId, IntoleranceId = value.DefinitionId, State = value.State, Source = value.Source }));
        database.AddRange(payload.RevisionOrigins.Where(value => newRevisionIds.Contains(value.OwnerId)).Select(value => new IngredientRevisionOriginRecord { IngredientRevisionId = value.OwnerId, OriginPropertyId = value.DefinitionId, State = value.State, Source = value.Source }));
        database.AddRange(payload.RevisionConversions.Where(value => newRevisionIds.Contains(value.OwnerId)).Select(value => new IngredientRevisionUnitConversionRecord { IngredientRevisionId = value.OwnerId, SourceUnitId = value.DefinitionId, FactorToBaseUnit = value.Factor, Precision = value.State }));
        database.AddRange(payload.RevisionSubstanceContents.Where(value => newRevisionIds.Contains(value.OwnerId)).Select(MapRevisionSubstanceContent));
        HashSet<Guid> newVariantIds = payload.Variants.Where(value => newRevisionIds.Contains(value.IngredientRevisionId)).Select(value => value.Id).ToHashSet();
        database.AddRange(payload.Variants.Where(value => newVariantIds.Contains(value.Id)).Select(Map));
        database.AddRange(payload.VariantNutrition.Where(value => newVariantIds.Contains(value.OwnerId)).Select(MapVariantNutrition));
        database.AddRange(payload.VariantAllergens.Where(value => newVariantIds.Contains(value.OwnerId)).Select(value => new IngredientVariantAllergenOverrideRecord { VariantRevisionId = value.OwnerId, AllergenId = value.DefinitionId, State = value.State, Source = value.Source }));
        database.AddRange(payload.VariantIntolerances.Where(value => newVariantIds.Contains(value.OwnerId)).Select(value => new IngredientVariantIntoleranceOverrideRecord { VariantRevisionId = value.OwnerId, IntoleranceId = value.DefinitionId, State = value.State, Source = value.Source }));
        database.AddRange(payload.VariantOrigins.Where(value => newVariantIds.Contains(value.OwnerId)).Select(value => new IngredientVariantOriginOverrideRecord { VariantRevisionId = value.OwnerId, OriginPropertyId = value.DefinitionId, State = value.State, Source = value.Source }));
        database.AddRange(payload.VariantConversions.Where(value => newVariantIds.Contains(value.OwnerId)).Select(value => new IngredientVariantUnitConversionOverrideRecord { VariantRevisionId = value.OwnerId, SourceUnitId = value.DefinitionId, FactorToBaseUnit = value.Factor, Precision = value.State }));
        database.AddRange(payload.VariantSubstanceContents.Where(value => newVariantIds.Contains(value.OwnerId)).Select(MapVariantSubstanceContent));
        await AddMissingAsync(payload.Recipes, value => value.Id, Map, value => value.Id, cancellationToken);
        await AddMissingAsync(payload.RecipeRevisions, value => value.Id, Map, value => value.Id, cancellationToken);
        await AddMissingAsync(payload.Entries, value => value.Id, Map, value => value.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static Payload Read(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Catering reference data is missing.");
        if (json.TryGetProperty("schemaVersion", out JsonElement schema) && schema.GetInt32() == 1)
        {
            LegacyPayload legacy = json.Deserialize<LegacyPayload>(JsonOptions)
                ?? throw new InvalidDataException("Catering reference data is empty.");
            if (typeof(LegacyPayload).GetProperties().Where(property => property.PropertyType != typeof(int))
                .Any(property => property.GetValue(legacy) is null))
                throw new InvalidDataException("Catering reference data is incomplete.");
            return new Payload(SchemaVersion, legacy.Entries, legacy.Recipes, legacy.RecipeRevisions,
                legacy.Units, legacy.Categories, legacy.Allergens, legacy.Intolerances, legacy.Origins,
                legacy.Ingredients, legacy.IngredientRevisions, legacy.RevisionNutrition,
                legacy.RevisionAllergens, legacy.RevisionIntolerances, legacy.RevisionOrigins,
                legacy.RevisionConversions, [], legacy.Variants, legacy.VariantNutrition,
                legacy.VariantAllergens, legacy.VariantIntolerances, legacy.VariantOrigins,
                legacy.VariantConversions, []);
        }
        Payload payload = json.Deserialize<Payload>(JsonOptions)
            ?? throw new InvalidDataException("Catering reference data is empty.");
        if (typeof(Payload).GetProperties().Where(property => property.PropertyType != typeof(int))
            .Any(property => property.GetValue(payload) is null))
            throw new InvalidDataException("Catering reference data is incomplete.");
        return payload;
    }

    private static bool HasDuplicate(IEnumerable<Guid> values)
    {
        var seen = new HashSet<Guid>();
        return values.Any(value => value == Guid.Empty || !seen.Add(value));
    }

    private async Task AddMissingAsync<TData, TEntity, TKey>(IReadOnlyList<TData> values, Func<TData, TKey> key, Func<TData, TEntity> map, Func<TEntity, TKey> entityKey, CancellationToken token) where TEntity : class where TKey : notnull
    {
        if (values.Count == 0) return;
        HashSet<TKey> desired = values.Select(key).ToHashSet();
        HashSet<TKey> existing = (await database.Set<TEntity>().AsNoTracking().ToArrayAsync(token)).Select(entityKey).Where(desired.Contains).ToHashSet();
        database.AddRange(values.Where(value => !existing.Contains(key(value))).Select(map));
    }

    private static CampEntryData Map(CampRecipeEntryRecord x) => new(x.Id, x.CampId, x.UpstreamRecipeRevisionId!.Value, x.CreatedBy, x.CreatedAtUtc, x.UpdatedBy, x.UpdatedAtUtc);
    private static CampRecipeEntryRecord Map(CampEntryData x) => new() { Id = x.Id, CampId = x.CampId, UpstreamRecipeRevisionId = x.RevisionId, CreatedBy = x.CreatedBy, CreatedAtUtc = x.CreatedAtUtc, UpdatedBy = x.UpdatedBy, UpdatedAtUtc = x.UpdatedAtUtc };
    private static RecipeData Map(RecipeRecord x) => new(x.Id, x.ScopeType, x.ScopeId, x.Name, x.NormalizedName, x.Status, x.RecipeType, x.Description, x.Source, x.InternalNotes, x.ReferenceServings, x.AuthoringStageId, x.AuthoringStageName, x.AuthoringStageFactor, x.ReferenceQuantity, x.ReferenceUnitId, x.DefaultAgeGroupScalingApplies, x.DraftVersion, x.CreatedBy, x.CreatedAtUtc, x.UpdatedBy, x.UpdatedAtUtc);
    private static RecipeRecord Map(RecipeData x) => new() { Id=x.Id, ScopeType=x.ScopeType, ScopeId=x.ScopeId, Name=x.Name, NormalizedName=x.NormalizedName, Status=x.Status, RecipeType=x.RecipeType, Description=x.Description, Source=x.Source, InternalNotes=x.InternalNotes, ReferenceServings=x.ReferenceServings, AuthoringStageId=x.AuthoringStageId, AuthoringStageName=x.AuthoringStageName, AuthoringStageFactor=x.AuthoringStageFactor, ReferenceQuantity=x.ReferenceQuantity, ReferenceUnitId=x.ReferenceUnitId, DefaultAgeGroupScalingApplies=x.DefaultAgeGroupScalingApplies, DraftVersion=x.DraftVersion, CreatedBy=x.CreatedBy, CreatedAtUtc=x.CreatedAtUtc, UpdatedBy=x.UpdatedBy, UpdatedAtUtc=x.UpdatedAtUtc };
    private static RecipeRevisionData Map(RecipeRevisionRecord x) => new(x.Id,x.RecipeId,x.RevisionNumber,x.PublishedAtUtc,x.PublishedBy,x.ChangeNote,x.SnapshotSchemaVersion,x.SnapshotJson);
    private static RecipeRevisionRecord Map(RecipeRevisionData x) => new() { Id=x.Id, RecipeId=x.RecipeId, RevisionNumber=x.RevisionNumber, PublishedAtUtc=x.PublishedAtUtc, PublishedBy=x.PublishedBy, ChangeNote=x.ChangeNote, SnapshotSchemaVersion=x.SnapshotSchemaVersion, SnapshotJson=x.SnapshotJson };
    private static UnitData Map(MeasurementUnit x) => new(x.Id,x.Name,x.Symbol,(int)x.Dimension,x.BaseUnitFactor);
    private static MasterData Map(IngredientCategoryRecord x) => new(x.Id,x.ParentCategoryId,x.Code,x.Name,x.NormalizedName,x.Status,false);
    private static MasterData Map(IngredientAllergenDefinitionRecord x) => new(x.Id,x.ParentAllergenId,x.Code,x.Name,x.Name.Trim().ToUpperInvariant(),x.Status,x.IsEuMajorAllergen);
    private static MasterData Map(IngredientIntoleranceDefinitionRecord x) => new(x.Id,null,x.Code,x.Name,x.Name.Trim().ToUpperInvariant(),x.Status,x.IsQuantityDependent);
    private static MasterData Map(IngredientOriginPropertyRecord x) => new(x.Id,null,x.Code,x.Name,x.Name.Trim().ToUpperInvariant(),x.Status,x.IsAnimalOrigin);
    private static IngredientIdentityData Map(IngredientIdentityRecord x) => new(x.Id,x.ScopeType,x.ScopeId,x.CurrentPublishedRevisionId,x.Status);
    private static IngredientRevisionData Map(IngredientRevisionRecord x) => new(x.Id,x.IngredientId,x.RevisionNumber,x.State,x.Name,x.NormalizedName,x.CategoryId,x.BaseUnitId,x.AllergenReviewState,x.IntoleranceReviewState,x.OriginReviewState,x.RowVersion,x.CreatedAtUtc,x.CreatedBy,x.UpdatedAtUtc,x.UpdatedBy,x.PublishedAtUtc,x.PublishedBy,x.SourceSummary);
    private static IngredientRevisionRecord Map(IngredientRevisionData x) => new() { Id=x.Id,IngredientId=x.IngredientId,RevisionNumber=x.RevisionNumber,State=x.State,Name=x.Name,NormalizedName=x.NormalizedName,CategoryId=x.CategoryId,BaseUnitId=x.BaseUnitId,AllergenReviewState=x.AllergenReviewState,IntoleranceReviewState=x.IntoleranceReviewState,OriginReviewState=x.OriginReviewState,SourceSummary=x.SourceSummary,RowVersion=x.RowVersion,CreatedAtUtc=x.CreatedAtUtc,CreatedBy=x.CreatedBy,UpdatedAtUtc=x.UpdatedAtUtc,UpdatedBy=x.UpdatedBy,PublishedAtUtc=x.PublishedAtUtc,PublishedBy=x.PublishedBy };
    private static PropertyData Map(IngredientRevisionAllergenRecord x)=>new(x.IngredientRevisionId,x.AllergenId,x.State,x.Source,0);
    private static PropertyData Map(IngredientRevisionIntoleranceRecord x)=>new(x.IngredientRevisionId,x.IntoleranceId,x.State,x.Source,0);
    private static PropertyData Map(IngredientRevisionOriginRecord x)=>new(x.IngredientRevisionId,x.OriginPropertyId,x.State,x.Source,0);
    private static PropertyData Map(IngredientRevisionUnitConversionRecord x)=>new(x.IngredientRevisionId,x.SourceUnitId,x.Precision,0,x.FactorToBaseUnit);
    private static SubstanceContentData Map(IngredientRevisionSubstanceContentRecord x)=>new(x.IngredientRevisionId,x.SubstanceId,x.Amount,x.AmountUnitId,x.ReferenceQuantity,x.ReferenceUnitId,x.SourceType,x.SourceReference,x.ReviewState);
    private static VariantData Map(IngredientVariantRevisionRecord x)=>new(x.Id,x.IngredientRevisionId,x.VariantKey,x.Name,x.NormalizedName,x.Status,x.SortOrder);
    private static IngredientVariantRevisionRecord Map(VariantData x)=>new(){Id=x.Id,IngredientRevisionId=x.IngredientRevisionId,VariantKey=x.Key,Name=x.Name,NormalizedName=x.NormalizedName,Status=x.Status,SortOrder=x.SortOrder};
    private static PropertyData Map(IngredientVariantAllergenOverrideRecord x)=>new(x.VariantRevisionId,x.AllergenId,x.State,x.Source,0);
    private static PropertyData Map(IngredientVariantIntoleranceOverrideRecord x)=>new(x.VariantRevisionId,x.IntoleranceId,x.State,x.Source,0);
    private static PropertyData Map(IngredientVariantOriginOverrideRecord x)=>new(x.VariantRevisionId,x.OriginPropertyId,x.State,x.Source,0);
    private static PropertyData Map(IngredientVariantUnitConversionOverrideRecord x)=>new(x.VariantRevisionId,x.SourceUnitId,x.Precision,0,x.FactorToBaseUnit);
    private static SubstanceContentData Map(IngredientVariantSubstanceContentOverrideRecord x)=>new(x.VariantRevisionId,x.SubstanceId,x.Amount,x.AmountUnitId,x.ReferenceQuantity,x.ReferenceUnitId,x.SourceType,x.SourceReference,x.ReviewState);
    private static NutritionData Map(IngredientRevisionNutritionProfileRecord x)=>new(x.IngredientRevisionId,x.ReferenceQuantity,x.ReferenceUnitId,x.EnergyKilojoules,x.FatGrams,x.SaturatedFatGrams,x.CarbohydrateGrams,x.SugarsGrams,x.ProteinGrams,x.SaltGrams,x.FiberGrams,x.SourceType,x.SourceReference,x.ReviewState,x.ReferenceDate);
    private static NutritionData Map(IngredientVariantNutritionProfileRecord x)=>new(x.VariantRevisionId,x.ReferenceQuantity,x.ReferenceUnitId,x.EnergyKilojoules,x.FatGrams,x.SaturatedFatGrams,x.CarbohydrateGrams,x.SugarsGrams,x.ProteinGrams,x.SaltGrams,x.FiberGrams,x.SourceType,x.SourceReference,x.ReviewState,x.ReferenceDate);
    private static IngredientRevisionNutritionProfileRecord MapRevisionNutrition(NutritionData x)=>new(){IngredientRevisionId=x.OwnerId,ReferenceQuantity=x.ReferenceQuantity,ReferenceUnitId=x.ReferenceUnitId,EnergyKilojoules=x.EnergyKilojoules,FatGrams=x.FatGrams,SaturatedFatGrams=x.SaturatedFatGrams,CarbohydrateGrams=x.CarbohydrateGrams,SugarsGrams=x.SugarsGrams,ProteinGrams=x.ProteinGrams,SaltGrams=x.SaltGrams,FiberGrams=x.FiberGrams,SourceType=x.SourceType,SourceReference=x.SourceReference,ReviewState=x.ReviewState,ReferenceDate=x.ReferenceDate};
    private static IngredientVariantNutritionProfileRecord MapVariantNutrition(NutritionData x)=>new(){VariantRevisionId=x.OwnerId,ReferenceQuantity=x.ReferenceQuantity,ReferenceUnitId=x.ReferenceUnitId,EnergyKilojoules=x.EnergyKilojoules,FatGrams=x.FatGrams,SaturatedFatGrams=x.SaturatedFatGrams,CarbohydrateGrams=x.CarbohydrateGrams,SugarsGrams=x.SugarsGrams,ProteinGrams=x.ProteinGrams,SaltGrams=x.SaltGrams,FiberGrams=x.FiberGrams,SourceType=x.SourceType,SourceReference=x.SourceReference,ReviewState=x.ReviewState,ReferenceDate=x.ReferenceDate};
    private static IngredientRevisionSubstanceContentRecord MapRevisionSubstanceContent(SubstanceContentData x)=>new(){IngredientRevisionId=x.OwnerId,SubstanceId=x.SubstanceId,Amount=x.Amount,AmountUnitId=x.AmountUnitId,ReferenceQuantity=x.ReferenceQuantity,ReferenceUnitId=x.ReferenceUnitId,SourceType=x.SourceType,SourceReference=x.SourceReference,ReviewState=x.ReviewState};
    private static IngredientVariantSubstanceContentOverrideRecord MapVariantSubstanceContent(SubstanceContentData x)=>new(){VariantRevisionId=x.OwnerId,SubstanceId=x.SubstanceId,Amount=x.Amount,AmountUnitId=x.AmountUnitId,ReferenceQuantity=x.ReferenceQuantity,ReferenceUnitId=x.ReferenceUnitId,SourceType=x.SourceType,SourceReference=x.SourceReference,ReviewState=x.ReviewState};

    private sealed record Payload(int SchemaVersion,IReadOnlyList<CampEntryData> Entries,IReadOnlyList<RecipeData> Recipes,IReadOnlyList<RecipeRevisionData> RecipeRevisions,IReadOnlyList<UnitData> Units,IReadOnlyList<MasterData> Categories,IReadOnlyList<MasterData> Allergens,IReadOnlyList<MasterData> Intolerances,IReadOnlyList<MasterData> Origins,IReadOnlyList<IngredientIdentityData> Ingredients,IReadOnlyList<IngredientRevisionData> IngredientRevisions,IReadOnlyList<NutritionData> RevisionNutrition,IReadOnlyList<PropertyData> RevisionAllergens,IReadOnlyList<PropertyData> RevisionIntolerances,IReadOnlyList<PropertyData> RevisionOrigins,IReadOnlyList<PropertyData> RevisionConversions,IReadOnlyList<SubstanceContentData> RevisionSubstanceContents,IReadOnlyList<VariantData> Variants,IReadOnlyList<NutritionData> VariantNutrition,IReadOnlyList<PropertyData> VariantAllergens,IReadOnlyList<PropertyData> VariantIntolerances,IReadOnlyList<PropertyData> VariantOrigins,IReadOnlyList<PropertyData> VariantConversions,IReadOnlyList<SubstanceContentData> VariantSubstanceContents);
    private sealed record LegacyPayload(int SchemaVersion,IReadOnlyList<CampEntryData> Entries,IReadOnlyList<RecipeData> Recipes,IReadOnlyList<RecipeRevisionData> RecipeRevisions,IReadOnlyList<UnitData> Units,IReadOnlyList<MasterData> Categories,IReadOnlyList<MasterData> Allergens,IReadOnlyList<MasterData> Intolerances,IReadOnlyList<MasterData> Origins,IReadOnlyList<IngredientIdentityData> Ingredients,IReadOnlyList<IngredientRevisionData> IngredientRevisions,IReadOnlyList<NutritionData> RevisionNutrition,IReadOnlyList<PropertyData> RevisionAllergens,IReadOnlyList<PropertyData> RevisionIntolerances,IReadOnlyList<PropertyData> RevisionOrigins,IReadOnlyList<PropertyData> RevisionConversions,IReadOnlyList<VariantData> Variants,IReadOnlyList<NutritionData> VariantNutrition,IReadOnlyList<PropertyData> VariantAllergens,IReadOnlyList<PropertyData> VariantIntolerances,IReadOnlyList<PropertyData> VariantOrigins,IReadOnlyList<PropertyData> VariantConversions);
    private sealed record CampEntryData(Guid Id,Guid CampId,Guid RevisionId,Guid CreatedBy,DateTimeOffset CreatedAtUtc,Guid UpdatedBy,DateTimeOffset UpdatedAtUtc);
    private sealed record RecipeData(Guid Id,int ScopeType,Guid? ScopeId,string Name,string NormalizedName,int Status,int RecipeType,string? Description,string? Source,string? InternalNotes,decimal? ReferenceServings,Guid? AuthoringStageId,string? AuthoringStageName,decimal? AuthoringStageFactor,decimal? ReferenceQuantity,Guid? ReferenceUnitId,bool? DefaultAgeGroupScalingApplies,long DraftVersion,Guid CreatedBy,DateTimeOffset CreatedAtUtc,Guid UpdatedBy,DateTimeOffset UpdatedAtUtc);
    private sealed record RecipeRevisionData(Guid Id,Guid RecipeId,int RevisionNumber,DateTimeOffset PublishedAtUtc,Guid PublishedBy,string? ChangeNote,int SnapshotSchemaVersion,string SnapshotJson);
    private sealed record UnitData(Guid Id,string Name,string Symbol,int Dimension,decimal BaseUnitFactor);
    private sealed record MasterData(Guid Id,Guid? ParentId,string Code,string Name,string NormalizedName,int Status,bool Flag);
    private sealed record IngredientIdentityData(Guid Id,int ScopeType,Guid? ScopeId,Guid? CurrentPublishedRevisionId,int Status);
    private sealed record IngredientRevisionData(Guid Id,Guid IngredientId,int RevisionNumber,int State,string Name,string NormalizedName,Guid CategoryId,Guid BaseUnitId,int AllergenReviewState,int IntoleranceReviewState,int OriginReviewState,long RowVersion,DateTimeOffset CreatedAtUtc,Guid CreatedBy,DateTimeOffset UpdatedAtUtc,Guid UpdatedBy,DateTimeOffset? PublishedAtUtc,Guid? PublishedBy,string SourceSummary = "");
    private sealed record NutritionData(Guid OwnerId,decimal ReferenceQuantity,Guid ReferenceUnitId,decimal? EnergyKilojoules,decimal? FatGrams,decimal? SaturatedFatGrams,decimal? CarbohydrateGrams,decimal? SugarsGrams,decimal? ProteinGrams,decimal? SaltGrams,decimal? FiberGrams,int SourceType,string SourceReference,int ReviewState,DateOnly? ReferenceDate);
    private sealed record PropertyData(Guid OwnerId,Guid DefinitionId,int State,int Source,decimal Factor);
    private sealed record SubstanceContentData(Guid OwnerId,Guid SubstanceId,decimal Amount,Guid AmountUnitId,decimal ReferenceQuantity,Guid ReferenceUnitId,int SourceType,string SourceReference,int ReviewState);
    private sealed record VariantData(Guid Id,Guid IngredientRevisionId,string Key,string Name,string NormalizedName,int Status,int SortOrder);
}
