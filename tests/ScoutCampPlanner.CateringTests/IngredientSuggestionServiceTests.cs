using Microsoft.Extensions.Options;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientSuggestionServiceTests
{
    [Fact]
    public async Task BlsProvider_SearchesGeneratedIndexAndReturnsMappedValues()
    {
        string path = Path.Combine(Path.GetTempPath(), $"scp-bls-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, """
                {
                  "schemaVersion": "1.0",
                  "entries": [{
                    "code": "M010000",
                    "name": "Kuhmilch vollfett",
                    "nutrition": {
                      "energyKilojoules": 268,
                      "fatGrams": 3.5,
                      "saturatedFatGrams": 2.3,
                      "carbohydrateGrams": 4.8,
                      "sugarsGrams": 4.8,
                      "proteinGrams": 3.3,
                      "saltGrams": 0.1,
                      "fiberGrams": 0
                    },
                    "substances": { "LACTOSE": 4.8 }
                  }]
                }
                """, TestContext.Current.CancellationToken);
            var provider = new BlsIngredientSuggestionProvider(
                Options.Create(new BlsIngredientSuggestionOptions { BlsIndexPath = path }));

            IngredientSuggestionSearchResult result = await provider.SearchAsync(
                "milch", 20, TestContext.Current.CancellationToken);

            Assert.True(result.IsAvailable);
            IngredientDataSuggestion suggestion = Assert.Single(result.Suggestions);
            Assert.Equal("M010000", suggestion.SourceKey);
            Assert.Equal(100m, suggestion.ReferenceQuantity);
            Assert.Equal("g", suggestion.ReferenceUnitSymbol);
            Assert.Equal(268m, suggestion.Nutrition.EnergyKilojoules);
            Assert.Equal(4.8m, Assert.Single(suggestion.SubstanceContents).AmountGrams);
            Assert.Contains("BLS", suggestion.SourceSummary);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Search_RejectsUnknownProviderWithoutCallingOtherProviders()
    {
        var service = new IngredientSuggestionService([]);

        IngredientSuggestionSearchResult result = await service.SearchAsync(
            "OFF", "Milch", TestContext.Current.CancellationToken);

        Assert.False(result.IsAvailable);
        Assert.Equal("suggestion_provider_unknown", result.UnavailableReason);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task Search_IgnoresQueriesShorterThanTwoCharacters()
    {
        var provider = new RecordingProvider();
        var service = new IngredientSuggestionService([provider]);

        IngredientSuggestionSearchResult result = await service.SearchAsync(
            "BLS", " x ", TestContext.Current.CancellationToken);

        Assert.True(result.IsAvailable);
        Assert.Empty(result.Suggestions);
        Assert.False(provider.WasCalled);
    }

    private sealed class RecordingProvider : IIngredientSuggestionProvider
    {
        public string Provider => "BLS";
        public bool WasCalled { get; private set; }

        public Task<IngredientSuggestionSearchResult> SearchAsync(
            string query,
            int maximumResults,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new IngredientSuggestionSearchResult(true, null, []));
        }
    }
}
