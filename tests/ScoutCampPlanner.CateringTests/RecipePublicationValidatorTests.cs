using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipePublicationValidatorTests
{
    [Fact]
    public void Valid_portion_recipe_has_no_publication_issues()
    {
        var references = new FakeReferences();
        Guid ingredientId = references.AddIngredient();
        Guid unitId = references.AddIngredientUnit(ingredientId);
        var draft = CompletePortionDraft();
        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, null, ingredientId, 2.5m, unitId, 0));

        RecipeValidationResult result = new RecipePublicationValidator(references).Validate(draft);

        Assert.Empty(result.Issues);
        Assert.True(result.CanPublish(warningsAcknowledged: false));
    }

    [Fact]
    public void Incomplete_draft_returns_structured_errors_and_metadata_warnings()
    {
        var draft = new RecipeDraft(Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased);

        RecipeValidationResult result = new RecipePublicationValidator(new FakeReferences()).Validate(draft);

        Assert.Contains(result.Errors, value => value.Code == RecipeValidationCodes.NameMissing);
        Assert.Contains(result.Errors, value => value.Code == RecipeValidationCodes.ReferenceServingsInvalid);
        Assert.Contains(result.Errors, value => value.Code == RecipeValidationCodes.PositionsEmpty);
        Assert.Contains(result.Warnings, value => value.Code == RecipeValidationCodes.DescriptionMissing);
        Assert.Contains(result.Warnings, value => value.Code == RecipeValidationCodes.SourceMissing);
    }

    [Fact]
    public void Duplicate_ingredient_in_same_group_is_rejected_but_different_groups_are_allowed()
    {
        var references = new FakeReferences();
        Guid ingredientId = references.AddIngredient();
        Guid unitId = references.AddIngredientUnit(ingredientId);
        var draft = CompletePortionDraft();
        var firstGroup = new RecipeIngredientGroup(Guid.NewGuid(), draft.Id, "Erste", 0);
        var secondGroup = new RecipeIngredientGroup(Guid.NewGuid(), draft.Id, "Zweite", 1);
        draft.AddGroup(firstGroup);
        draft.AddGroup(secondGroup);
        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, firstGroup.Id, ingredientId, 1m, unitId, 0));
        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, secondGroup.Id, ingredientId, 1m, unitId, 0));

        RecipeValidationResult allowed = new RecipePublicationValidator(references).Validate(draft);

        Assert.DoesNotContain(allowed.Errors, value => value.Code == RecipeValidationCodes.IngredientDuplicate);

        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, firstGroup.Id, ingredientId, 1m, unitId, 1));
        RecipeValidationResult rejected = new RecipePublicationValidator(references).Validate(draft);
        Assert.Equal(2, rejected.Errors.Count(value => value.Code == RecipeValidationCodes.IngredientDuplicate));
    }

    [Fact]
    public void Different_revisions_of_the_same_ingredient_are_duplicates_in_one_group()
    {
        var references = new FakeReferences();
        Guid ingredientId = references.AddIngredient();
        Guid newerRevisionId = references.AddIngredientRevision(ingredientId);
        Guid oldUnitId = references.AddIngredientUnit(ingredientId);
        Guid newUnitId = references.AddIngredientUnit(newerRevisionId);
        var draft = CompletePortionDraft();
        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, null, ingredientId, 1m, oldUnitId, 0));
        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, null, newerRevisionId, 1m, newUnitId, 1));

        RecipeValidationResult result = new RecipePublicationValidator(references).Validate(draft);

        Assert.Equal(2, result.Errors.Count(value => value.Code == RecipeValidationCodes.IngredientDuplicate));
    }

    [Fact]
    public void Same_conflict_on_two_replacement_rules_is_rejected()
    {
        var references = new FakeReferences();
        Guid ingredientId = references.AddIngredient();
        Guid replacementOne = references.AddIngredient();
        Guid replacementTwo = references.AddIngredient();
        Guid unitId = references.AddIngredientUnit(ingredientId);
        Guid replacementOneUnit = references.AddIngredientUnit(replacementOne);
        Guid replacementTwoUnit = references.AddIngredientUnit(replacementTwo);
        var draft = CompletePortionDraft();
        var position = new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, null, ingredientId, 1m, unitId, 0);
        var conflict = new ConflictReference(ConflictType.Allergen, Guid.NewGuid());
        position.AddReplacementRule(new IngredientReplacementRule(
            Guid.NewGuid(), position.Id, replacementOne, 1m, replacementOneUnit, [conflict]));
        position.AddReplacementRule(new IngredientReplacementRule(
            Guid.NewGuid(), position.Id, replacementTwo, 1m, replacementTwoUnit, [conflict]));
        draft.AddIngredientPosition(position);

        RecipeValidationResult result = new RecipePublicationValidator(references).Validate(draft);

        Assert.Equal(2, result.Errors.Count(value => value.Code == RecipeValidationCodes.ReplacementConflictDuplicate));
    }

    [Fact]
    public void Cyclic_archived_subrecipe_is_error_and_warning()
    {
        var references = new FakeReferences();
        Guid referencedRecipeId = Guid.NewGuid();
        Guid revisionId = references.AddRevision(referencedRecipeId, RecipeType.PortionBased, RecipeStatus.Archived);
        var draft = CompletePortionDraft();
        references.CyclicRecipeIds.Add(referencedRecipeId);
        draft.AddSubrecipePosition(new RecipeSubrecipePosition(
            Guid.NewGuid(), draft.Id, null, revisionId, 5m, null, null, 0));

        RecipeValidationResult result = new RecipePublicationValidator(references).Validate(draft);

        Assert.Contains(result.Errors, value => value.Code == RecipeValidationCodes.SubrecipeCycle);
        Assert.Contains(result.Warnings, value => value.Code == RecipeValidationCodes.ArchivedRevisionReferenced);
    }

    [Fact]
    public void Central_recipe_cannot_reference_tenant_ingredient()
    {
        var references = new FakeReferences();
        Guid ingredientId = references.AddIngredient(IngredientScopeType.Tenant, Guid.NewGuid());
        Guid unitId = references.AddIngredientUnit(ingredientId);
        var draft = CompletePortionDraft();
        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, null, ingredientId, 1m, unitId, 0));

        RecipeValidationResult result = new RecipePublicationValidator(references).Validate(draft);

        Assert.Contains(result.Errors, value => value.Code == RecipeValidationCodes.IngredientScopeForbidden);
    }

    [Fact]
    public void Replacement_that_keeps_declared_and_adds_new_conflict_produces_warnings()
    {
        var references = new FakeReferences();
        Guid originalId = references.AddIngredient();
        Guid replacementId = references.AddIngredient();
        Guid originalUnit = references.AddIngredientUnit(originalId);
        Guid replacementUnit = references.AddIngredientUnit(replacementId);
        var declared = new ConflictReference(ConflictType.Allergen, Guid.NewGuid());
        var created = new ConflictReference(ConflictType.DietaryRequirement, Guid.NewGuid());
        references.AddIngredientConflict(originalId, declared, "Milch");
        references.AddIngredientConflict(replacementId, declared, "Milch");
        references.AddIngredientConflict(replacementId, created, "Vegan");
        var draft = CompletePortionDraft();
        var position = new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, null, originalId, 1m, originalUnit, 0);
        position.AddReplacementRule(new IngredientReplacementRule(
            Guid.NewGuid(), position.Id, replacementId, 1m, replacementUnit, [declared]));
        draft.AddIngredientPosition(position);

        RecipeValidationResult result = new RecipePublicationValidator(references).Validate(draft);

        RecipeValidationIssue remains = Assert.Single(result.Warnings,
            value => value.Code == RecipeValidationCodes.ReplacementConflictRemains);
        RecipeValidationIssue createdWarning = Assert.Single(result.Warnings,
            value => value.Code == RecipeValidationCodes.ReplacementCreatesConflict);
        Assert.Equal("Milch", remains.Context["conflictName"]);
        Assert.Equal("Vegan", createdWarning.Context["conflictName"]);
    }

    private static RecipeDraft CompletePortionDraft()
    {
        var draft = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased, "Testrezept");
        draft.SetDetails("Beschreibung", "Quelle", null);
        draft.ConfigurePortionReference(10m, true);
        return draft;
    }

    private sealed class FakeReferences : IRecipeValidationReferences
    {
        private readonly Dictionary<Guid, IngredientDescriptor> ingredients = [];
        private readonly HashSet<(Guid IngredientId, Guid UnitId)> ingredientUnits = [];
        private readonly HashSet<Guid> units = [];
        private readonly Dictionary<Guid, RecipeRevisionDescriptor> revisions = [];
        private readonly Dictionary<Guid, HashSet<ConflictReference>> ingredientConflicts = [];
        private readonly Dictionary<ConflictReference, string> conflictNames = [];

        public HashSet<Guid> CyclicRecipeIds { get; } = [];

        public Guid AddIngredient(IngredientScopeType scopeType = IngredientScopeType.Central, Guid? scopeId = null)
        {
            Guid id = Guid.NewGuid();
            ingredients[id] = new IngredientDescriptor(id, id, scopeType, scopeId);
            ingredientConflicts[id] = [];
            return id;
        }

        public Guid AddIngredientRevision(
            Guid ingredientId,
            IngredientScopeType scopeType = IngredientScopeType.Central,
            Guid? scopeId = null)
        {
            Guid revisionId = Guid.NewGuid();
            ingredients[revisionId] = new IngredientDescriptor(revisionId, ingredientId, scopeType, scopeId);
            ingredientConflicts[revisionId] = [];
            return revisionId;
        }

        public void AddIngredientConflict(Guid ingredientId, ConflictReference conflict, string? name = null)
        {
            ingredientConflicts.GetValueOrDefault(ingredientId)?.Add(conflict);
            if (name is not null) conflictNames[conflict] = name;
        }

        public Guid AddIngredientUnit(Guid ingredientId)
        {
            Guid unitId = Guid.NewGuid();
            units.Add(unitId);
            ingredientUnits.Add((ingredientId, unitId));
            return unitId;
        }

        public Guid AddRevision(Guid recipeId, RecipeType type, RecipeStatus status)
        {
            Guid id = Guid.NewGuid();
            revisions[id] = new RecipeRevisionDescriptor(id, recipeId, type, status, null, new HashSet<ConflictReference>());
            return id;
        }

        public IngredientDescriptor? FindIngredient(Guid ingredientId) => ingredients.GetValueOrDefault(ingredientId);
        public bool IsUnitAvailableForIngredient(Guid ingredientId, Guid unitId) =>
            ingredientUnits.Contains((ingredientId, unitId));
        public IReadOnlySet<ConflictReference> GetIngredientConflicts(Guid ingredientId) =>
            ingredientConflicts.GetValueOrDefault(ingredientId) ?? new HashSet<ConflictReference>();
        public string? FindConflictName(ConflictReference conflict) => conflictNames.GetValueOrDefault(conflict);
        public bool UnitExists(Guid unitId) => units.Contains(unitId);
        public bool AreUnitsCompatible(Guid sourceUnitId, Guid targetUnitId) => sourceUnitId == targetUnitId;
        public RecipeRevisionDescriptor? FindRevision(Guid revisionId) =>
            revisions.GetValueOrDefault(revisionId);
        public bool WouldCreateCycle(Guid recipeId, Guid referencedRecipeId) =>
            recipeId == referencedRecipeId || CyclicRecipeIds.Contains(referencedRecipeId);
    }
}
