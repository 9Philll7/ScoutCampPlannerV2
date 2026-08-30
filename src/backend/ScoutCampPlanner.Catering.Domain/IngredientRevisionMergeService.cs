namespace ScoutCampPlanner.Catering.Domain;

public sealed record IngredientMergeConflict(string Path);

public sealed record IngredientMergeResult(
    IngredientRevision? Draft,
    IReadOnlyList<IngredientMergeConflict> Conflicts)
{
    public bool IsSuccess => Draft is not null && Conflicts.Count == 0;
}

public sealed class IngredientRevisionMergeService
{
    public bool HasCentralUpdate(
        IngredientIdentity localIngredient,
        IngredientRevision lastConsideredCentralRevision,
        IngredientRevision currentCentralRevision)
    {
        ValidateLineage(localIngredient, lastConsideredCentralRevision, currentCentralRevision);
        return currentCentralRevision.RevisionNumber > lastConsideredCentralRevision.RevisionNumber;
    }

    public IngredientMergeResult MergeIntoNewDraft(
        IngredientIdentity localIngredient,
        IngredientRevision baseRevision,
        IngredientRevision localRevision,
        IngredientRevision remoteRevision,
        Guid newDraftId,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(localIngredient);
        ArgumentNullException.ThrowIfNull(localRevision);
        ValidateLineage(localIngredient, baseRevision, remoteRevision);
        if (localRevision.IngredientId != localIngredient.Id)
            throw new ArgumentException("Local revision does not belong to the local ingredient.", nameof(localRevision));
        if (localRevision.State != IngredientRevisionState.Published)
            throw new InvalidOperationException("A central update can currently be merged only from a published local revision.");
        if (remoteRevision.RevisionNumber <= baseRevision.RevisionNumber)
            throw new ArgumentException("Remote revision must be newer than the merge base.", nameof(remoteRevision));

        var conflicts = new List<IngredientMergeConflict>();
        string name = MergeValue("name", baseRevision.Name, localRevision.Name, remoteRevision.Name, conflicts);
        Guid categoryId = MergeValue("category", baseRevision.CategoryId, localRevision.CategoryId, remoteRevision.CategoryId, conflicts);
        Guid baseUnitId = MergeValue("base_unit", baseRevision.BaseUnitId, localRevision.BaseUnitId, remoteRevision.BaseUnitId, conflicts);
        IngredientPropertyReviewState allergenReview = MergeValue(
            "allergens.review_state", baseRevision.AllergenReviewState, localRevision.AllergenReviewState,
            remoteRevision.AllergenReviewState, conflicts);
        IngredientPropertyReviewState intoleranceReview = MergeValue(
            "intolerances.review_state", baseRevision.IntoleranceReviewState, localRevision.IntoleranceReviewState,
            remoteRevision.IntoleranceReviewState, conflicts);
        IngredientPropertyReviewState originReview = MergeValue(
            "origins.review_state", baseRevision.OriginReviewState, localRevision.OriginReviewState,
            remoteRevision.OriginReviewState, conflicts);

        IReadOnlyCollection<IngredientPropertyValue> allergens = MergeEntries(
            "allergens", baseRevision.Allergens, localRevision.Allergens, remoteRevision.Allergens,
            value => value.PropertyId, EqualityComparer<IngredientPropertyValue>.Default.Equals, conflicts);
        IReadOnlyCollection<IngredientPropertyValue> intolerances = MergeEntries(
            "intolerances", baseRevision.Intolerances, localRevision.Intolerances, remoteRevision.Intolerances,
            value => value.PropertyId, EqualityComparer<IngredientPropertyValue>.Default.Equals, conflicts);
        IReadOnlyCollection<IngredientPropertyValue> origins = MergeEntries(
            "origins", baseRevision.Origins, localRevision.Origins, remoteRevision.Origins,
            value => value.PropertyId, EqualityComparer<IngredientPropertyValue>.Default.Equals, conflicts);
        IReadOnlyCollection<IngredientRevisionUnitConversion> conversions = MergeEntries(
            "unit_conversions", baseRevision.UnitConversions, localRevision.UnitConversions, remoteRevision.UnitConversions,
            value => value.SourceUnitId, EqualityComparer<IngredientRevisionUnitConversion>.Default.Equals, conflicts);
        IReadOnlyCollection<IngredientVariantRevision> variants = MergeEntries(
            "variants", baseRevision.Variants, localRevision.Variants, remoteRevision.Variants,
            value => value.VariantKey, VariantEquals, conflicts);

        if (conflicts.Count > 0)
            return new IngredientMergeResult(null, conflicts);

        IngredientRevision draft = localIngredient.CreateDraft(
            newDraftId,
            name,
            categoryId,
            baseUnitId,
            createdBy,
            createdAt,
            localRevision.Id);
        draft.ReplaceRevisionDetailsForMerge(
            allergens,
            intolerances,
            origins,
            conversions,
            variants,
            allergenReview,
            intoleranceReview,
            originReview,
            remoteRevision.Id);
        return new IngredientMergeResult(draft, []);
    }

