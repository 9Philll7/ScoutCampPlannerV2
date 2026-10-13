using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeSnapshotCodecTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Published_recipe_remains_directly_resolvable_after_full_snapshot_roundtrip(bool useVariant)
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var database = new CateringDbContext(new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        database.Add(new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var data = await new IngredientEditorReferenceDataStore(database).GetAsync(TestContext.Current.CancellationToken);
        Guid milk = data.Allergens.Single(x => x.Code == "MILK").Id;
        Guid gram = data.Units.Single(x => x.Symbol == "g").Id;
        Guid actor = Guid.NewGuid();
        database.Add(new Allergen(milk, "TEST Milk"));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ingredients = new IngredientRevisionWorkflowStore(database);
        async Task<Guid> Ingredient(bool contains)
        {
            Guid revision = Guid.NewGuid();
            var content = IngredientRevisionDraftContent.Create("TEST " + revision, data.Categories[0].Id, gram,
                IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed,
                allergens: [new(milk, contains ? IngredientPropertyState.Contains : IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)],
                variants: contains && useVariant ? [new(Guid.NewGuid(), "free", "TEST free", true, 0,
                    allergenOverrides: [new(milk, IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)])] : []);
            Assert.Equal(IngredientRevisionMutationStatus.Created, (await ingredients.CreateDraftAsync(Guid.NewGuid(), revision,
                new(IngredientScopeType.Central, null), content, actor, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(IngredientRevisionMutationStatus.Saved, (await ingredients.SaveDraftAsync(revision, content, 1, actor, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken)).Status);
            Assert.Equal(IngredientRevisionMutationStatus.Published, (await ingredients.PublishAsync(revision, 2, actor, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken)).Status);
            return revision;
        }
        var draft = new RecipeDraft(Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased, "TEST codec");
        draft.ConfigurePortionReference(1, false);
        var position = new RecipeIngredientPosition(Guid.NewGuid(), draft.Id, null, await Ingredient(true), 10, gram, 0);
        if (!useVariant) position.AddReplacementRule(new(Guid.NewGuid(), position.Id, await Ingredient(false), 10, gram, [new(ConflictType.Allergen, milk)]));
        draft.AddIngredientPosition(position);
        var drafts = new RecipeDraftStore(database);
        await drafts.CreateAsync(draft, actor, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        var references = new EfRecipeReferences(database);
        var publication = await new RecipePublisher(database, drafts, new(references), new(references))
            .PublishAsync(draft.Id, 0, actor, DateTimeOffset.UtcNow, true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(publication.Revision);
        database.ChangeTracker.Clear();
        string json = publication.Revision.SnapshotJson;
        var decoded = RecipeSnapshotBuilder.Deserialize(json);
        Assert.Equal(json, RecipeSnapshotBuilder.Serialize(decoded));
        Assert.Equal(new(ConflictType.Allergen, milk), Assert.Single(decoded.ExposedConflicts));
        var resolver = new SupplySolutionResolver(new EfSupplySuitabilityReferences(database));
        var result = resolver.Resolve(new(Guid.NewGuid(), publication.Revision.Id, null, 1,
            new Dictionary<string, RecipePositionSelection>(), [], SupplyDecisionSource.Automatic), new(null, [milk], []), [], TestContext.Current.CancellationToken);
        Assert.Equal(SupplyResolutionStage.Direct, result.Stage);
        var selection = Assert.Single(Assert.IsType<SupplySolution>(result.Selected).PositionSelections).Value;
        if (useVariant) Assert.Equal("free", selection.VariantKey);
        else Assert.Equal(position.ReplacementRules[0].Id, selection.ReplacementRuleId);
    }

    [Theory]
    [InlineData(ConflictType.Allergen)]
    [InlineData(ConflictType.Intolerance)]
    [InlineData(ConflictType.DietaryRequirement)]
    public void All_conflict_locations_preserve_type_and_id(ConflictType type)
    {
        var conflict = new ConflictReference(type, Guid.NewGuid());
        var unit = new IngredientUnitSnapshot(new(Guid.NewGuid(), "g", "g", MeasurementDimension.Mass, 1), 1);
        var ingredient = new IngredientSnapshotSource(Guid.NewGuid(), "TEST", [conflict]);
        var snapshot = Empty() with {
            IngredientPositions = [new(Guid.NewGuid(), null, 0, ingredient, 1, unit, ScalingMode.Linear,
                AgeGroupScalingMode.Ignore, null, [new(Guid.NewGuid(), ingredient, 1, unit, [conflict])])],
            SubrecipePositions = [new(Guid.NewGuid(), null, 0, Guid.NewGuid(), 1, null, null, [conflict],
                [new(Guid.NewGuid(), Guid.NewGuid(), 1, null, null, [conflict], [conflict])])],
            ExposedConflicts = [conflict]
        };
        string json = RecipeSnapshotBuilder.Serialize(snapshot);
        var actual = RecipeSnapshotBuilder.Deserialize(json);
        Assert.Equal(conflict, Assert.Single(actual.ExposedConflicts));
        Assert.Equal(conflict, Assert.Single(actual.IngredientPositions[0].Ingredient.Conflicts));
        Assert.Equal(conflict, Assert.Single(actual.IngredientPositions[0].Replacements[0].ApplicableConflicts));
        Assert.Equal(conflict, Assert.Single(actual.SubrecipePositions[0].RevisionConflicts));
        Assert.Equal(conflict, Assert.Single(actual.SubrecipePositions[0].Replacements[0].ApplicableConflicts));
        Assert.Equal(json, RecipeSnapshotBuilder.Serialize(actual));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"type\":\"Allergen\"}")]
    [InlineData("{\"type\":\"Allergen\",\"id\":\"00000000-0000-0000-0000-000000000000\"}")]
    public void Incomplete_references_are_not_silently_defaulted(string reference)
    {
        string json = RecipeSnapshotBuilder.Serialize(Empty()).Replace("\"exposedConflicts\":[]", "\"exposedConflicts\":[" + reference + "]");
        Assert.Throws<JsonException>(() => RecipeSnapshotBuilder.Deserialize(json));
    }

    private static RecipeSnapshot Empty() => new(2, "TEST", null, null, null, RecipeType.PortionBased,
        new(1, 1, null, null), null, false, [], [], [], [], []);
}
