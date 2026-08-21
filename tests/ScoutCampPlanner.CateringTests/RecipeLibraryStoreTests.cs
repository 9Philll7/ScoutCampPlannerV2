using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeLibraryStoreTests
{
    [Fact]
    public async Task Tenant_library_accepts_only_central_revision_and_deduplicates_it()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid centralRevision = await fixture.PublishAsync(RecipeScopeType.Central, null, "Zentral");
        Guid tenantRevision = await fixture.PublishAsync(RecipeScopeType.Tenant, fixture.TenantId, "Mandant");

        RecipeLibraryMutationResult added = await fixture.Libraries.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), fixture.TenantId, centralRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        RecipeLibraryMutationResult duplicate = await fixture.Libraries.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), fixture.TenantId, centralRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        RecipeLibraryMutationResult invalid = await fixture.Libraries.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), fixture.TenantId, tenantRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeLibraryMutationStatus.Added, added.Status);
        Assert.Equal(RecipeLibraryMutationStatus.AlreadyExists, duplicate.Status);
        Assert.Equal(RecipeLibraryMutationStatus.InvalidSourceScope, invalid.Status);
        TenantRecipeLibraryEntry entry = Assert.Single(await fixture.Libraries.ListTenantEntriesAsync(
            fixture.TenantId, TestContext.Current.CancellationToken));
        Assert.Equal(centralRevision, entry.SourceId);
        Assert.Equal(RecipeLibraryEntryType.UpstreamRevision, entry.Type);
    }

    [Fact]
    public async Task Camp_library_accepts_central_and_tenant_but_not_camp_revision()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid centralRevision = await fixture.PublishAsync(RecipeScopeType.Central, null, "Zentral");
        Guid tenantRevision = await fixture.PublishAsync(RecipeScopeType.Tenant, fixture.TenantId, "Mandant");
        Guid campRevision = await fixture.PublishAsync(RecipeScopeType.Camp, fixture.CampId, "Lager");

        RecipeLibraryMutationResult central = await fixture.Libraries.AddUpstreamRevisionToCampAsync(
            Guid.NewGuid(), fixture.CampId, centralRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        RecipeLibraryMutationResult tenant = await fixture.Libraries.AddUpstreamRevisionToCampAsync(
            Guid.NewGuid(), fixture.CampId, tenantRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        RecipeLibraryMutationResult invalid = await fixture.Libraries.AddUpstreamRevisionToCampAsync(
            Guid.NewGuid(), fixture.CampId, campRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeLibraryMutationStatus.Added, central.Status);
        Assert.Equal(RecipeLibraryMutationStatus.Added, tenant.Status);
        Assert.Equal(RecipeLibraryMutationStatus.InvalidSourceScope, invalid.Status);
        CampRecipeLibraryEntry[] entries = (await fixture.Libraries.ListCampEntriesAsync(
            fixture.CampId, TestContext.Current.CancellationToken)).ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Contains(entries, value => value.SourceId == centralRevision && value.UpstreamScope == RecipeScopeType.Central);
        Assert.Contains(entries, value => value.SourceId == tenantRevision && value.UpstreamScope == RecipeScopeType.Tenant);
    }

    [Fact]
    public async Task Tenant_upstream_entry_converts_to_independent_tenant_draft()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid centralRevision = await fixture.PublishAsync(RecipeScopeType.Central, null, "Zentral");
        RecipeLibraryMutationResult added = await fixture.Libraries.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), fixture.TenantId, centralRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        Guid localRecipeId = Guid.NewGuid();

        RecipeLibraryMutationResult converted = await fixture.Libraries.ConvertTenantEntryToLocalRecipeAsync(
            added.EntryId!.Value, localRecipeId, "Mandantenkopie", fixture.UserId, fixture.Now.AddMinutes(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeLibraryMutationStatus.Converted, converted.Status);
        TenantRecipeLibraryEntry entry = Assert.Single(await fixture.Libraries.ListTenantEntriesAsync(
            fixture.TenantId, TestContext.Current.CancellationToken));
        Assert.Equal(RecipeLibraryEntryType.LocalRecipe, entry.Type);
        Assert.Equal(localRecipeId, entry.SourceId);
        RecipeDraft local = Assert.IsType<RecipeDraft>(await fixture.Drafts.FindAsync(
            localRecipeId, TestContext.Current.CancellationToken));
        Assert.Equal(RecipeScopeType.Tenant, local.ScopeType);
        Assert.Equal("Mandantenkopie", local.Name);
        Assert.Equal(RecipeStatus.Draft, local.Status);
        (Guid? centralRecipe, Guid? centralSourceRevision, Guid? tenantRecipe, Guid? tenantSourceRevision) =
            await fixture.ReadLineageAsync(localRecipeId);
        Assert.NotNull(centralRecipe);
        Assert.Equal(centralRevision, centralSourceRevision);
        Assert.Null(tenantRecipe);
        Assert.Null(tenantSourceRevision);
    }

    [Fact]
    public async Task Camp_upstream_entry_converts_to_independent_camp_draft()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid tenantRevision = await fixture.PublishAsync(RecipeScopeType.Tenant, fixture.TenantId, "Mandant");
        RecipeLibraryMutationResult added = await fixture.Libraries.AddUpstreamRevisionToCampAsync(
            Guid.NewGuid(), fixture.CampId, tenantRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        Guid localRecipeId = Guid.NewGuid();

        RecipeLibraryMutationResult converted = await fixture.Libraries.ConvertCampEntryToLocalRecipeAsync(
            added.EntryId!.Value, localRecipeId, "Lagerkopie", fixture.UserId, fixture.Now.AddMinutes(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeLibraryMutationStatus.Converted, converted.Status);
        CampRecipeLibraryEntry entry = Assert.Single(await fixture.Libraries.ListCampEntriesAsync(
            fixture.CampId, TestContext.Current.CancellationToken));
        Assert.Equal(RecipeLibraryEntryType.LocalRecipe, entry.Type);
        Assert.Equal(localRecipeId, entry.SourceId);
        Assert.Null(entry.UpstreamScope);
        RecipeDraft local = Assert.IsType<RecipeDraft>(await fixture.Drafts.FindAsync(
            localRecipeId, TestContext.Current.CancellationToken));
        Assert.Equal(RecipeScopeType.Camp, local.ScopeType);
        Assert.Equal(fixture.CampId, local.ScopeId);
        (Guid? centralRecipe, Guid? centralRevision, Guid? tenantRecipe, Guid? tenantSourceRevision) =
            await fixture.ReadLineageAsync(localRecipeId);
        Assert.Null(centralRecipe);
        Assert.Null(centralRevision);
        Assert.NotNull(tenantRecipe);
        Assert.Equal(tenantRevision, tenantSourceRevision);
        Assert.Empty(await fixture.Libraries.CheckCampUpdatesAsync(
            fixture.CampId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Upstream_updates_are_reported_and_adopted_only_explicitly()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid revisionOne = await fixture.PublishAsync(RecipeScopeType.Central, null, "Zentral");
        RecipeLibraryMutationResult tenantEntry = await fixture.Libraries.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), fixture.TenantId, revisionOne, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        RecipeLibraryMutationResult campEntry = await fixture.Libraries.AddUpstreamRevisionToCampAsync(
            Guid.NewGuid(), fixture.CampId, revisionOne, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        Guid revisionTwo = await fixture.PublishNextAsync(revisionOne);

        RecipeLibraryUpdate tenantUpdate = Assert.Single(await fixture.Libraries.CheckTenantUpdatesAsync(
            fixture.TenantId, TestContext.Current.CancellationToken));
        RecipeLibraryUpdate campUpdate = Assert.Single(await fixture.Libraries.CheckCampUpdatesAsync(
            fixture.CampId, TestContext.Current.CancellationToken));

        Assert.True(tenantUpdate.UpdateAvailable);
        Assert.Equal(revisionOne, tenantUpdate.CurrentRevisionId);
        Assert.Equal(revisionTwo, tenantUpdate.LatestRevisionId);
        Assert.True(campUpdate.UpdateAvailable);
        Assert.Equal(revisionOne, (await fixture.Libraries.ListTenantEntriesAsync(
            fixture.TenantId, TestContext.Current.CancellationToken)).Single().SourceId);

        RecipeLibraryMutationResult adopted = await fixture.Libraries.AdoptLatestTenantRevisionAsync(
            tenantEntry.EntryId!.Value, fixture.UserId, fixture.Now.AddMinutes(2),
            TestContext.Current.CancellationToken);
        RecipeLibraryMutationResult noUpdate = await fixture.Libraries.AdoptLatestTenantRevisionAsync(
            tenantEntry.EntryId.Value, fixture.UserId, fixture.Now.AddMinutes(3),
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeLibraryMutationStatus.Updated, adopted.Status);
        Assert.Equal(RecipeLibraryMutationStatus.NoUpdate, noUpdate.Status);
        Assert.Equal(revisionTwo, (await fixture.Libraries.ListTenantEntriesAsync(
            fixture.TenantId, TestContext.Current.CancellationToken)).Single().SourceId);
        Assert.Equal(revisionOne, (await fixture.Libraries.ListCampEntriesAsync(
            fixture.CampId, TestContext.Current.CancellationToken)).Single().SourceId);

        Assert.Equal(RecipeLibraryMutationStatus.Updated,
            (await fixture.Libraries.AdoptLatestCampRevisionAsync(
                campEntry.EntryId!.Value, fixture.UserId, fixture.Now.AddMinutes(4),
                TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Local_revision_submission_exposes_three_way_comparison()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid centralRevisionOne = await fixture.PublishAsync(RecipeScopeType.Central, null, "Zentral");
        RecipeLibraryMutationResult entry = await fixture.Libraries.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), fixture.TenantId, centralRevisionOne, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        Guid localRecipeId = Guid.NewGuid();
        await fixture.Libraries.ConvertTenantEntryToLocalRecipeAsync(
            entry.EntryId!.Value, localRecipeId, "Verbesserung", fixture.UserId, fixture.Now.AddMinutes(1),
            TestContext.Current.CancellationToken);
        Guid localRevision = await fixture.PublishCurrentAsync(localRecipeId);

        RecipeSubmissionResult submitted = await fixture.Submissions.SubmitAsync(
            Guid.NewGuid(), localRevision, fixture.UserId, fixture.Now.AddMinutes(2),
            TestContext.Current.CancellationToken);
        Guid centralRevisionTwo = await fixture.PublishNextAsync(centralRevisionOne);
        CentralRecipeChangeComparison comparison = Assert.Single(await fixture.Submissions.ListPendingAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(RecipeSubmissionStatus.Submitted, submitted.Status);
        Assert.Equal(centralRevisionOne, comparison.SourceCentralRevisionId);
        Assert.Equal(centralRevisionTwo, comparison.LatestCentralRevisionId);
        Assert.Equal(localRevision, comparison.SubmittedLocalRevisionId);
        Assert.Equal("Zentral", comparison.SourceCentralRevision.Name);
        Assert.Equal("Verbesserung", comparison.SubmittedLocalRevision.Name);
        Assert.Equal(RecipeSubmissionStatus.AlreadySubmitted,
            (await fixture.Submissions.SubmitAsync(
                Guid.NewGuid(), localRevision, fixture.UserId, fixture.Now.AddMinutes(3),
                TestContext.Current.CancellationToken)).Status);

        RecipeSubmissionReviewResult accepted = await fixture.Submissions.AcceptAsync(
            comparison.SubmissionId, fixture.UserId, fixture.Now.AddMinutes(4),
            acknowledgeWarnings: true, "Lokale Verbesserung übernommen",
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeSubmissionReviewStatus.Accepted, accepted.Status);
        Assert.Equal(3, accepted.ResultingRevision!.RevisionNumber);
        Assert.Equal("Verbesserung", (await fixture.Drafts.FindAsync(
            comparison.CentralRecipeId, TestContext.Current.CancellationToken))!.Name);
        Assert.Empty(await fixture.Submissions.ListPendingAsync(TestContext.Current.CancellationToken));
        Assert.Equal(RecipeSubmissionReviewStatus.AlreadyReviewed,
            (await fixture.Submissions.RejectAsync(
                comparison.SubmissionId, fixture.UserId, fixture.Now.AddMinutes(5),
                TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Pending_submission_can_be_rejected_without_creating_a_central_revision()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid centralRevision = await fixture.PublishAsync(RecipeScopeType.Central, null, "Zentral");
        RecipeLibraryMutationResult entry = await fixture.Libraries.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), fixture.TenantId, centralRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        Guid localRecipeId = Guid.NewGuid();
        await fixture.Libraries.ConvertTenantEntryToLocalRecipeAsync(
            entry.EntryId!.Value, localRecipeId, "Nicht übernehmen", fixture.UserId,
            fixture.Now.AddMinutes(1), TestContext.Current.CancellationToken);
        Guid localRevision = await fixture.PublishCurrentAsync(localRecipeId);
        RecipeSubmissionResult submitted = await fixture.Submissions.SubmitAsync(
            Guid.NewGuid(), localRevision, fixture.UserId, fixture.Now.AddMinutes(2),
            TestContext.Current.CancellationToken);

        RecipeSubmissionReviewResult rejected = await fixture.Submissions.RejectAsync(
            submitted.SubmissionId!.Value, fixture.UserId, fixture.Now.AddMinutes(3),
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeSubmissionReviewStatus.Rejected, rejected.Status);
        Assert.Empty(await fixture.Submissions.ListPendingAsync(TestContext.Current.CancellationToken));
        Assert.Equal(RecipeSubmissionReviewStatus.AlreadyReviewed,
            (await fixture.Submissions.AcceptAsync(
                submitted.SubmissionId.Value, fixture.UserId, fixture.Now.AddMinutes(4), true,
                cancellationToken: TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Camp_notes_survive_revision_adoption_and_are_soft_deleted()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid firstRevision = await fixture.PublishAsync(RecipeScopeType.Central, null, "Zentral");
        RecipeLibraryMutationResult entry = await fixture.Libraries.AddUpstreamRevisionToCampAsync(
            Guid.NewGuid(), fixture.CampId, firstRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        CampRecipeNoteMutationResult first = await fixture.Notes.CreateAsync(
            Guid.NewGuid(), entry.EntryId!.Value, "Erste Notiz", fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        CampRecipeNoteMutationResult second = await fixture.Notes.CreateAsync(
            Guid.NewGuid(), entry.EntryId.Value, "Zweite Notiz", fixture.UserId,
            fixture.Now.AddMinutes(1), TestContext.Current.CancellationToken);

        Guid nextRevision = await fixture.PublishNextAsync(firstRevision);
        await fixture.Libraries.AdoptLatestCampRevisionAsync(
            entry.EntryId.Value, fixture.UserId, fixture.Now.AddMinutes(2),
            TestContext.Current.CancellationToken);
        Assert.Equal(CampRecipeNoteMutationStatus.NotFound,
            (await fixture.Notes.UpdateAsync(
                Guid.NewGuid(), first.Note!.Id, "Fremder Eintrag", fixture.UserId,
                fixture.Now.AddMinutes(3), TestContext.Current.CancellationToken)).Status);
        CampRecipeNoteMutationResult edited = await fixture.Notes.UpdateAsync(
            entry.EntryId.Value, first.Note.Id, "Überarbeitet", fixture.UserId,
            fixture.Now.AddMinutes(3), TestContext.Current.CancellationToken);
        CampRecipeNoteMutationResult deleted = await fixture.Notes.DeleteAsync(
            entry.EntryId.Value, second.Note!.Id, fixture.UserId, fixture.Now.AddMinutes(4),
            TestContext.Current.CancellationToken);

        CampRecipeNote remaining = Assert.Single(await fixture.Notes.ListAsync(
            entry.EntryId.Value, TestContext.Current.CancellationToken));
        Assert.Equal(nextRevision, (await fixture.Libraries.ListCampEntriesAsync(
            fixture.CampId, TestContext.Current.CancellationToken)).Single().SourceId);
        Assert.Equal(CampRecipeNoteMutationStatus.Updated, edited.Status);
        Assert.Equal(CampRecipeNoteMutationStatus.Deleted, deleted.Status);
        Assert.Equal("Überarbeitet", remaining.Text);
        Assert.Equal(fixture.Now, remaining.CreatedAtUtc);
        Assert.Equal(fixture.Now.AddMinutes(3), remaining.UpdatedAtUtc);
    }

    private sealed class DatabaseFixture(
        SqliteConnection connection,
        CateringDbContext database,
        RecipeDraftStore drafts,
        RecipePublisher publisher,
        RecipeLibraryStore libraries,
        RecipeChangeSubmissionStore submissions,
        CampRecipeNoteStore notes) : IAsyncDisposable
    {
        private readonly Dictionary<Guid, Guid> revisionRecipes = [];
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid CampId { get; } = Guid.NewGuid();
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public RecipeLibraryStore Libraries { get; } = libraries;
        public RecipeDraftStore Drafts { get; } = drafts;
        public RecipeChangeSubmissionStore Submissions { get; } = submissions;
        public CampRecipeNoteStore Notes { get; } = notes;

        public static async Task<DatabaseFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new CateringDbContext(
                new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var drafts = new RecipeDraftStore(database);
            var references = new EfRecipeReferences(database);
            var publisher = new RecipePublisher(
                database, drafts, new RecipePublicationValidator(references),
                new RecipeSnapshotBuilder(references));
            return new DatabaseFixture(
                connection, database, drafts,
                publisher,
                new RecipeLibraryStore(database, drafts),
                new RecipeChangeSubmissionStore(database, drafts, publisher),
                new CampRecipeNoteStore(database));
        }

        public async Task<Guid> PublishAsync(RecipeScopeType scope, Guid? scopeId, string name)
        {
            Guid ingredientId = Guid.NewGuid();
            Guid unitId = Guid.NewGuid();
            IngredientScopeType ingredientScope = scope switch
            {
                RecipeScopeType.Central => IngredientScopeType.Central,
                RecipeScopeType.Tenant => IngredientScopeType.Tenant,
                RecipeScopeType.Camp => IngredientScopeType.Camp,
                _ => throw new ArgumentOutOfRangeException(nameof(scope)),
            };
            database.AddRange(
                new BaseIngredient(ingredientId, ingredientScope, scopeId, $"Zutat {name}"),
                new MeasurementUnit(unitId, $"Gramm {name}", $"g{name[0]}", MeasurementDimension.Mass, 1m),
                new IngredientUnitConversion(ingredientId, unitId, 1m));
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            var draft = new RecipeDraft(Guid.NewGuid(), scope, scopeId, RecipeType.PortionBased, name);
            draft.SetDetails("Beschreibung", "Quelle", null);
            draft.ConfigurePortionReference(10m, true);
            draft.AddIngredientPosition(new RecipeIngredientPosition(
                Guid.NewGuid(), draft.Id, null, ingredientId, 1_000m, unitId, 0));
            await Drafts.CreateAsync(draft, UserId, Now, TestContext.Current.CancellationToken);
            RecipePublicationResult result = await publisher.PublishAsync(
                draft.Id, 0, UserId, Now, acknowledgeWarnings: true,
                cancellationToken: TestContext.Current.CancellationToken);
            revisionRecipes[result.Revision!.Id] = draft.Id;
            return result.Revision!.Id;
        }

        public async Task<Guid> PublishNextAsync(Guid currentRevisionId)
        {
            Guid recipeId = revisionRecipes[currentRevisionId];
            RecipeDraft draft = Assert.IsType<RecipeDraft>(await Drafts.FindAsync(
                recipeId, TestContext.Current.CancellationToken));
            RecipePublicationResult result = await publisher.PublishAsync(
                recipeId, draft.DraftVersion, UserId, Now.AddMinutes(1), acknowledgeWarnings: true,
                cancellationToken: TestContext.Current.CancellationToken);
            revisionRecipes[result.Revision!.Id] = recipeId;
            return result.Revision.Id;
        }

        public async Task<Guid> PublishCurrentAsync(Guid recipeId)
        {
            RecipeDraft draft = Assert.IsType<RecipeDraft>(await Drafts.FindAsync(
                recipeId, TestContext.Current.CancellationToken));
            RecipePublicationResult result = await publisher.PublishAsync(
                recipeId, draft.DraftVersion, UserId, Now.AddMinutes(1), acknowledgeWarnings: true,
                cancellationToken: TestContext.Current.CancellationToken);
            revisionRecipes[result.Revision!.Id] = recipeId;
            return result.Revision.Id;
        }

        public async Task<(Guid? CentralRecipe, Guid? CentralRevision, Guid? TenantRecipe, Guid? TenantRevision)>
            ReadLineageAsync(Guid recipeId)
        {
            await using var command = database.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                "SELECT CentralSourceRecipeId, CentralSourceRevisionId, TenantSourceRecipeId, " +
                "TenantSourceRevisionId FROM Recipes WHERE Id = $id";
            command.Parameters.Add(new SqliteParameter("$id", recipeId));
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            return (ReadGuid(reader, 0), ReadGuid(reader, 1), ReadGuid(reader, 2), ReadGuid(reader, 3));
        }

        private static Guid? ReadGuid(DbDataReader reader, int ordinal) =>
            reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
