using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientRevisionWorkflowPersistenceTests
{
    [Fact]
    public async Task Create_draft_persists_identity_scope_and_initial_revision()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid actorId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        var references = await fixture.AddReferencesAsync();
        var store = new IngredientRevisionWorkflowStore(fixture.Database);
        IngredientRevisionDraftContent content = IngredientRevisionDraftContent.Create(
            "  Haferflocken ",
            references.CategoryId,
            references.UnitId,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed);

        IngredientRevisionMutationResult result = await store.CreateDraftAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new IngredientRevisionScope(IngredientScopeType.Tenant, tenantId),
            content,
            actorId,
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Created, result.Status);
        IngredientIdentityRecord identity = await fixture.Database.Set<IngredientIdentityRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        IngredientRevisionRecord revision = await fixture.Database.Set<IngredientRevisionRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)IngredientScopeType.Tenant, identity.ScopeType);
        Assert.Equal(tenantId, identity.ScopeId);
        Assert.Equal(result.IngredientId, identity.Id);
        Assert.Equal(result.RevisionId, revision.Id);
        Assert.Equal("Haferflocken", revision.Name);
        Assert.Equal(1, revision.RowVersion);
        Assert.Equal((int)IngredientRevisionState.Draft, revision.State);
        IngredientRevisionSummary summary = Assert.Single(await store.ListAsync(
            new IngredientRevisionScope(IngredientScopeType.Tenant, tenantId),
            TestContext.Current.CancellationToken));
        Assert.Equal(result.RevisionId, summary.RevisionId);
        Assert.Equal(IngredientRevisionState.Draft, summary.State);
    }

    [Fact]
    public async Task Create_draft_rejects_kitchen_measure_as_base_unit()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid actorId = Guid.NewGuid();
        var category = new IngredientCategoryRecord
        {
            Id = Guid.NewGuid(), Code = "TEST_CATEGORY", Name = "Testkategorie",
            NormalizedName = "TESTKATEGORIE",
        };
        var tablespoon = new MeasurementUnit(
            Guid.NewGuid(), "Esslöffel", "EL", MeasurementDimension.Count, 1m);
        fixture.Database.AddRange(category, tablespoon);
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var store = new IngredientRevisionWorkflowStore(fixture.Database);
        IngredientRevisionDraftContent content = IngredientRevisionDraftContent.Create(
            "Mehl",
            category.Id,
            tablespoon.Id,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed);

        IngredientRevisionMutationResult result = await store.CreateDraftAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new IngredientRevisionScope(IngredientScopeType.Central, null),
            content,
            actorId,
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Invalid, result.Status);
        Assert.Empty(await fixture.Database.Set<IngredientIdentityRecord>()
            .AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Specific_conversion_allows_other_dimensions_but_rejects_automatic_same_dimension()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var category = new IngredientCategoryRecord
        {
            Id = Guid.NewGuid(), Code = "TEST_CATEGORY", Name = "Testkategorie",
            NormalizedName = "TESTKATEGORIE",
        };
        var gram = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        var kilogram = new MeasurementUnit(Guid.NewGuid(), "Kilogramm", "kg", MeasurementDimension.Mass, 1_000m);
        var milliliter = new MeasurementUnit(Guid.NewGuid(), "Milliliter", "ml", MeasurementDimension.Volume, 1m);
        fixture.Database.AddRange(category, gram, kilogram, milliliter);
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var store = new IngredientRevisionWorkflowStore(fixture.Database);

        IngredientRevisionMutationResult sameDimension = await store.CreateDraftAsync(
            Guid.NewGuid(), Guid.NewGuid(), new IngredientRevisionScope(IngredientScopeType.Central, null),
            IngredientRevisionDraftContent.Create(
                "Mehl", category.Id, gram.Id,
                IngredientPropertyReviewState.Unreviewed,
                IngredientPropertyReviewState.Unreviewed,
                IngredientPropertyReviewState.Unreviewed,
                unitConversions: [new IngredientRevisionUnitConversion(
                    kilogram.Id, 1_000m, IngredientConversionPrecision.Exact)]),
            Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        IngredientRevisionMutationResult otherDimension = await store.CreateDraftAsync(
            Guid.NewGuid(), Guid.NewGuid(), new IngredientRevisionScope(IngredientScopeType.Central, null),
            IngredientRevisionDraftContent.Create(
                "Honig", category.Id, gram.Id,
                IngredientPropertyReviewState.Unreviewed,
                IngredientPropertyReviewState.Unreviewed,
                IngredientPropertyReviewState.Unreviewed,
                unitConversions: [new IngredientRevisionUnitConversion(
                    milliliter.Id, 1.4m, IngredientConversionPrecision.Average)]),
            Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Invalid, sameDimension.Status);
        Assert.Equal(IngredientRevisionMutationStatus.Created, otherDimension.Status);
    }

    [Fact]
    public async Task Save_draft_updates_content_and_reports_stale_version()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: false);
        Guid allergenId = Guid.NewGuid();
        Guid intoleranceId = Guid.NewGuid();
        Guid originId = Guid.NewGuid();
        Guid conversionUnitId = Guid.NewGuid();
        fixture.Database.AddRange(
            new IngredientAllergenDefinitionRecord
            {
                Id = allergenId, Code = "TEST_ALLERGEN", Name = "Testallergen", IsEuMajorAllergen = true,
            },
            new IngredientIntoleranceDefinitionRecord
            {
                Id = intoleranceId, Code = "TEST_INTOLERANCE", Name = "Testunverträglichkeit",
            },
            new IngredientOriginPropertyRecord
            {
                Id = originId, Code = "TEST_ORIGIN", Name = "Testherkunft",
            },
            new MeasurementUnit(conversionUnitId, "EsslÃ¶ffel", "EL", MeasurementDimension.Count, 1m));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();
        var store = new IngredientRevisionWorkflowStore(fixture.Database);
        IngredientRevisionDraftContent content = IngredientRevisionDraftContent.Create(
            "Gelbe Linsen",
            seed.CategoryId,
            seed.UnitId,
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed,
            [new IngredientPropertyValue(allergenId, IngredientPropertyState.MayContain,
                IngredientPropertySource.ManuallyVerified)],
            [new IngredientPropertyValue(intoleranceId, IngredientPropertyState.DoesNotContain,
                IngredientPropertySource.ManuallyVerified)],
            [new IngredientPropertyValue(originId, IngredientPropertyState.Contains,
                IngredientPropertySource.ManuallyVerified)],
            [new IngredientRevisionUnitConversion(
                conversionUnitId, 15m, IngredientConversionPrecision.Average)]);

        IngredientRevisionMutationResult saved = await store.SaveDraftAsync(
            seed.RevisionId, content, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        IngredientRevisionDraftContent staleContent = IngredientRevisionDraftContent.Create(
            "Veraltete Änderung",
            seed.CategoryId,
            seed.UnitId,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed);
        IngredientRevisionMutationResult stale = await store.SaveDraftAsync(
            seed.RevisionId, staleContent, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Saved, saved.Status);
        Assert.Equal(2, saved.RowVersion);
        Assert.Equal(IngredientRevisionMutationStatus.ConcurrencyConflict, stale.Status);
        Assert.Equal(2, stale.RowVersion);
        IngredientRevisionRecord stored = await fixture.Database.Set<IngredientRevisionRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Gelbe Linsen", stored.Name);
        Assert.Equal("GELBE LINSEN", stored.NormalizedName);
        Assert.Equal((int)IngredientPropertyState.MayContain,
            Assert.Single(await fixture.Database.Set<IngredientRevisionAllergenRecord>()
                .AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken)).State);
        Assert.Equal(intoleranceId,
            Assert.Single(await fixture.Database.Set<IngredientRevisionIntoleranceRecord>()
                .AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken)).IntoleranceId);
        Assert.Equal(originId,
            Assert.Single(await fixture.Database.Set<IngredientRevisionOriginRecord>()
                .AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken)).OriginPropertyId);
        IngredientRevisionUnitConversionRecord storedConversion = Assert.Single(
            await fixture.Database.Set<IngredientRevisionUnitConversionRecord>()
                .AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(conversionUnitId, storedConversion.SourceUnitId);
        Assert.Equal(15m, storedConversion.FactorToBaseUnit);
    }

    [Fact]
    public async Task Save_draft_synchronizes_variants_and_preserves_stable_key()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: false);
        var store = new IngredientRevisionWorkflowStore(fixture.Database);
        Guid variantId = Guid.NewGuid();
        Guid milkId = Guid.Parse("21111111-1111-1111-1111-000000000007");
        Guid lactoseId = Guid.Parse("31111111-1111-1111-1111-000000000001");
        Guid spoonId = Guid.NewGuid();
        fixture.Database.Add(new MeasurementUnit(
            spoonId, "Test-Esslöffel", "EL", MeasurementDimension.Count, 1m));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();
        IngredientRevisionDraftContent initial = IngredientRevisionDraftContent.Create(
            "Linsen",
            seed.CategoryId,
            seed.UnitId,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed,
            unitConversions: [new IngredientRevisionUnitConversion(
                spoonId, 15m, IngredientConversionPrecision.Average)],
            variants: [new IngredientVariantDraftContent(
                variantId, "red_lentils", "Rote Linsen", true, 0,
                allergenOverrides: [new IngredientPropertyValue(
                    milkId, IngredientPropertyState.Contains, IngredientPropertySource.ManuallyVerified)],
                unitConversionOverrides: [new IngredientRevisionUnitConversion(
                    spoonId, 12m, IngredientConversionPrecision.Estimated)])]);

        IngredientRevisionMutationResult created = await store.SaveDraftAsync(
            seed.RevisionId, initial, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        IngredientRevisionDraftContent renamed = IngredientRevisionDraftContent.Create(
            "Linsen",
            seed.CategoryId,
            seed.UnitId,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed,
            unitConversions: [new IngredientRevisionUnitConversion(
                spoonId, 15m, IngredientConversionPrecision.Average)],
            variants: [new IngredientVariantDraftContent(
                variantId, "red_lentils", "Rote Linsen, geschält", false, 0,
                allergenOverrides: [new IngredientPropertyValue(
                    milkId, IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)],
                intoleranceOverrides: [new IngredientPropertyValue(
                    lactoseId, IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)],
                unitConversionOverrides: [new IngredientRevisionUnitConversion(
                    spoonId, 10.5m, IngredientConversionPrecision.Estimated)])]);
        IngredientRevisionMutationResult updated = await store.SaveDraftAsync(
            seed.RevisionId, renamed, 2, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        IngredientRevisionDraftContent changedKey = IngredientRevisionDraftContent.Create(
            "Linsen",
            seed.CategoryId,
            seed.UnitId,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed,
            IngredientPropertyReviewState.Unreviewed,
            unitConversions: [new IngredientRevisionUnitConversion(
                spoonId, 15m, IngredientConversionPrecision.Average)],
            variants: [new IngredientVariantDraftContent(variantId, "changed_key", "Rote Linsen, geschält", false, 0)]);
        IngredientRevisionMutationResult rejected = await store.SaveDraftAsync(
            seed.RevisionId, changedKey, 3, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Saved, created.Status);
        Assert.Equal(IngredientRevisionMutationStatus.Saved, updated.Status);
        Assert.Equal(IngredientRevisionMutationStatus.Invalid, rejected.Status);
        IngredientVariantRevisionRecord stored = Assert.Single(await fixture.Database
            .Set<IngredientVariantRevisionRecord>().AsNoTracking()
            .ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("red_lentils", stored.VariantKey);
        Assert.Equal("Rote Linsen, geschält", stored.Name);
        Assert.Equal(1, stored.Status);
        Assert.Equal((int)IngredientPropertyState.DoesNotContain,
            (await fixture.Database.Set<IngredientVariantAllergenOverrideRecord>().AsNoTracking()
                .SingleAsync(TestContext.Current.CancellationToken)).State);
        Assert.Equal(lactoseId,
            (await fixture.Database.Set<IngredientVariantIntoleranceOverrideRecord>().AsNoTracking()
                .SingleAsync(TestContext.Current.CancellationToken)).IntoleranceId);
        IngredientVariantUnitConversionOverrideRecord storedConversion = await fixture.Database
            .Set<IngredientVariantUnitConversionOverrideRecord>().AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(spoonId, storedConversion.SourceUnitId);
        Assert.Equal(10.5m, storedConversion.FactorToBaseUnit);
        Assert.Equal(3, (await fixture.Database.Set<IngredientRevisionRecord>().AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken)).RowVersion);
    }

    [Fact]
    public async Task List_prefers_editable_draft_over_current_published_revision()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: true);
        IngredientRevisionRecord published = await fixture.Database.Set<IngredientRevisionRecord>().SingleAsync(
            value => value.Id == seed.RevisionId, TestContext.Current.CancellationToken);
        IngredientIdentityRecord identity = await fixture.Database.Set<IngredientIdentityRecord>().SingleAsync(
            value => value.Id == published.IngredientId, TestContext.Current.CancellationToken);
        published.State = (int)IngredientRevisionState.Published;
        identity.CurrentPublishedRevisionId = published.Id;
        Guid draftId = Guid.NewGuid();
        fixture.Database.Add(new IngredientRevisionRecord
        {
            Id = draftId,
            IngredientId = published.IngredientId,
            RevisionNumber = 2,
            State = (int)IngredientRevisionState.Draft,
            BasedOnRevisionId = published.Id,
            Name = "Linsen neu",
            NormalizedName = "LINSEN NEU",
            CategoryId = seed.CategoryId,
            BaseUnitId = seed.UnitId,
            RowVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = seed.ActorId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedBy = seed.ActorId,
        });
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();
        var store = new IngredientRevisionWorkflowStore(fixture.Database);

        IngredientRevisionSummary result = Assert.Single(await store.ListAsync(
            new IngredientRevisionScope(IngredientScopeType.Central, null),
            TestContext.Current.CancellationToken));

        Assert.Equal(draftId, result.RevisionId);
        Assert.Equal(IngredientRevisionState.Draft, result.State);
    }

    [Fact]
    public async Task Get_returns_revision_scope_and_complete_editable_graph()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: true);
        Guid allergenId = Guid.NewGuid();
        Guid variantId = Guid.NewGuid();
        fixture.Database.AddRange(
            new IngredientAllergenDefinitionRecord
            {
                Id = allergenId, Code = "TEST_MILK", Name = "Test-Milch", IsEuMajorAllergen = true,
            },
            new IngredientRevisionAllergenRecord
            {
                IngredientRevisionId = seed.RevisionId,
                AllergenId = allergenId,
                State = (int)IngredientPropertyState.Contains,
                Source = (int)IngredientPropertySource.ManuallyVerified,
            },
            new IngredientVariantRevisionRecord
            {
                Id = variantId,
                IngredientRevisionId = seed.RevisionId,
                VariantKey = "lactose_free",
                Name = "Laktosefrei",
                NormalizedName = "LAKTOSEFREI",
            },
            new IngredientVariantAllergenOverrideRecord
            {
                VariantRevisionId = variantId,
                AllergenId = allergenId,
                State = (int)IngredientPropertyState.DoesNotContain,
                Source = (int)IngredientPropertySource.ManuallyVerified,
            });
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();
        var store = new IngredientRevisionWorkflowStore(fixture.Database);

        IngredientRevisionDraftDetails? result = await store.GetAsync(
            seed.RevisionId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(IngredientScopeType.Central, result.ScopeType);
        Assert.Equal("Linsen", result.Name);
        Assert.Equal(1, result.RowVersion);
        Assert.Equal(allergenId, Assert.Single(result.Allergens).PropertyId);
        IngredientVariantRevisionItem variant = Assert.Single(result.Variants);
        Assert.Equal("lactose_free", variant.VariantKey);
        Assert.Equal(IngredientPropertyState.DoesNotContain,
            Assert.Single(variant.AllergenOverrides).State);
    }

    [Fact]
    public async Task Follow_up_draft_copies_complete_published_graph()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: true);
        Guid allergenId = Guid.NewGuid();
        Guid variantId = Guid.NewGuid();
        var spoon = new MeasurementUnit(Guid.NewGuid(), "Esslöffel", "EL", MeasurementDimension.Count, 1m);
        fixture.Database.AddRange(
            spoon,
            new IngredientAllergenDefinitionRecord
            {
                Id = allergenId, Code = "TEST_MILK", Name = "Test-Milch", IsEuMajorAllergen = true,
            },
            new IngredientRevisionAllergenRecord
            {
                IngredientRevisionId = seed.RevisionId,
                AllergenId = allergenId,
                State = (int)IngredientPropertyState.Contains,
                Source = (int)IngredientPropertySource.ManuallyVerified,
            },
            new IngredientRevisionUnitConversionRecord
            {
                IngredientRevisionId = seed.RevisionId,
                SourceUnitId = spoon.Id,
                FactorToBaseUnit = 12m,
                Precision = (int)IngredientConversionPrecision.Average,
            },
            new IngredientVariantRevisionRecord
            {
                Id = variantId,
                IngredientRevisionId = seed.RevisionId,
                VariantKey = "fine",
                Name = "Fein",
                NormalizedName = "FEIN",
            },
            new IngredientVariantAllergenOverrideRecord
            {
                VariantRevisionId = variantId,
                AllergenId = allergenId,
                State = (int)IngredientPropertyState.DoesNotContain,
                Source = (int)IngredientPropertySource.ManuallyVerified,
            });
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();
        var store = new IngredientRevisionWorkflowStore(fixture.Database);
        Assert.Equal(IngredientRevisionMutationStatus.Published, (await store.PublishAsync(
            seed.RevisionId, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).Status);
        Guid nextRevisionId = Guid.NewGuid();

        IngredientRevisionMutationResult created = await store.CreateDraftFromPublishedAsync(
            seed.RevisionId, nextRevisionId, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        IngredientRevisionDraftDetails? draft = await store.GetAsync(
            nextRevisionId, TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Created, created.Status);
        Assert.NotNull(draft);
        Assert.Equal(IngredientRevisionState.Draft, draft.State);
        Assert.Equal(seed.RevisionId, draft.BasedOnRevisionId);
        Assert.Equal(2, draft.RevisionNumber);
        Assert.Equal(allergenId, Assert.Single(draft.Allergens).PropertyId);
        Assert.Equal(12m, Assert.Single(draft.UnitConversions).FactorToBaseUnit);
        Assert.Equal("fine", Assert.Single(draft.Variants).VariantKey);
        Assert.Equal(IngredientPropertyState.DoesNotContain,
            Assert.Single(Assert.Single(draft.Variants).AllergenOverrides).State);
    }

    [Fact]
    public async Task Camp_fork_is_created_from_current_central_revision_on_first_changed_save()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: true);
        var store = new IngredientRevisionWorkflowStore(fixture.Database);
        Assert.Equal(IngredientRevisionMutationStatus.Published, (await store.PublishAsync(
            seed.RevisionId, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken)).Status);
        Guid campId = Guid.NewGuid();
        Guid forkIngredientId = Guid.NewGuid();
        Guid forkRevisionId = Guid.NewGuid();
        IngredientRevisionDraftContent changed = IngredientRevisionDraftContent.Create(
            "Rote Linsen",
            seed.CategoryId,
            seed.UnitId,
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed);

        IngredientRevisionMutationResult result = await store.CreateForkDraftAsync(
            seed.RevisionId, 2, forkIngredientId, forkRevisionId,
            new IngredientRevisionScope(IngredientScopeType.Camp, campId),
            changed, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        IngredientRevisionMutationResult duplicate = await store.CreateForkDraftAsync(
            seed.RevisionId, 2, Guid.NewGuid(), Guid.NewGuid(),
            new IngredientRevisionScope(IngredientScopeType.Camp, campId),
            changed, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Created, result.Status);
        Assert.Equal(IngredientRevisionMutationStatus.DraftAlreadyExists, duplicate.Status);
        Assert.Equal(forkRevisionId, duplicate.RevisionId);
        IngredientIdentityRecord fork = await fixture.Database.Set<IngredientIdentityRecord>()
            .AsNoTracking().SingleAsync(value => value.Id == forkIngredientId,
                TestContext.Current.CancellationToken);
        Assert.Equal((int)IngredientScopeType.Camp, fork.ScopeType);
        Assert.Equal(campId, fork.ScopeId);
        Assert.Equal(seed.IngredientId, fork.SourceIngredientId);
        Assert.Equal(seed.RevisionId, fork.SourceRevisionId);
        IngredientRevisionRecord draft = await fixture.Database.Set<IngredientRevisionRecord>()
            .AsNoTracking().SingleAsync(value => value.Id == forkRevisionId,
                TestContext.Current.CancellationToken);
        Assert.Equal("Rote Linsen", draft.Name);
        Assert.Equal(seed.RevisionId, draft.BasedOnRevisionId);
        Assert.Equal(seed.RevisionId, draft.MergedCentralRevisionId);
    }

    [Fact]
    public async Task Publish_is_transactional_and_updates_identity_pointer()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: true);
        var store = new IngredientRevisionWorkflowStore(fixture.Database);

        IngredientRevisionMutationResult result = await store.PublishAsync(
            seed.RevisionId, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Published, result.Status);
        Assert.Equal(2, result.RowVersion);
        IngredientRevisionRecord revision = await fixture.Database.Set<IngredientRevisionRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        IngredientIdentityRecord identity = await fixture.Database.Set<IngredientIdentityRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)IngredientRevisionState.Published, revision.State);
        Assert.Equal(seed.RevisionId, identity.CurrentPublishedRevisionId);
    }

    [Fact]
    public async Task Invalid_publication_leaves_draft_and_identity_unchanged()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: false);
        var store = new IngredientRevisionWorkflowStore(fixture.Database);

        IngredientRevisionMutationResult result = await store.PublishAsync(
            seed.RevisionId, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Invalid, result.Status);
        IngredientRevisionRecord revision = await fixture.Database.Set<IngredientRevisionRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        IngredientIdentityRecord identity = await fixture.Database.Set<IngredientIdentityRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)IngredientRevisionState.Draft, revision.State);
        Assert.Null(identity.CurrentPublishedRevisionId);
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

        public async Task<Seed> SeedDraftAsync(bool reviewed)
        {
            Guid ingredientId = Guid.NewGuid();
            Guid revisionId = Guid.NewGuid();
            Guid actorId = Guid.NewGuid();
            var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
            Guid categoryId = Guid.Parse("51111111-1111-1111-1111-000000000002");
            Database.AddRange(
                unit,
                new IngredientIdentityRecord
                {
                    Id = ingredientId,
                    ScopeType = (int)IngredientScopeType.Central,
                    Status = (int)IngredientIdentityStatus.Active,
                },
                new IngredientRevisionRecord
                {
                    Id = revisionId,
                    IngredientId = ingredientId,
                    RevisionNumber = 1,
                    State = (int)IngredientRevisionState.Draft,
                    Name = "Linsen",
                    NormalizedName = "LINSEN",
                    CategoryId = categoryId,
                    BaseUnitId = unit.Id,
                    AllergenReviewState = reviewed ? 1 : 0,
                    IntoleranceReviewState = reviewed ? 1 : 0,
                    OriginReviewState = reviewed ? 1 : 0,
                    RowVersion = 1,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = actorId,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedBy = actorId,
                });
            await Database.SaveChangesAsync(TestContext.Current.CancellationToken);
            Database.ChangeTracker.Clear();
            return new Seed(ingredientId, revisionId, categoryId, unit.Id, actorId);
        }

        public async Task<(Guid CategoryId, Guid UnitId)> AddReferencesAsync()
        {
            var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
            var category = new IngredientCategoryRecord
            {
                Id = Guid.NewGuid(), Code = "TEST_CATEGORY", Name = "Testkategorie",
                NormalizedName = "TESTKATEGORIE",
            };
            Database.AddRange(unit, category);
            await Database.SaveChangesAsync(TestContext.Current.CancellationToken);
            Database.ChangeTracker.Clear();
            return (category.Id, unit.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record Seed(Guid IngredientId, Guid RevisionId, Guid CategoryId, Guid UnitId, Guid ActorId);
}
