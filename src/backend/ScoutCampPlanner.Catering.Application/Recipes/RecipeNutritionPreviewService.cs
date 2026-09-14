using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public enum RecipeNutritionPreviewStatus
{
    Found,
    NotFound,
    Forbidden,
    NotCalculable,
}

public sealed record RecipeNutritionPreviewResult(
    RecipeNutritionPreviewStatus Status,
    RecipeNutritionCalculation? Nutrition = null);

public sealed class RecipeNutritionPreviewService(
    IRecipeEditorStore drafts,
    IRecipeEditorAuthorization authorization,
    RecipeSnapshotBuilder snapshotBuilder,
    RecipeCalculator calculator)
{
    public async Task<RecipeNutritionPreviewResult> PreviewCampAsync(
        Guid campId,
        Guid recipeId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(campId, nameof(campId));
        Required(recipeId, nameof(recipeId));
        Required(actorUserId, nameof(actorUserId));
        if (!await authorization.CanReadCampAsync(actorUserId, campId, cancellationToken))
            return new(RecipeNutritionPreviewStatus.Forbidden);

        RecipeDraft? draft = await drafts.FindAsync(recipeId, cancellationToken);
        if (draft is null || draft.ScopeType != RecipeScopeType.Camp || draft.ScopeId != campId)
            return new(RecipeNutritionPreviewStatus.NotFound);

        try
        {
            RecipeSnapshot snapshot = snapshotBuilder.Build(draft);
            if (snapshot.Reference.StandardServings is not > 0)
                return new(RecipeNutritionPreviewStatus.NotCalculable);
            decimal standardServings = snapshot.Reference.StandardServings.Value;
            decimal directDemand = snapshot.AuthoringStage?.EnteredServings ?? standardServings;
            RecipeCalculationResult calculation = calculator.Calculate(
                new RecipeCalculationRequest(draft.Id, standardServings, directDemand), snapshot);
            return new(RecipeNutritionPreviewStatus.Found, calculation.Nutrition);
        }
        catch (InvalidOperationException)
        {
            return new(RecipeNutritionPreviewStatus.NotCalculable);
        }
        catch (ArgumentException)
        {
            return new(RecipeNutritionPreviewStatus.NotCalculable);
        }
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
