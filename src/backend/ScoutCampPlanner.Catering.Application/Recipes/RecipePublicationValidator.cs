using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public sealed class RecipePublicationValidator(IRecipeValidationReferences references)
{
    public RecipeValidationResult Validate(RecipeDraft draft, RecipeValidationContext? validationContext = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var issues = new List<RecipeValidationIssue>();

        ValidateIdentityAndReference(draft, issues);
        ValidateStructure(draft, issues);
        ValidateIngredientPositions(draft, validationContext ?? new RecipeValidationContext(), issues);
        ValidateSubrecipePositions(draft, issues);
        AddMetadataWarnings(draft, issues);

        return new RecipeValidationResult(issues);
    }

    private void ValidateIdentityAndReference(RecipeDraft draft, List<RecipeValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(draft.Name)) AddError(issues, RecipeValidationCodes.NameMissing);

        if (draft.RecipeType == RecipeType.PortionBased)
        {
            if (draft.ReferenceServings is null or <= 0)
                AddError(issues, RecipeValidationCodes.ReferenceServingsInvalid);
            if (draft.ScopeType == RecipeScopeType.Central && draft.AuthoringStage is not null)
                AddError(issues, RecipeValidationCodes.CentralAuthoringStageForbidden);
        }
        else
        {
            if (draft.ReferenceQuantity is null or <= 0)
                AddError(issues, RecipeValidationCodes.ReferenceQuantityInvalid);
            if (!draft.ReferenceUnitId.HasValue || !references.UnitExists(draft.ReferenceUnitId.Value))
                AddError(issues, RecipeValidationCodes.ReferenceUnitInvalid);
        }
    }

    private static void ValidateStructure(RecipeDraft draft, List<RecipeValidationIssue> issues)
    {
        if (draft.IngredientPositions.Count + draft.SubrecipePositions.Count == 0)
            AddError(issues, RecipeValidationCodes.PositionsEmpty);

        foreach (RecipeIngredientGroup group in draft.Groups)
        {
            var context = Context("groupId", group.Id);
            if (string.IsNullOrWhiteSpace(group.Name)) AddError(issues, RecipeValidationCodes.GroupNameMissing, context);
            bool hasPosition = draft.IngredientPositions.Any(value => value.GroupId == group.Id) ||
                               draft.SubrecipePositions.Any(value => value.GroupId == group.Id);
            if (!hasPosition) AddError(issues, RecipeValidationCodes.GroupEmpty, context);
        }

        var positions = draft.IngredientPositions
            .Select(value => (value.GroupId, value.SortOrder, value.Id))
            .Concat(draft.SubrecipePositions.Select(value => (value.GroupId, value.SortOrder, value.Id)))
            .ToArray();
        foreach (var position in positions.Where(value => value.SortOrder < 0))
            AddError(issues, RecipeValidationCodes.SortOrderInvalid, Context("positionId", position.Id));
        foreach (var duplicate in positions.GroupBy(value => (value.GroupId, value.SortOrder)).Where(value => value.Count() > 1))
            foreach (var position in duplicate)
                AddError(issues, RecipeValidationCodes.SortOrderInvalid, Context("positionId", position.Id));
    }

    private void ValidateIngredientPositions(
        RecipeDraft draft,
        RecipeValidationContext validationContext,
        List<RecipeValidationIssue> issues)
    {
        foreach (RecipeIngredientPosition position in draft.IngredientPositions)
        {
            var context = Context("positionId", position.Id);
            IngredientDescriptor? ingredient = position.BaseIngredientId.HasValue
                ? references.FindIngredient(position.BaseIngredientId.Value)
                : null;
            if (ingredient is null)
                AddError(issues, RecipeValidationCodes.IngredientMissing, context);
            else if (!CanReferenceIngredient(draft, validationContext, ingredient))
                AddError(issues, RecipeValidationCodes.IngredientScopeForbidden, context);
            if (position.Quantity is null or <= 0)
                AddError(issues, RecipeValidationCodes.IngredientQuantityInvalid, context);
            if (!position.UnitId.HasValue || !position.BaseIngredientId.HasValue ||
                !references.IsUnitAvailableForIngredient(position.BaseIngredientId.Value, position.UnitId.Value))
                AddError(issues, RecipeValidationCodes.IngredientUnitInvalid, context);
            if (!Enum.IsDefined(position.ScalingMode))
                AddError(issues, RecipeValidationCodes.ScalingModeInvalid, context);
            if (!Enum.IsDefined(position.AgeGroupScaling))
                AddError(issues, RecipeValidationCodes.AgeGroupScalingInvalid, context);
            bool validStepwise = position.ScalingMode == ScalingMode.Stepwise
                ? position.StepwiseScaling is { StepSize: > 0, QuantityPerStep: > 0 }
                : position.StepwiseScaling is null;
            if (!validStepwise) AddError(issues, RecipeValidationCodes.StepwiseScalingInvalid, context);

            ValidateIngredientReplacementRules(draft, validationContext, position, issues);
        }

        foreach (var duplicate in draft.IngredientPositions
                     .Where(value => value.BaseIngredientId.HasValue)
                     .GroupBy(value => (value.GroupId, value.BaseIngredientId))
                     .Where(value => value.Count() > 1))
            foreach (RecipeIngredientPosition position in duplicate)
                AddError(issues, RecipeValidationCodes.IngredientDuplicate, Context("positionId", position.Id));
    }

    private void ValidateIngredientReplacementRules(
        RecipeDraft draft,
        RecipeValidationContext validationContext,
        RecipeIngredientPosition position,
        List<RecipeValidationIssue> issues)
    {
        foreach (IngredientReplacementRule rule in position.ReplacementRules)
        {
            var context = Context("replacementId", rule.Id);
            IngredientDescriptor? replacementIngredient = rule.ReplacementBaseIngredientId.HasValue
                ? references.FindIngredient(rule.ReplacementBaseIngredientId.Value)
                : null;
            if (replacementIngredient is null)
                AddError(issues, RecipeValidationCodes.IngredientReplacementMissing, context);
            else if (!CanReferenceIngredient(draft, validationContext, replacementIngredient))
                AddError(issues, RecipeValidationCodes.IngredientScopeForbidden, context);
            if (rule.ReplacementQuantity is null or <= 0)
                AddError(issues, RecipeValidationCodes.IngredientReplacementQuantityInvalid, context);
            if (!rule.ReplacementUnitId.HasValue || !rule.ReplacementBaseIngredientId.HasValue ||
                !references.IsUnitAvailableForIngredient(
                    rule.ReplacementBaseIngredientId.Value, rule.ReplacementUnitId.Value))
                AddError(issues, RecipeValidationCodes.IngredientReplacementUnitInvalid, context);
            if (rule.Conflicts.Count == 0)
                AddError(issues, RecipeValidationCodes.ReplacementConflictsEmpty, context);
        }

        AddDuplicateConflictErrors(position.ReplacementRules.Select(value => (value.Id, value.Conflicts)), issues);
        if (position.BaseIngredientId.HasValue)
        {
            IReadOnlySet<ConflictReference> originalConflicts =
                references.GetIngredientConflicts(position.BaseIngredientId.Value);
            AddUnresolvedConflictWarnings(
                originalConflicts,
                position.ReplacementRules.SelectMany(value => value.Conflicts),
                Context("positionId", position.Id),
                issues);
            foreach (IngredientReplacementRule rule in position.ReplacementRules.Where(value => value.ReplacementBaseIngredientId.HasValue))
                AddReplacementConflictWarnings(
                    rule.Conflicts,
                    originalConflicts,
                    references.GetIngredientConflicts(rule.ReplacementBaseIngredientId!.Value),
                    Context("replacementId", rule.Id),
                    issues);
        }
    }

    private static bool CanReferenceIngredient(
        RecipeDraft draft,
        RecipeValidationContext context,
        IngredientDescriptor ingredient) =>
        ingredient.ScopeType switch
        {
            IngredientScopeType.Central => true,
            IngredientScopeType.Tenant => draft.ScopeType switch
            {
                RecipeScopeType.Tenant => draft.ScopeId == ingredient.ScopeId,
                RecipeScopeType.Camp => context.TenantId == ingredient.ScopeId,
                _ => false,
            },
            IngredientScopeType.Camp => draft.ScopeType == RecipeScopeType.Camp && draft.ScopeId == ingredient.ScopeId,
            _ => false,
        };

    private void ValidateSubrecipePositions(RecipeDraft draft, List<RecipeValidationIssue> issues)
    {
        foreach (RecipeSubrecipePosition position in draft.SubrecipePositions)
        {
            RecipeRevisionDescriptor? descriptor = position.RecipeRevisionId.HasValue
                ? references.FindRevision(position.RecipeRevisionId.Value)
                : null;
            var context = Context("positionId", position.Id);
            if (descriptor is null)
            {
                AddError(issues, RecipeValidationCodes.SubrecipeRevisionInvalid, context);
            }
            else
            {
                ValidateDemand(
                    descriptor, position.RequiredServings, position.RequiredQuantity, position.RequiredUnitId,
                    RecipeValidationCodes.SubrecipeDemandInvalid, context, issues);
                if (references.WouldCreateCycle(draft.Id, descriptor.RecipeId))
                    AddError(issues, RecipeValidationCodes.SubrecipeCycle, context);
                if (descriptor.RecipeStatus == RecipeStatus.Archived)
                    AddWarning(issues, RecipeValidationCodes.ArchivedRevisionReferenced, context);
            }

            ValidateRecipeReplacementRules(draft.Id, position, descriptor, issues);
        }

        foreach (var duplicate in draft.SubrecipePositions
                     .Where(value => value.RecipeRevisionId.HasValue)
                     .GroupBy(value => (value.GroupId, value.RecipeRevisionId))
                     .Where(value => value.Count() > 1))
            foreach (RecipeSubrecipePosition position in duplicate)
                AddError(issues, RecipeValidationCodes.SubrecipeDuplicate, Context("positionId", position.Id));
    }

    private void ValidateRecipeReplacementRules(
        Guid recipeId,
        RecipeSubrecipePosition position,
        RecipeRevisionDescriptor? original,
        List<RecipeValidationIssue> issues)
    {
        foreach (RecipeReplacementRule rule in position.ReplacementRules)
        {
            RecipeRevisionDescriptor? replacement = rule.ReplacementRecipeRevisionId.HasValue
                ? references.FindRevision(rule.ReplacementRecipeRevisionId.Value)
                : null;
            var context = Context("replacementId", rule.Id);
            if (replacement is null)
            {
                AddError(issues, RecipeValidationCodes.ReplacementRecipeInvalid, context);
            }
            else
            {
                if (original is not null && replacement.RecipeType != original.RecipeType)
                    AddError(issues, RecipeValidationCodes.ReplacementRecipeTypeMismatch, context);
                ValidateDemand(
                    replacement, rule.ReplacementServings, rule.ReplacementQuantity, rule.ReplacementUnitId,
                    RecipeValidationCodes.ReplacementRecipeDemandInvalid, context, issues);
                if (references.WouldCreateCycle(recipeId, replacement.RecipeId))
                    AddError(issues, RecipeValidationCodes.SubrecipeCycle, context);
                if (replacement.RecipeStatus == RecipeStatus.Archived)
                    AddWarning(issues, RecipeValidationCodes.ArchivedRevisionReferenced, context);
            }
            if (rule.Conflicts.Count == 0)
                AddError(issues, RecipeValidationCodes.ReplacementConflictsEmpty, context);
        }

        AddDuplicateConflictErrors(position.ReplacementRules.Select(value => (value.Id, value.Conflicts)), issues);
        if (original is not null)
        {
            AddUnresolvedConflictWarnings(
                original.Conflicts,
                position.ReplacementRules.SelectMany(value => value.Conflicts),
                Context("positionId", position.Id),
                issues);
            foreach (RecipeReplacementRule rule in position.ReplacementRules)
            {
                RecipeRevisionDescriptor? replacement = rule.ReplacementRecipeRevisionId.HasValue
                    ? references.FindRevision(rule.ReplacementRecipeRevisionId.Value)
                    : null;
                if (replacement is not null)
                    AddReplacementConflictWarnings(
                        rule.Conflicts, original.Conflicts, replacement.Conflicts,
                        Context("replacementId", rule.Id), issues);
            }
        }
    }

    private static void AddUnresolvedConflictWarnings(
        IEnumerable<ConflictReference> exposedConflicts,
        IEnumerable<ConflictReference> coveredConflicts,
        IReadOnlyDictionary<string, string> context,
        List<RecipeValidationIssue> issues)
    {
        var covered = coveredConflicts.ToHashSet();
        foreach (ConflictReference conflict in exposedConflicts.Where(value => !covered.Contains(value)))
            AddWarning(issues, RecipeValidationCodes.ConflictUnresolved, WithConflict(context, conflict));
    }

    private static void AddReplacementConflictWarnings(
        IEnumerable<ConflictReference> declaredConflicts,
        IEnumerable<ConflictReference> originalConflicts,
        IEnumerable<ConflictReference> replacementConflicts,
        IReadOnlyDictionary<string, string> context,
        List<RecipeValidationIssue> issues)
    {
        var replacement = replacementConflicts.ToHashSet();
        foreach (ConflictReference conflict in declaredConflicts.Where(replacement.Contains))
            AddWarning(issues, RecipeValidationCodes.ReplacementConflictRemains, WithConflict(context, conflict));

        var original = originalConflicts.ToHashSet();
        foreach (ConflictReference conflict in replacement.Where(value => !original.Contains(value)))
            AddWarning(issues, RecipeValidationCodes.ReplacementCreatesConflict, WithConflict(context, conflict));
    }

    private static IReadOnlyDictionary<string, string> WithConflict(
        IReadOnlyDictionary<string, string> context,
        ConflictReference conflict)
    {
        var result = new Dictionary<string, string>(context, StringComparer.Ordinal)
        {
            ["conflictType"] = conflict.Type.ToString(),
            ["conflictId"] = conflict.Id.ToString(),
        };
        return result;
    }

    private void ValidateDemand(
        RecipeRevisionDescriptor revision,
        decimal? servings,
        decimal? quantity,
        Guid? unitId,
        string code,
        IReadOnlyDictionary<string, string> context,
        List<RecipeValidationIssue> issues)
    {
        bool valid = revision.RecipeType == RecipeType.PortionBased
            ? servings is > 0 && quantity is null && unitId is null
            : quantity is > 0 && unitId.HasValue && servings is null && revision.ReferenceUnitId.HasValue &&
              references.AreUnitsCompatible(unitId.Value, revision.ReferenceUnitId.Value);
        if (!valid) AddError(issues, code, context);
    }

    private static void AddDuplicateConflictErrors(
        IEnumerable<(Guid RuleId, IReadOnlySet<ConflictReference> Conflicts)> rules,
        List<RecipeValidationIssue> issues)
    {
        foreach (var duplicate in rules.SelectMany(rule => rule.Conflicts.Select(conflict => (rule.RuleId, Conflict: conflict)))
                     .GroupBy(value => value.Conflict).Where(value => value.Count() > 1))
            foreach (var occurrence in duplicate)
                AddError(issues, RecipeValidationCodes.ReplacementConflictDuplicate,
                    Context("replacementId", occurrence.RuleId));
    }

    private static void AddMetadataWarnings(RecipeDraft draft, List<RecipeValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(draft.Description)) AddWarning(issues, RecipeValidationCodes.DescriptionMissing);
        if (string.IsNullOrWhiteSpace(draft.Source)) AddWarning(issues, RecipeValidationCodes.SourceMissing);
    }

    private static Dictionary<string, string> Context(string key, Guid value) =>
        new(StringComparer.Ordinal) { [key] = value.ToString() };

    private static void AddError(
        List<RecipeValidationIssue> issues,
        string code,
        IReadOnlyDictionary<string, string>? context = null) =>
        issues.Add(new RecipeValidationIssue(code, RecipeValidationSeverity.Error, code, context));

    private static void AddWarning(
        List<RecipeValidationIssue> issues,
        string code,
        IReadOnlyDictionary<string, string>? context = null) =>
        issues.Add(new RecipeValidationIssue(code, RecipeValidationSeverity.Warning, code, context));
}