    private static T MergeValue<T>(
        string path,
        T baseValue,
        T localValue,
        T remoteValue,
        ICollection<IngredientMergeConflict> conflicts)
    {
        var comparer = EqualityComparer<T>.Default;
        if (comparer.Equals(localValue, remoteValue) || comparer.Equals(remoteValue, baseValue))
            return localValue;
        if (comparer.Equals(localValue, baseValue))
            return remoteValue;
        conflicts.Add(new IngredientMergeConflict(path));
        return localValue;
    }

    private static IReadOnlyCollection<TValue> MergeEntries<TKey, TValue>(
        string path,
        IEnumerable<TValue> baseValues,
        IEnumerable<TValue> localValues,
        IEnumerable<TValue> remoteValues,
        Func<TValue, TKey> keySelector,
        Func<TValue, TValue, bool> equals,
        ICollection<IngredientMergeConflict> conflicts)
        where TKey : notnull
    {
        Dictionary<TKey, TValue> baseByKey = baseValues.ToDictionary(keySelector);
        Dictionary<TKey, TValue> localByKey = localValues.ToDictionary(keySelector);
        Dictionary<TKey, TValue> remoteByKey = remoteValues.ToDictionary(keySelector);
        var merged = new List<TValue>();

        foreach (TKey key in baseByKey.Keys.Concat(localByKey.Keys).Concat(remoteByKey.Keys).Distinct())
        {
            bool hasBase = baseByKey.TryGetValue(key, out TValue? baseValue);
            bool hasLocal = localByKey.TryGetValue(key, out TValue? localValue);
            bool hasRemote = remoteByKey.TryGetValue(key, out TValue? remoteValue);
            bool localChanged = !OptionalEquals(hasBase, baseValue, hasLocal, localValue, equals);
            bool remoteChanged = !OptionalEquals(hasBase, baseValue, hasRemote, remoteValue, equals);

            if (localChanged && remoteChanged &&
                !OptionalEquals(hasLocal, localValue, hasRemote, remoteValue, equals))
            {
                conflicts.Add(new IngredientMergeConflict($"{path}.{key}"));
                continue;
            }

            if (remoteChanged)
            {
                if (hasRemote)
                    merged.Add(remoteValue!);
            }
            else if (hasLocal)
            {
                merged.Add(localValue!);
            }
        }

        return merged;
    }

    private static bool OptionalEquals<T>(
        bool hasLeft,
        T? left,
        bool hasRight,
        T? right,
        Func<T, T, bool> equals) =>
        hasLeft == hasRight && (!hasLeft || equals(left!, right!));

    private static bool VariantEquals(IngredientVariantRevision left, IngredientVariantRevision right) =>
        left.VariantKey == right.VariantKey &&
        left.Name == right.Name &&
        PropertySetsEqual(left.AllergenOverrides, right.AllergenOverrides) &&
        PropertySetsEqual(left.IntoleranceOverrides, right.IntoleranceOverrides) &&
        PropertySetsEqual(left.OriginOverrides, right.OriginOverrides) &&
        ConversionSetsEqual(left.UnitConversionOverrides, right.UnitConversionOverrides);

    private static bool PropertySetsEqual(
        IEnumerable<IngredientPropertyValue> left,
        IEnumerable<IngredientPropertyValue> right) =>
        left.OrderBy(value => value.PropertyId).SequenceEqual(right.OrderBy(value => value.PropertyId));

    private static bool ConversionSetsEqual(
        IEnumerable<IngredientRevisionUnitConversion> left,
        IEnumerable<IngredientRevisionUnitConversion> right) =>
        left.OrderBy(value => value.SourceUnitId).SequenceEqual(right.OrderBy(value => value.SourceUnitId));

    private static void ValidateLineage(
        IngredientIdentity localIngredient,
        IngredientRevision baseRevision,
        IngredientRevision remoteRevision)
    {
        ArgumentNullException.ThrowIfNull(localIngredient);
        ArgumentNullException.ThrowIfNull(baseRevision);
        ArgumentNullException.ThrowIfNull(remoteRevision);
        if (localIngredient.ScopeType == IngredientScopeType.Central || !localIngredient.SourceIngredientId.HasValue)
            throw new ArgumentException("Only a local fork can receive central updates.", nameof(localIngredient));
        if (baseRevision.IngredientId != localIngredient.SourceIngredientId ||
            remoteRevision.IngredientId != localIngredient.SourceIngredientId)
            throw new ArgumentException("Base and remote revisions must belong to the fork's central source.");
        if (baseRevision.State != IngredientRevisionState.Published ||
            remoteRevision.State != IngredientRevisionState.Published)
            throw new ArgumentException("Base and remote revisions must be published.");
        Guid expectedBaseId = localIngredient.Revisions
            .Where(value => value.State == IngredientRevisionState.Published)
            .OrderByDescending(value => value.RevisionNumber)
            .Select(value => value.MergedCentralRevisionId)
            .FirstOrDefault(value => value.HasValue)
            ?? localIngredient.SourceRevisionId!.Value;
        if (baseRevision.Id != expectedBaseId)
            throw new ArgumentException("Base revision is not the last central revision considered by the fork.", nameof(baseRevision));
    }
}
