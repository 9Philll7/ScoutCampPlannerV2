namespace ScoutCampPlanner.Camp.Contracts;

public sealed record ParticipantIntoleranceData(
    Guid SubstanceId, decimal? ThresholdGramsPerPortion, string? ThresholdSource);

public sealed record ParticipantPlanningData(
    Guid Id, Guid CampId, string DisplayName, Guid? DietTypeId,
    IReadOnlyList<DateOnly> AbsentDays, IReadOnlyList<Guid> AbsentMealIds,
    IReadOnlyList<Guid> AllergenIds, IReadOnlyList<ParticipantIntoleranceData> Intolerances,
    Guid? StructureNodeId = null);

/// <summary>Data-minimized meal projection. No names, source metadata or health dossier.</summary>
public sealed record CateringParticipantData(Guid ParticipantReference, Guid? StructureNodeId,
    bool IsPresent, Guid? DietTypeId, IReadOnlyList<Guid> AllergenIds,
    IReadOnlyList<CateringIntoleranceData> Intolerances)
{
    public bool RequiresSpecialCatering => DietTypeId.HasValue || AllergenIds.Count > 0 || Intolerances.Count > 0;
}
public sealed record CateringIntoleranceData(Guid SubstanceId, decimal? ThresholdGramsPerPortion);
public interface ICampCateringParticipantLookup
{
    Task<IReadOnlyList<CateringParticipantData>> GetMealParticipantsAsync(Guid campId,
        DateOnly date, Guid mealId, CancellationToken cancellationToken = default);
}

/// <summary>Read after health authorization. Never expose this through the ordinary camp.view projection.</summary>
public interface ICampParticipantLookup
{
    Task<IReadOnlyList<ParticipantPlanningData>> GetParticipantsAsync(
        Guid campId, CancellationToken cancellationToken = default);
}
