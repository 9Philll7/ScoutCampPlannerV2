using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientCatalogPersistenceTests
{
    [Fact]
    public async Task Revisioned_ingredient_graph_round_trips()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid ingredientId = Guid.NewGuid();
        Guid revisionId = Guid.NewGuid();
        Guid variantId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        var spoon = new MeasurementUnit(Guid.NewGuid(), "Esslöffel", "EL", MeasurementDimension.Count, 1m);
        var category = new IngredientCategoryRecord
        {
            Id = Guid.NewGuid(), Code = "FATS", Name = "Fette", NormalizedName = "FETTE",
        };
        var milk = new IngredientAllergenDefinitionRecord
        {
            Id = Guid.NewGuid(), Code = "TEST_MILK", Name = "Test-Milch", IsEuMajorAllergen = true,
        };
        var lactose = new IngredientIntoleranceDefinitionRecord
        {
            Id = Guid.NewGuid(), Code = "TEST_LACTOSE", Name = "Test-Laktose",
        };
        var dairy = new IngredientOriginPropertyRecord
        {
            Id = Guid.NewGuid(), Code = "TEST_DAIRY", Name = "Test-Milcherzeugnis", IsAnimalOrigin = true,
        };
        var identity = new IngredientIdentityRecord
        {
            Id = ingredientId, ScopeType = (int)IngredientScopeType.Central,
        };
        var revision = new IngredientRevisionRecord
        {
            Id = revisionId,
            IngredientId = ingredientId,
            RevisionNumber = 1,
            State = (int)IngredientRevisionState.Published,
            Name = "Butter",
            NormalizedName = "BUTTER",
            CategoryId = category.Id,
            BaseUnitId = unit.Id,
            AllergenReviewState = (int)IngredientPropertyReviewState.Reviewed,
            IntoleranceReviewState = (int)IngredientPropertyReviewState.Reviewed,
            OriginReviewState = (int)IngredientPropertyReviewState.Reviewed,
            RowVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = actorId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedBy = actorId,
            PublishedAtUtc = DateTimeOffset.UtcNow,
            PublishedBy = actorId,
        };
        fixture.Database.AddRange(unit, spoon, category, milk, lactose, dairy, identity, revision);
        fixture.Database.AddRange(
            new IngredientRevisionAllergenRecord
            {
                IngredientRevisionId = revisionId, AllergenId = milk.Id,
                State = (int)IngredientPropertyState.Contains,
                Source = (int)IngredientPropertySource.Inherent,
            },
            new IngredientRevisionIntoleranceRecord
            {
                IngredientRevisionId = revisionId, IntoleranceId = lactose.Id,
                State = (int)IngredientPropertyState.Contains,
                Source = (int)IngredientPropertySource.Inherent,
            },
            new IngredientRevisionOriginRecord
            {
                IngredientRevisionId = revisionId, OriginPropertyId = dairy.Id,
                State = (int)IngredientPropertyState.Contains,
                Source = (int)IngredientPropertySource.Inherent,
            },
            new IngredientRevisionUnitConversionRecord
            {
                IngredientRevisionId = revisionId, SourceUnitId = spoon.Id,
                FactorToBaseUnit = 14m, Precision = (int)IngredientConversionPrecision.Average,
            },
            new IngredientVariantRevisionRecord
            {
                Id = variantId, IngredientRevisionId = revisionId, VariantKey = "lactose_free",
                Name = "Laktosefrei", NormalizedName = "LAKTOSEFREI",
            },
            new IngredientVariantIntoleranceOverrideRecord
            {
                VariantRevisionId = variantId, IntoleranceId = lactose.Id,
                State = (int)IngredientPropertyState.DoesNotContain,
                Source = (int)IngredientPropertySource.ManuallyVerified,
            });
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        identity.CurrentPublishedRevisionId = revisionId;
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();

        IngredientIdentityRecord storedIdentity = await fixture.Database.Set<IngredientIdentityRecord>()
            .SingleAsync(TestContext.Current.CancellationToken);
        IngredientRevisionRecord storedRevision = await fixture.Database.Set<IngredientRevisionRecord>()
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(revisionId, storedIdentity.CurrentPublishedRevisionId);
        Assert.Equal("Butter", storedRevision.Name);
        Assert.Single(await fixture.Database.Set<IngredientRevisionAllergenRecord>()
            .ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Single(await fixture.Database.Set<IngredientVariantIntoleranceOverrideRecord>()
            .ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(14m, (await fixture.Database.Set<IngredientRevisionUnitConversionRecord>()
            .SingleAsync(TestContext.Current.CancellationToken)).FactorToBaseUnit);
    }

    [Fact]
    public async Task Only_one_draft_per_ingredient_is_enforced_by_database()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid ingredientId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        Guid categoryId = Guid.Parse("51111111-1111-1111-1111-000000000018");
        fixture.Database.AddRange(unit, new IngredientIdentityRecord
        {
            Id = ingredientId, ScopeType = (int)IngredientScopeType.Central,
        });
        fixture.Database.AddRange(
            DraftRecord(Guid.NewGuid(), ingredientId, categoryId, unit.Id, actorId, 1),
            DraftRecord(Guid.NewGuid(), ingredientId, categoryId, unit.Id, actorId, 2));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Complete_ingredient_catalog_graph_round_trips()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid ingredientId = Guid.NewGuid();
        var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        var ingredient = new BaseIngredient(ingredientId, IngredientScopeType.Central, null, "Mehl", "Österreich");
        var allergen = new Allergen(Guid.NewGuid(), "Gluten");
        var intolerance = new Intolerance(Guid.NewGuid(), "Glutensensitivität");
        var requirement = new DietaryRequirement(Guid.NewGuid(), "Vegan");

        fixture.Database.AddRange(unit, ingredient, allergen, intolerance, requirement);
        fixture.Database.AddRange(
            new IngredientVariant(Guid.NewGuid(), ingredientId, "Bio-Weizenmehl"),
            new IngredientUnitConversion(ingredientId, unit.Id, 1m),
            new BaseIngredientAllergen(ingredientId, allergen.Id),
            new BaseIngredientIntolerance(ingredientId, intolerance.Id),
            new BaseIngredientDietaryRequirement(ingredientId, requirement.Id));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Assert.Equal("Mehl", (await fixture.Database.BaseIngredients.SingleAsync(cancellationToken)).Name);
        Assert.Equal("Bio-Weizenmehl", (await fixture.Database.IngredientVariants.SingleAsync(cancellationToken)).Name);
        Assert.Single(await fixture.Database.IngredientUnitConversions.ToArrayAsync(cancellationToken));
        Assert.Single(await fixture.Database.BaseIngredientAllergens.ToArrayAsync(cancellationToken));
        Assert.Single(await fixture.Database.BaseIngredientIntolerances.ToArrayAsync(cancellationToken));
        Assert.Single(await fixture.Database.BaseIngredientDietaryRequirements.ToArrayAsync(cancellationToken));
    }

    [Fact]
    public async Task Central_ingredient_name_is_unique_despite_null_owner()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        fixture.Database.BaseIngredients.Add(
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Central, null, "Olivenöl"));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.BaseIngredients.Add(
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Central, null, "  OLIVENÖL "));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Same_ingredient_name_is_allowed_for_different_tenants_only()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid firstTenantId = Guid.NewGuid();
        fixture.Database.BaseIngredients.AddRange(
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Tenant, firstTenantId, "Gewürzmischung"),
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Tenant, Guid.NewGuid(), "Gewürzmischung"));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.BaseIngredients.Add(
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Tenant, firstTenantId, "GEWÜRZMISCHUNG"));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Catalog_query_returns_complete_graph_and_respects_visibility_scope()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid tenantId = Guid.NewGuid();
        Guid otherTenantId = Guid.NewGuid();
        Guid campId = Guid.NewGuid();
        Guid centralId = Guid.NewGuid();
        var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        var allergen = new Allergen(Guid.NewGuid(), "Gluten");
        fixture.Database.AddRange(
            unit, allergen,
            new BaseIngredient(centralId, IngredientScopeType.Central, null, "Mehl", "Regional"),
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Tenant, tenantId, "Hausgewürz"),
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Tenant, otherTenantId, "Fremd"),
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Camp, campId, "Lagerzutat"));
        fixture.Database.AddRange(
            new IngredientVariant(Guid.NewGuid(), centralId, "Dinkelmehl"),
            new IngredientUnitConversion(centralId, unit.Id, 1m),
            new BaseIngredientAllergen(centralId, allergen.Id));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var catalog = new IngredientCatalogStore(fixture.Database);

        var central = await catalog.ListCentralAsync(TestContext.Current.CancellationToken);
        var tenant = await catalog.ListTenantAsync(tenantId, TestContext.Current.CancellationToken);
        var camp = await catalog.ListCampAsync(tenantId, campId, TestContext.Current.CancellationToken);

        var flour = Assert.Single(central);
        Assert.Equal("Dinkelmehl", Assert.Single(flour.Variants).Name);
        Assert.Equal("g", Assert.Single(flour.Units).Symbol);
        Assert.Equal("Gluten", Assert.Single(flour.Conflicts).Name);
        Assert.Equal(2, tenant.Count);
        Assert.Equal(3, camp.Count);
        Assert.DoesNotContain(tenant, value => value.Name == "Fremd");
        Assert.Contains(camp, value => value.Name == "Lagerzutat");
    }

    [Fact]
    public async Task Published_revisioned_camp_ingredient_is_available_in_camp_catalog()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid tenantId = Guid.NewGuid();
        Guid campId = Guid.NewGuid();
        Guid ingredientId = Guid.NewGuid();
        Guid revisionId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        var kilogram = new MeasurementUnit(Guid.NewGuid(), "Kilogramm", "kg", MeasurementDimension.Mass, 1_000m);
        var category = new IngredientCategoryRecord
        {
            Id = Guid.NewGuid(), Code = "TEST", Name = "Test", NormalizedName = "TEST",
        };
        var origin = new IngredientOriginPropertyRecord
        {
            Id = Guid.NewGuid(), Code = "TEST_PLANT", Name = "Pflanzlich",
        };
        var allergen = new IngredientAllergenDefinitionRecord
        {
            Id = Guid.NewGuid(), Code = "TEST_ALLERGEN", Name = "Testallergen",
            IsEuMajorAllergen = true,
        };
        var identity = new IngredientIdentityRecord
        {
            Id = ingredientId,
            ScopeType = (int)IngredientScopeType.Camp,
            ScopeId = campId,
        };
        var revision = new IngredientRevisionRecord
        {
            Id = revisionId,
            IngredientId = ingredientId,
            RevisionNumber = 1,
            State = (int)IngredientRevisionState.Published,
            Name = "Veröffentlichte Lagerzutat",
            NormalizedName = "VERÖFFENTLICHTE LAGERZUTAT",
            CategoryId = category.Id,
            BaseUnitId = unit.Id,
            AllergenReviewState = (int)IngredientPropertyReviewState.Reviewed,
            IntoleranceReviewState = (int)IngredientPropertyReviewState.Reviewed,
            OriginReviewState = (int)IngredientPropertyReviewState.Reviewed,
            RowVersion = 2,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = actorId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedBy = actorId,
            PublishedAtUtc = DateTimeOffset.UtcNow,
            PublishedBy = actorId,
        };
        fixture.Database.AddRange(unit, kilogram, category, origin, allergen, identity, revision);
        fixture.Database.AddRange(
            new IngredientRevisionOriginRecord
            {
                IngredientRevisionId = revisionId,
                OriginPropertyId = origin.Id,
                State = (int)IngredientPropertyState.Contains,
                Source = (int)IngredientPropertySource.ManuallyVerified,
            },
            new IngredientRevisionAllergenRecord
            {
                IngredientRevisionId = revisionId,
                AllergenId = allergen.Id,
                State = (int)IngredientPropertyState.Contains,
                Source = (int)IngredientPropertySource.ManuallyVerified,
            });
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        identity.CurrentPublishedRevisionId = revisionId;
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();

        IngredientCatalogEntry entry = Assert.Single(await new IngredientCatalogStore(fixture.Database)
            .ListCampAsync(tenantId, campId, TestContext.Current.CancellationToken));

        Assert.Equal(ingredientId, entry.Id);
        Assert.Equal("Veröffentlichte Lagerzutat", entry.Name);
        Assert.Equal(IngredientScopeType.Camp, entry.Scope);
        Assert.Equal("Pflanzlich", entry.OriginInformation);
        Assert.Equal(["g", "kg"], entry.Units.Select(value => value.Symbol).Order().ToArray());
        Assert.Equal(1_000m, entry.Units.Single(value => value.Symbol == "kg").ReferenceQuantityPerUnit);
        Assert.Equal("Testallergen", Assert.Single(entry.Conflicts).Name);
    }

    [Fact]
    public async Task Revisioned_catalog_entry_replaces_legacy_entry_with_same_identity()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid ingredientId = Guid.NewGuid();
        Guid revisionId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        var category = new IngredientCategoryRecord
        {
            Id = Guid.NewGuid(), Code = "TEST", Name = "Test", NormalizedName = "TEST",
        };
        var identity = new IngredientIdentityRecord
        {
            Id = ingredientId,
            ScopeType = (int)IngredientScopeType.Central,
        };
        var revision = new IngredientRevisionRecord
        {
            Id = revisionId,
            IngredientId = ingredientId,
            RevisionNumber = 2,
            State = (int)IngredientRevisionState.Published,
            Name = "Aktueller Name",
            NormalizedName = "AKTUELLER NAME",
            CategoryId = category.Id,
            BaseUnitId = unit.Id,
            RowVersion = 2,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = actorId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedBy = actorId,
            PublishedAtUtc = DateTimeOffset.UtcNow,
            PublishedBy = actorId,
        };
        fixture.Database.AddRange(
            unit,
            category,
            new BaseIngredient(ingredientId, IngredientScopeType.Central, null, "Alter Name", "Regional"),
            identity,
            revision);
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        identity.CurrentPublishedRevisionId = revisionId;
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();

        IngredientCatalogEntry entry = Assert.Single(await new IngredientCatalogStore(fixture.Database)
            .ListCentralAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Aktueller Name", entry.Name);
        Assert.Equal("Regional", entry.OriginInformation);
    }

    [Fact]
    public async Task Camp_ingredient_creation_persists_variants_and_rejects_duplicate_name()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid campId = Guid.NewGuid();
        var store = new IngredientManagementStore(fixture.Database);
        var request = new ScoutCampPlanner.Catering.Application.Ingredients.CreateIngredientRequest(
            "  Lagerkäse  ", "  Aus der Region  ", ["Mild", "Würzig"]);

        var created = await store.CreateAsync(
            Guid.NewGuid(), IngredientScopeType.Camp, campId, request,
            TestContext.Current.CancellationToken);
        var duplicate = await store.CreateAsync(
            Guid.NewGuid(), IngredientScopeType.Camp, campId,
            request with { Name = "LAGERKÄSE" }, TestContext.Current.CancellationToken);

        Assert.Equal(ScoutCampPlanner.Catering.Application.Ingredients.IngredientMutationStatus.Created,
            created.Status);
        Assert.Equal("Lagerkäse", created.Ingredient!.Name);
        Assert.Equal("Aus der Region", created.Ingredient.OriginInformation);
        Assert.Equal(2, created.Ingredient.Variants.Count);
        Assert.Equal(ScoutCampPlanner.Catering.Application.Ingredients.IngredientMutationStatus.DuplicateName,
            duplicate.Status);
    }

    private sealed class DatabaseFixture(SqliteConnection connection, CateringDbContext database) : IAsyncDisposable
    {
        public CateringDbContext Database { get; } = database;

        public static async Task<DatabaseFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var database = new CateringDbContext(
                new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new DatabaseFixture(connection, database);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private static IngredientRevisionRecord DraftRecord(
        Guid id,
        Guid ingredientId,
        Guid categoryId,
        Guid baseUnitId,
        Guid actorId,
        int revisionNumber) => new()
        {
            Id = id,
            IngredientId = ingredientId,
            RevisionNumber = revisionNumber,
            State = (int)IngredientRevisionState.Draft,
            Name = "Zutat",
            NormalizedName = "ZUTAT",
            CategoryId = categoryId,
            BaseUnitId = baseUnitId,
            RowVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = actorId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedBy = actorId,
        };
}
