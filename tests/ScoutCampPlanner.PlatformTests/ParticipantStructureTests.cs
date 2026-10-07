using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed partial class CampManagementServiceTests
{
    [Fact]
    public async Task Camp_owns_leaf_assignment_and_catering_projection_excludes_names_and_metadata()
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, participants) = await ParticipantFixtureAsync();
        Guid root = Guid.NewGuid(), leaf = Guid.NewGuid();
        camps.StructureNodes.AddRange(new StructureNode(root, campId, null, "Root"), new StructureNode(leaf, campId, root, "Leaf"));
        await camps.SaveChangesAsync(ct);
        Assert.Equal(ParticipantMutationStatus.Invalid, (await participants.SaveAsync(otherUserId, campId, null, null,
            DummyParticipant() with { StructureNodeId = root }, ct)).Status);
        Assert.Equal(ParticipantMutationStatus.Success, (await participants.SaveAsync(otherUserId, campId, null, null,
            DummyParticipant() with { StructureNodeId = leaf, AbsentDays = [new(2027, 7, 1)] }, ct)).Status);
        var meal = await catering.CampMeals.FirstAsync(value => value.CampId == campId, ct);
        var projection = Assert.Single(await camps.GetMealParticipantsAsync(campId, new(2027, 7, 1), meal.Id, ct));
        Assert.Equal(leaf, projection.StructureNodeId); Assert.False(projection.IsPresent);
        Assert.DoesNotContain("Dummy private name", JsonSerializer.Serialize(projection));
        Assert.DoesNotContain("DisplayName", JsonSerializer.Serialize(projection));
        Assert.DoesNotContain("ThresholdSource", JsonSerializer.Serialize(projection));
        Assert.Equal(CreateStructureNodeFailure.HasEstimates,
            (await service.CreateStructureNodeAsync(otherUserId, campId, new(leaf, "Forbidden child"), ct)).Failure);
        Assert.Equal(DeleteStructureNodeFailure.HasEstimates,
            await service.DeleteStructureNodeAsync(otherUserId, campId, leaf, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_converts_only_unique_leaf_and_preserves_ambiguous_input(bool ambiguous)
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, participants) = await ParticipantFixtureAsync();
        Guid root = Guid.NewGuid(), leaf = Guid.NewGuid(), unit = Guid.NewGuid();
        camps.StructureNodes.AddRange(new StructureNode(root, campId, null, "Root"), new StructureNode(leaf, campId, root, "Leaf"));
        if (ambiguous) camps.StructureNodes.Add(new StructureNode(Guid.NewGuid(), campId, root, "Second leaf"));
        await camps.SaveChangesAsync(ct);
        var created = await participants.SaveAsync(otherUserId, campId, null, null, DummyParticipant(), ct);
        catering.CookingUnits.Add(new CookingUnit(unit, campId, "Kitchen", 0));
        catering.CookingUnitStructureAssignments.Add(new(Guid.NewGuid(), campId, unit, null, root));
        await catering.SaveChangesAsync(ct);
        var json = JsonSerializer.Serialize(new CookingUnitParticipantAssignments[] { new(unit, [created.ParticipantId!.Value], []) }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await new MealPlanningStore(catering).SaveParticipantConfigurationAsync(campId, 0, MealPlanningDemandMode.UseActualParticipants, json, ct);
        await ParticipantStructureMigration.RunAsync(camps, catering, NullLogger.Instance, ct);
        camps.ChangeTracker.Clear(); catering.ChangeTracker.Clear();
        var person = await camps.Participants.SingleAsync(ct);
        var configuration = await catering.Set<MealPlanningParticipantConfiguration>().SingleAsync(ct);
        Assert.Equal(ambiguous ? null : (Guid?)leaf, person.StructureNodeId);
        Assert.Equal(ambiguous ? json : "[]", configuration.AssignmentsJson);
    }
}
