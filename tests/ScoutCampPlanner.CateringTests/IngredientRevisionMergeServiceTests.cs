using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientRevisionMergeServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CategoryA = Guid.NewGuid();
    private static readonly Guid CategoryB = Guid.NewGuid();
    private static readonly Guid UnitId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 16, 0, 0, TimeSpan.Zero);
    private static readonly IngredientRevisionPublicationValidator Validator = new([]);
    private readonly IngredientRevisionMergeService service = new();

    [Fact]
    public void Newer_central_revision_is_detected_without_overwriting_local_revision()
    {
        Scenario scenario = CreateScenario();

        Assert.True(service.HasCentralUpdate(scenario.Fork, scenario.Base, scenario.Remote));
        Assert.Equal("Lagerbutter", scenario.Local.Name);
        Assert.Equal(scenario.Local.Id, scenario.Fork.CurrentPublishedRevisionId);
        Assert.DoesNotContain(scenario.Fork.Revisions, value => value.Id == scenario.Remote.Id);
    }

    [Fact]
    public void Non_overlapping_changes_create_merged_local_draft()
    {
        Scenario scenario = CreateScenario();

        IngredientMergeResult result = service.MergeIntoNewDraft(
            scenario.Fork,
            scenario.Base,
            scenario.Local,
            scenario.Remote,
            Guid.NewGuid(),
            UserId,
            Now.AddDays(3));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Draft);
        Assert.Equal("Lagerbutter", result.Draft.Name);
        Assert.Equal(CategoryB, result.Draft.CategoryId);
        Assert.Equal(scenario.Local.Id, result.Draft.BasedOnRevisionId);
        Assert.Equal(scenario.Remote.Id, result.Draft.MergedCentralRevisionId);
        Assert.Equal(IngredientRevisionState.Published, scenario.Local.State);
    }

    [Fact]
    public void Overlapping_scalar_changes_create_conflict_and_no_draft()
    {
        Scenario scenario = CreateScenario(remoteName: "Zentrale Markenbutter");

        IngredientMergeResult result = service.MergeIntoNewDraft(
            scenario.Fork,
            scenario.Base,
            scenario.Local,
            scenario.Remote,
            Guid.NewGuid(),
            UserId,
            Now.AddDays(3));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Draft);
        Assert.Contains(result.Conflicts, value => value.Path == "name");
        Assert.DoesNotContain(scenario.Fork.Revisions, value => value.State == IngredientRevisionState.Draft);
    }

    [Fact]
    public void Non_overlapping_property_entries_are_combined()
    {
        Guid localAllergen = Guid.NewGuid();
        Guid remoteAllergen = Guid.NewGuid();
        Scenario scenario = CreateScenario(
            configureLocal: revision => revision.SetAllergen(
                Value(localAllergen, IngredientPropertyState.Contains), UserId, Now),
            configureRemote: revision => revision.SetAllergen(
                Value(remoteAllergen, IngredientPropertyState.MayContain), UserId, Now));

        IngredientMergeResult result = service.MergeIntoNewDraft(
            scenario.Fork,
            scenario.Base,
            scenario.Local,
            scenario.Remote,
            Guid.NewGuid(),
            UserId,
            Now.AddDays(3));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Draft!.Allergens.Count);
        Assert.Contains(result.Draft.Allergens, value => value.PropertyId == localAllergen);
        Assert.Contains(result.Draft.Allergens, value => value.PropertyId == remoteAllergen);
    }

    [Fact]
    public void Different_changes_to_same_property_create_conflict()
    {
        Guid allergen = Guid.NewGuid();
        Scenario scenario = CreateScenario(
            configureLocal: revision => revision.SetAllergen(
                Value(allergen, IngredientPropertyState.Contains), UserId, Now),
            configureRemote: revision => revision.SetAllergen(
                Value(allergen, IngredientPropertyState.DoesNotContain), UserId, Now));

        IngredientMergeResult result = service.MergeIntoNewDraft(
            scenario.Fork,
            scenario.Base,
            scenario.Local,
            scenario.Remote,
            Guid.NewGuid(),
            UserId,
            Now.AddDays(3));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Conflicts, value => value.Path == $"allergens.{allergen}");
    }

    [Fact]
    public void Non_overlapping_unit_conversions_are_combined()
    {
        Guid localUnit = Guid.NewGuid();
        Guid remoteUnit = Guid.NewGuid();
        Scenario scenario = CreateScenario(
            configureLocal: revision => revision.SetUnitConversion(
                new IngredientRevisionUnitConversion(localUnit, 10m, IngredientConversionPrecision.Average),
                UserId,
                Now),
            configureRemote: revision => revision.SetUnitConversion(
                new IngredientRevisionUnitConversion(remoteUnit, 20m, IngredientConversionPrecision.Estimated),
                UserId,
                Now));

        IngredientMergeResult result = service.MergeIntoNewDraft(
            scenario.Fork,
            scenario.Base,
            scenario.Local,
            scenario.Remote,
            Guid.NewGuid(),
            UserId,
            Now.AddDays(3));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Draft!.UnitConversions.Count);
    }

    [Fact]
    public void Different_changes_to_same_unit_conversion_create_conflict()
    {
        Guid unit = Guid.NewGuid();
        Scenario scenario = CreateScenario(
            configureLocal: revision => revision.SetUnitConversion(
                new IngredientRevisionUnitConversion(unit, 10m, IngredientConversionPrecision.Average),
                UserId,
                Now),
            configureRemote: revision => revision.SetUnitConversion(
                new IngredientRevisionUnitConversion(unit, 20m, IngredientConversionPrecision.Estimated),
                UserId,
                Now));

        IngredientMergeResult result = service.MergeIntoNewDraft(
            scenario.Fork,
            scenario.Base,
            scenario.Local,
            scenario.Remote,
            Guid.NewGuid(),
            UserId,
            Now.AddDays(3));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Conflicts, value => value.Path == $"unit_conversions.{unit}");
    }

    [Fact]
    public void Independent_local_ingredient_cannot_receive_central_update()
    {
        Scenario scenario = CreateScenario();
        IngredientIdentity independent = IngredientIdentity.CreateLocal(
            Guid.NewGuid(), IngredientScopeType.Tenant, Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => service.HasCentralUpdate(
            independent, scenario.Base, scenario.Remote));
    }

    private static Scenario CreateScenario(
        string? remoteName = null,
        Action<IngredientRevision>? configureLocal = null,
        Action<IngredientRevision>? configureRemote = null)
    {
        IngredientIdentity central = IngredientIdentity.CreateCentral(Guid.NewGuid());
        IngredientRevision baseRevision = central.CreateDraft(
            Guid.NewGuid(), "Butter", CategoryA, UnitId, UserId, Now);
        Review(baseRevision);
        central.PublishDraft(baseRevision.Id, UserId, Now, Validator);

        IngredientIdentity fork = IngredientIdentity.ForkCentral(
            Guid.NewGuid(),
            IngredientScopeType.Tenant,
            Guid.NewGuid(),
            central,
            baseRevision,
            Guid.NewGuid(),
            UserId,
            Now.AddDays(1));
        IngredientRevision local = Assert.Single(fork.Revisions);
        local.SetContent("Lagerbutter", CategoryA, UnitId, UserId, Now.AddDays(1));
        configureLocal?.Invoke(local);
        Review(local);
        fork.PublishDraft(local.Id, UserId, Now.AddDays(1), Validator);

        IngredientRevision remote = central.CreateDraftFromPublished(
            Guid.NewGuid(), UserId, Now.AddDays(2));
        remote.SetContent(remoteName ?? "Butter", CategoryB, UnitId, UserId, Now.AddDays(2));
        configureRemote?.Invoke(remote);
        Review(remote);
        central.PublishDraft(remote.Id, UserId, Now.AddDays(2), Validator);

        return new Scenario(fork, baseRevision, local, remote);
    }

    private static void Review(IngredientRevision revision) => revision.SetReviewStates(
        IngredientPropertyReviewState.Reviewed,
        IngredientPropertyReviewState.Reviewed,
        IngredientPropertyReviewState.Reviewed,
        UserId,
        Now);

    private static IngredientPropertyValue Value(Guid id, IngredientPropertyState state) =>
        new(id, state, IngredientPropertySource.ManuallyVerified);

    private sealed record Scenario(
        IngredientIdentity Fork,
        IngredientRevision Base,
        IngredientRevision Local,
        IngredientRevision Remote);
}
