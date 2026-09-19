using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ScoutCampPlanner.Catering.Application.Ingredients;

namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

public sealed class BlsIngredientSuggestionOptions
{
    public string BlsIndexPath { get; set; } = "reference-data/bls-4.0-suggestions.json";
}

public sealed class BlsIngredientSuggestionProvider(
    IOptions<BlsIngredientSuggestionOptions> options) : IIngredientSuggestionProvider
{
    private readonly SemaphoreSlim loadLock = new(1, 1);
    private IReadOnlyList<IndexedSuggestion>? suggestions;
    private string? loadError;

    public string Provider => "BLS";

    public async Task<IngredientSuggestionSearchResult> SearchAsync(
        string query,
        int maximumResults,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        if (suggestions is null)
            return new(false, loadError ?? "bls_index_unavailable", []);

        string[] terms = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        IngredientDataSuggestion[] matches = suggestions
            .Where(value => terms.All(term => value.NormalizedSearch.Contains(term, StringComparison.Ordinal)))
            .OrderBy(value => Score(value, terms))
            .ThenBy(value => value.Value.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(Math.Clamp(maximumResults, 1, 50))
            .Select(value => value.Value)
            .ToArray();
        return new(true, null, matches);
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (suggestions is not null || loadError is not null) return;
        await loadLock.WaitAsync(cancellationToken);
        try
        {
            if (suggestions is not null || loadError is not null) return;
            string configured = options.Value.BlsIndexPath?.Trim() ?? string.Empty;
            string path = Path.GetFullPath(configured);
            if (!File.Exists(path))
            {
                loadError = "bls_index_not_configured";
                return;
            }

            await using FileStream stream = File.OpenRead(path);
            BlsSuggestionIndex? index = await JsonSerializer.DeserializeAsync<BlsSuggestionIndex>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
            if (index is null || index.SchemaVersion != "1.0" || index.Entries.Count == 0)
            {
                loadError = "bls_index_invalid";
                return;
            }

            suggestions = index.Entries.Select(ToSuggestion).ToArray();
        }
        catch (JsonException)
        {
            loadError = "bls_index_invalid";
        }
        catch (IOException)
        {
            loadError = "bls_index_unavailable";
        }
        catch (UnauthorizedAccessException)
        {
            loadError = "bls_index_unavailable";
        }
        finally
        {
            loadLock.Release();
        }
    }

    private static IndexedSuggestion ToSuggestion(BlsSuggestionEntry entry)
    {
        IngredientSuggestionSubstance[] substances = entry.Substances
            .Where(value => value.Value >= 0)
            .Select(value => new IngredientSuggestionSubstance(value.Key, value.Value))
            .ToArray();
        var value = new IngredientDataSuggestion(
            "BLS", entry.Code, entry.Name, 100m, "g",
            $"Bundeslebensmittelschlüssel (BLS) 4.0, Max Rubner-Institut, Code {entry.Code} (Schätzung)",
            new IngredientSuggestionNutrition(entry.Nutrition.EnergyKilojoules, entry.Nutrition.FatGrams,
                entry.Nutrition.SaturatedFatGrams, entry.Nutrition.CarbohydrateGrams,
                entry.Nutrition.SugarsGrams, entry.Nutrition.ProteinGrams,
                entry.Nutrition.SaltGrams, entry.Nutrition.FiberGrams),
            substances);
        return new(value, Normalize($"{entry.Code} {entry.Name}"));
    }

    private static int Score(IndexedSuggestion value, string[] terms)
    {
        string name = Normalize(value.Value.Name);
        if (name == string.Join(' ', terms)) return 0;
        if (name.StartsWith(string.Join(' ', terms), StringComparison.Ordinal)) return 1;
        return 2;
    }

    private static string Normalize(string value)
    {
        string decomposed = value.Trim().Replace("ß", "ss", StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        bool previousSpace = false;
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousSpace = false;
            }
            else if (!previousSpace && builder.Length > 0)
            {
                builder.Append(' ');
                previousSpace = true;
            }
        }
        return builder.ToString().Trim();
    }

    private sealed record IndexedSuggestion(IngredientDataSuggestion Value, string NormalizedSearch);
    private sealed record BlsSuggestionIndex(string SchemaVersion, IReadOnlyList<BlsSuggestionEntry> Entries);
    private sealed record BlsSuggestionEntry(
        string Code,
        string Name,
        BlsSuggestionNutrition Nutrition,
        IReadOnlyDictionary<string, decimal> Substances);
    private sealed record BlsSuggestionNutrition(
        decimal? EnergyKilojoules,
        decimal? FatGrams,
        decimal? SaturatedFatGrams,
        decimal? CarbohydrateGrams,
        decimal? SugarsGrams,
        decimal? ProteinGrams,
        decimal? SaltGrams,
        decimal? FiberGrams);
}
