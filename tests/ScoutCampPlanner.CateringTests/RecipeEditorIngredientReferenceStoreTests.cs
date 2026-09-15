using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeEditorIngredientReferenceStoreTests
{
    static RecipeEditorIngredientReferenceStoreTests() =>
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());

    [Fact]
    public async Task Resolves_an_older_published_revision_after_a_new_revision_becomes_current()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var database = new CateringDbContext(
            new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
        await database.Database.ExecuteSqlRawAsync(
            database.Database.GenerateCreateScript(), TestContext.Current.CancellationToken);
        Guid ingredientId = Guid.NewGuid();
        Guid oldRevisionId = Guid.NewGuid();
        Guid currentRevisionId = Guid.NewGuid();
        Guid oldUnitId = Guid.NewGuid();
        Guid currentUnitId = Guid.NewGuid();
        Guid allergenId = Guid.NewGuid();
        Guid variantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        var identity = new IngredientIdentityRecord
        {
            Id = ingredientId,
            ScopeType = (int)IngredientScopeType.Central,
            Status = (int)IngredientIdentityStatus.Active,
        };
        database.AddRange(
            identity,
            new MeasurementUnit(oldUnitId, "Alte Rezepteinheit", "are", MeasurementDimension.Mass, 1m),
            new MeasurementUnit(currentUnitId, "Neue Rezepteinheit", "nre", MeasurementDimension.Mass, 1m),
            Revision(oldRevisionId, ingredientId, 1, "Alter Name", oldUnitId, userId, now),
            Revision(currentRevisionId, ingredientId, 2, "Neuer Name", currentUnitId, userId, now.AddDays(1)),
            new IngredientAllergenDefinitionRecord
            {
                Id = allergenId,
                Code = "MILK_TEST",
                Name = "Milch",
                IsEuMajorAllergen = true,
                Status = 0,
            },
            new IngredientRevisionAllergenRecord
            {
                IngredientRevisionId = oldRevisionId,
                AllergenId = allergenId,
                State = (int)IngredientPropertyState.Contains,
                Source = (int)IngredientPropertySource.ManuallyVerified,
            },
            new IngredientVariantRevisionRecord
            {
                Id = variantId,
                IngredientRevisionId = oldRevisionId,
                VariantKey = "MILCHFREI",
                Name = "Milchfreie Variante",
                NormalizedName = "MILCHFREIE VARIANTE",
                Status = 0,
                SortOrder = 0,
            },
            new IngredientVariantAllergenOverrideRecord
            {
                VariantRevisionId = variantId,
                AllergenId = allergenId,
                State = (int)IngredientPropertyState.DoesNotContain,
                Source = (int)IngredientPropertySource.ManuallyVerified,
            });
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        identity.CurrentPublishedRevisionId = currentRevisionId;
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        database.ChangeTracker.Clear();

        var store = new RecipeEditorIngredientReferenceStore(database);
        var result = await store.FindPublishedAsync(
            [oldRevisionId], TestContext.Current.CancellationToken);

        var reference = Assert.Single(result);
        Assert.Equal(oldRevisionId, reference.RevisionId);
        Assert.Equal(ingredientId, reference.IngredientId);
        Assert.Equal(1, reference.RevisionNumber);
        Assert.Equal("Alter Name", reference.Name);
        Assert.Equal(oldUnitId, Assert.Single(reference.Units).UnitId);
        var conflict = Assert.Single(reference.Conflicts);
        Assert.Equal("Milch", conflict.Name);
        Assert.Equal(["Milchfreie Variante"], conflict.PreventableByVariants);
        Assert.Equal(currentRevisionId, reference.AvailableUpdate!.RevisionId);
        Assert.Equal(2, reference.AvailableUpdate.RevisionNumber);
        Assert.Equal("Neuer Name", reference.AvailableUpdate.Name);
        Assert.Equal(currentUnitId, Assert.Single(reference.AvailableUpdate.Units).UnitId);
    }

    private static IngredientRevisionRecord Revision(
        Guid id, Guid ingredientId, int revisionNumber, string name, Guid unitId,
        Guid userId, DateTimeOffset timestamp) => new()
    {
        Id = id,
        IngredientId = ingredientId,
        RevisionNumber = revisionNumber,
        State = (int)IngredientRevisionState.Published,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        CategoryId = Guid.Parse("51111111-1111-1111-1111-000000000002"),
        BaseUnitId = unitId,
        RowVersion = 1,
        CreatedAtUtc = timestamp,
        CreatedBy = userId,
        UpdatedAtUtc = timestamp,
        UpdatedBy = userId,
        PublishedAtUtc = timestamp,
        PublishedBy = userId,
    };
}
