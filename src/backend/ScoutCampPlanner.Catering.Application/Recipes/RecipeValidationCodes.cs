namespace ScoutCampPlanner.Catering.Application.Recipes;

public static class RecipeValidationCodes
{
    public const string NameMissing = "recipe.name.missing";
    public const string ReferenceServingsInvalid = "recipe.reference.servings.invalid";
    public const string ReferenceQuantityInvalid = "recipe.reference.quantity.invalid";
    public const string ReferenceUnitInvalid = "recipe.reference.unit.invalid";
    public const string CentralAuthoringStageForbidden = "recipe.authoring-stage.central.forbidden";
    public const string PositionsEmpty = "recipe.positions.empty";
    public const string GroupNameMissing = "recipe.group.name.missing";
    public const string GroupEmpty = "recipe.group.empty";
    public const string SortOrderInvalid = "recipe.position.sort-order.invalid";
    public const string IngredientMissing = "recipe.ingredient.missing";
    public const string IngredientScopeForbidden = "recipe.ingredient.scope.forbidden";
    public const string IngredientQuantityInvalid = "recipe.ingredient.quantity.invalid";
    public const string IngredientUnitInvalid = "recipe.ingredient.unit.invalid";
    public const string IngredientDuplicate = "recipe.ingredient.duplicate";
    public const string ScalingModeInvalid = "recipe.ingredient.scaling-mode.invalid";
    public const string AgeGroupScalingInvalid = "recipe.ingredient.age-scaling.invalid";
    public const string StepwiseScalingInvalid = "recipe.ingredient.stepwise.invalid";
    public const string IngredientReplacementMissing = "recipe.ingredient-replacement.ingredient.missing";
    public const string IngredientReplacementQuantityInvalid = "recipe.ingredient-replacement.quantity.invalid";
    public const string IngredientReplacementUnitInvalid = "recipe.ingredient-replacement.unit.invalid";
    public const string ReplacementConflictsEmpty = "recipe.replacement.conflicts.empty";
    public const string ReplacementConflictDuplicate = "recipe.replacement.conflict.duplicate";
    public const string SubrecipeRevisionInvalid = "recipe.subrecipe.revision.invalid";
    public const string SubrecipeDemandInvalid = "recipe.subrecipe.demand.invalid";
    public const string SubrecipeDuplicate = "recipe.subrecipe.duplicate";
    public const string SubrecipeCycle = "recipe.subrecipe.cycle";
    public const string ReplacementRecipeInvalid = "recipe.recipe-replacement.revision.invalid";
    public const string ReplacementRecipeTypeMismatch = "recipe.recipe-replacement.type.mismatch";
    public const string ReplacementRecipeDemandInvalid = "recipe.recipe-replacement.demand.invalid";
    public const string DescriptionMissing = "recipe.description.missing";
    public const string SourceMissing = "recipe.source.missing";
    public const string ArchivedRevisionReferenced = "recipe.subrecipe.archived";
    public const string ConflictUnresolved = "recipe.conflict.unresolved";
    public const string ReplacementConflictRemains = "recipe.replacement.conflict.remains";
    public const string ReplacementCreatesConflict = "recipe.replacement.conflict.created";
}
