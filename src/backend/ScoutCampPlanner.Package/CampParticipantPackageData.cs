using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Catering.Infrastructure.Offline;
using ScoutCampPlanner.Catering.Application.MealPlanning;

namespace ScoutCampPlanner.Package;

/// <summary>Development transport only. This marker is not a production security mechanism.</summary>
public sealed record CampParticipantPackageData(int SchemaVersion, bool DummyDataOnly,
    IReadOnlyList<ParticipantPlanningData> Items, IReadOnlyList<ParticipantDietReference> DietTypes);

public sealed record ParticipantDietReference(Guid Id, string Name, Guid? TenantId = null,
    string? Description = null, int SortOrder = 0, int Version = 0, IReadOnlyList<DietaryOriginRuleData>? Rules = null);

/// <summary>
/// Called inside the package transaction. The composition root must enforce explicit rights.
/// Importing into a new local camp stores protected data, but never grants permission to read it.
/// </summary>
public interface ICampPackageParticipantAccess
{
    Task DemandReadAsync(Guid campId, CancellationToken cancellationToken);
    Task DemandEditAsync(Guid campId, CancellationToken cancellationToken);
}

public sealed class CampPackageParticipantAccessException : Exception
{
    public CampPackageParticipantAccessException() : base("Explicit participant permission is required.") { }
}

internal static class CampParticipantPackageValidation
{
    public static void Validate(CampPackagePayload package)
    {
        if (package.Participants is not { } data) return; // Pre-increment-2 packages remain readable.
        if (data.SchemaVersion is not (1 or 2) || !data.DummyDataOnly || data.Items is null || data.DietTypes is null ||
            data.Items.Any(value => value is null) || data.DietTypes.Any(value => value is null) ||
            data.Items.Select(value => value.Id).Distinct().Count() != data.Items.Count ||
            data.DietTypes.Select(value => value.Id).Distinct().Count() != data.DietTypes.Count ||
            data.DietTypes.Any(value => value.Id == Guid.Empty || string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 100 ||
                value.TenantId.HasValue && value.TenantId != package.Camp.TenantId || value.SortOrder < 0 || value.Version < 0 ||
                value.Description?.Length > 2000 || (value.Rules ?? []).Any(rule => rule is null || rule.OriginId == Guid.Empty || !Enum.IsDefined(rule.Decision)) ||
                (value.Rules ?? []).Select(rule => rule.OriginId).Distinct().Count() != (value.Rules ?? []).Count))
            throw Invalid();
        var diets = data.DietTypes.Select(value => value.Id).ToHashSet();
        var references = CampOfflineReferenceStore.ReadParticipantRequirementIds(package.CateringReferenceData);
        var meals = (package.CampMeals ?? []).Select(value => value.Id).ToHashSet();
        foreach (var item in data.Items)
        {
            if (item.StructureNodeId is Guid node && (!package.StructureNodes.Any(value => value.Id == node) ||
                package.StructureNodes.Any(value => value.ParentId == node))) throw Invalid();
            if (item.CampId != package.Camp.Id || item.AbsentDays is null || item.AbsentMealIds is null ||
                item.AllergenIds is null || item.Intolerances is null || item.Intolerances.Any(value => value is null) ||
                item.AbsentDays.Distinct().Count() != item.AbsentDays.Count ||
                item.AbsentMealIds.Distinct().Count() != item.AbsentMealIds.Count ||
                item.AllergenIds.Distinct().Count() != item.AllergenIds.Count ||
                item.AllergenIds.Any(value => !references.Allergens.Contains(value)) ||
                item.Intolerances.Any(value => !references.Substances.Contains(value.SubstanceId)) ||
                item.AbsentDays.Any(value => value < package.Camp.StartDate || value > package.Camp.EndDate) ||
                item.AbsentMealIds.Any(value => !meals.Contains(value)) ||
                item.DietTypeId is Guid diet && !diets.Contains(diet)) throw Invalid();
            try { _ = Restore(item); }
            catch (ArgumentException) { throw Invalid(); }
        }
    }

    internal static Participant Restore(ParticipantPlanningData item)
    {
        var participant = new Participant(item.Id, item.CampId, item.DisplayName);
        participant.AssignStructureNode(item.StructureNodeId);
        participant.SetRequirements(item.DietTypeId, item.AllergenIds, item.Intolerances.Select(value =>
            new ParticipantIntolerance(value.SubstanceId, value.ThresholdGramsPerPortion, value.ThresholdSource)));
        foreach (var date in item.AbsentDays) participant.SetDayAbsent(date, true);
        foreach (var meal in item.AbsentMealIds) participant.SetMealAbsent(meal, true);
        return participant;
    }

    private static CampPackageValidationException Invalid() => new("Participant package data is invalid.");
}
