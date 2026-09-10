using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public sealed record RecipeRevisionDescriptor(
    Guid RevisionId,
    Guid RecipeId,
    RecipeType RecipeType,
    RecipeStatus RecipeStatus,
    Guid? ReferenceUnitId,
    IReadOnlySet<ConflictReference> Conflicts);

public sealed record IngredientDescriptor(Guid IngredientRevisionId, IngredientScopeType ScopeType, Guid? ScopeId);

public sealed record RecipeValidationContext(Guid? TenantId = null);

public interface IRecipeValidationReferences
{
    IngredientDescriptor? FindIngredient(Guid ingredientRevisionId);
    bool IsUnitAvailableForIngredient(Guid ingredientRevisionId, Guid unitId);
    IReadOnlySet<ConflictReference> GetIngredientConflicts(Guid ingredientRevisionId);
    bool UnitExists(Guid unitId);
    bool AreUnitsCompatible(Guid sourceUnitId, Guid targetUnitId);
    RecipeRevisionDescriptor? FindRevision(Guid revisionId);
    bool WouldCreateCycle(Guid recipeId, Guid referencedRecipeId);
}
