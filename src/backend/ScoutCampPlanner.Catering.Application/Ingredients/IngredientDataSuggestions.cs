namespace ScoutCampPlanner.Catering.Application.Ingredients;

public sealed record IngredientSuggestionNutrition(
    decimal? EnergyKilojoules,
    decimal? FatGrams,
    decimal? SaturatedFatGrams,
    decimal? CarbohydrateGrams,
    decimal? SugarsGrams,
    decimal? ProteinGrams,
    decimal? SaltGrams,
    decimal? FiberGrams);

public sealed record IngredientSuggestionSubstance(string Code, decimal AmountGrams);

public sealed record IngredientDataSuggestion(
    string Provider,
    string SourceKey,
    string Name,
    decimal ReferenceQuantity,
    string ReferenceUnitSymbol,
    string SourceSummary,
    IngredientSuggestionNutrition Nutrition,
    IReadOnlyList<IngredientSuggestionSubstance> SubstanceContents);

public sealed record IngredientSuggestionSearchResult(
    bool IsAvailable,
    string? UnavailableReason,
    IReadOnlyList<IngredientDataSuggestion> Suggestions);

public interface IIngredientSuggestionProvider
{
    string Provider { get; }

    Task<IngredientSuggestionSearchResult> SearchAsync(
        string query,
        int maximumResults,
        CancellationToken cancellationToken = default);
}

public sealed class IngredientSuggestionService(IEnumerable<IIngredientSuggestionProvider> providers)
{
    public async Task<IngredientSuggestionSearchResult> SearchAsync(
        string provider,
        string query,
        CancellationToken cancellationToken = default)
    {
        string normalizedProvider = provider?.Trim() ?? string.Empty;
        string normalizedQuery = string.Join(' ', (query ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalizedQuery.Length is < 2 or > 100)
            return new(true, null, []);

        IIngredientSuggestionProvider? selected = providers.SingleOrDefault(value =>
            value.Provider.Equals(normalizedProvider, StringComparison.OrdinalIgnoreCase));
        return selected is null
            ? new(false, "suggestion_provider_unknown", [])
            : await selected.SearchAsync(normalizedQuery, 20, cancellationToken);
    }
}
