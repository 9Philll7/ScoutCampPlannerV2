namespace ScoutCampPlanner.Catering.Domain;

public sealed class IngredientRevisionPublicationValidator
{
    private readonly IReadOnlyDictionary<Guid, IngredientPropertyDefinition> allergensById;

    public IngredientRevisionPublicationValidator(IEnumerable<IngredientPropertyDefinition> allergens)
    {
        ArgumentNullException.ThrowIfNull(allergens);
        IngredientPropertyDefinition[] definitions = allergens.ToArray();
        if (definitions.Select(value => value.Id).Distinct().Count() != definitions.Length)
            throw new ArgumentException("Allergen IDs must be unique.", nameof(allergens));
        if (definitions.Select(value => value.Code).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
            throw new ArgumentException("Allergen codes must be unique.", nameof(allergens));
        allergensById = definitions.ToDictionary(value => value.Id);
        ValidateHierarchy();
    }

    public void Validate(IngredientRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.State != IngredientRevisionState.Draft)
            throw new InvalidOperationException("Only a draft can be validated for publication.");
        if (revision.AllergenReviewState != IngredientPropertyReviewState.Reviewed ||
            revision.IntoleranceReviewState != IngredientPropertyReviewState.Reviewed ||
            revision.OriginReviewState != IngredientPropertyReviewState.Reviewed)
            throw new InvalidOperationException("All ingredient property groups must be reviewed before publication.");

        ValidateAllergenAssignments(revision.Allergens, "ingredient revision");
        foreach (IngredientVariantRevision variant in revision.Variants)
        {
            IngredientPropertyValue[] effective = revision.Allergens
                .Where(value => variant.AllergenOverrides.All(overridden => overridden.PropertyId != value.PropertyId))
                .Concat(variant.AllergenOverrides)
                .ToArray();
            ValidateAllergenAssignments(effective, $"variant '{variant.VariantKey}'");
        }
    }

    private void ValidateAllergenAssignments(
        IEnumerable<IngredientPropertyValue> assignments,
        string subject)
    {
        IReadOnlyDictionary<Guid, IngredientPropertyValue> byId = assignments.ToDictionary(value => value.PropertyId);
        foreach (IngredientPropertyValue child in byId.Values)
        {
            if (child.State is not (IngredientPropertyState.Contains or IngredientPropertyState.MayContain))
                continue;

            Guid? parentId = allergensById.TryGetValue(child.PropertyId, out IngredientPropertyDefinition? definition)
                ? definition.ParentId
                : null;
            while (parentId.HasValue)
            {
                if (byId.TryGetValue(parentId.Value, out IngredientPropertyValue? parent) &&
                    parent.State == IngredientPropertyState.DoesNotContain)
                    throw new InvalidOperationException(
                        $"Allergen assignments for {subject} contradict their hierarchy.");
                parentId = allergensById[parentId.Value].ParentId;
            }
        }
    }

    private void ValidateHierarchy()
    {
        foreach (IngredientPropertyDefinition definition in allergensById.Values)
        {
            var visited = new HashSet<Guid> { definition.Id };
            Guid? parentId = definition.ParentId;
            while (parentId.HasValue)
            {
                if (!allergensById.TryGetValue(parentId.Value, out IngredientPropertyDefinition? parent))
                    throw new ArgumentException($"Allergen '{definition.Code}' references an unknown parent.");
                if (!visited.Add(parent.Id))
                    throw new ArgumentException($"Allergen hierarchy contains a cycle at '{definition.Code}'.");
                parentId = parent.ParentId;
            }
        }
    }
}

