using System.Text.Json;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record SupplyDecisionGroup(string Id, Guid? OfferGroupId, IReadOnlyList<string> RequirementGroupKeys,
    IReadOnlyList<SupplySolution> Options, bool Together);
public sealed record SupplyScenario(IReadOnlyList<SupplySolution> Solutions, IReadOnlyList<SupplyDecisionGroup> Decisions,
    IReadOnlyList<MealPlanningProblem> Problems);
public sealed record SupplyScenarioSelection(Guid? OfferGroupId, CookingTogetherPreference Preference);
public sealed record ManualSupplyDocument(IReadOnlyList<SupplySolution> Solutions, IReadOnlyList<SupplyScenarioSelection> ScenarioSelections, bool CompleteOwnSupply = false, CookingUnitParticipantFilter? ParticipantFilterOverride = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static ManualSupplyDocument Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var value = document.RootElement.ValueKind == JsonValueKind.Array
            ? new ManualSupplyDocument(document.RootElement.Deserialize<SupplySolution[]>(JsonOptions) ?? [], [])
            : document.RootElement.Deserialize<ManualSupplyDocument>(JsonOptions) ?? throw new InvalidOperationException("Missing supply decisions.");
        if (value.ParticipantFilterOverride.HasValue && !Enum.IsDefined(value.ParticipantFilterOverride.Value)) throw new InvalidOperationException("Invalid meal participant filter.");
        if (value.Solutions is null || value.ScenarioSelections is null || value.ScenarioSelections.Any(selection => selection is null ||
            !Enum.IsDefined(selection.Preference) || selection.OfferGroupId == Guid.Empty) ||
            value.ScenarioSelections.Select(selection => selection.OfferGroupId).Distinct().Count() != value.ScenarioSelections.Count)
            throw new InvalidOperationException("Invalid supply scenario decisions.");
        return value;
    }
    public string Serialize() => !CompleteOwnSupply && ParticipantFilterOverride is null && ScenarioSelections.Count == 0 ? JsonSerializer.Serialize(Solutions, JsonOptions) : JsonSerializer.Serialize(this, JsonOptions);
}
