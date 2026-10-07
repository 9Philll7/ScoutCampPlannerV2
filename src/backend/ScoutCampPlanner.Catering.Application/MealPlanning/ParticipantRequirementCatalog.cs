namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record ParticipantRequirementOption(Guid Id, string Code, string Name,
    bool IsQuantityDependent = false, Guid? ParentId = null,
    decimal? DefaultThresholdGramsPerPortion = null, string? DefaultThresholdSource = null, int DefaultThresholdVersion = 0);
public sealed record ParticipantRequirementCatalog(IReadOnlyList<ParticipantRequirementOption> Allergens,
    IReadOnlyList<ParticipantRequirementOption> Substances, IReadOnlyList<ParticipantRequirementOption> DietTypes);
