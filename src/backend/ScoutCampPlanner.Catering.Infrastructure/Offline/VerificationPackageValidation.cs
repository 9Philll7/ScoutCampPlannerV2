using System.Text.Json;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Offline;

public static class VerificationPackageValidation
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static bool ContainsProtectedSupply(MealPlanningPackageData data) => data.MealStates.Any(state =>
        state.DemandBasis == EffectiveDemandBasis.ActualParticipants || Documents(state).Any(value => value.DemandBasis == EffectiveDemandBasis.ActualParticipants) ||
        CampMealPlanningPackageStore.ReadSupplySolutions(state).Any(solution => solution.Allocations.Any(value =>
            value.RequirementGroupKey != new RequirementSignature(null, [], []).Key)));

    public static IEnumerable<RequirementSignature> Requirements(MealPlanningPackageData data) =>
        data.MealStates.SelectMany(state => Documents(state).SelectMany(document => document.RequirementGroups
            .Select(value => value.Requirements).Concat((document.Supply.Requirements ?? []).Select(value => value.Requirements))));

    private static IEnumerable<MealVerificationSnapshot> Documents(CookingUnitMealStatePackageRecord state)
    {
        if (state.VerificationSnapshotJson is not null)
            yield return JsonSerializer.Deserialize<MealVerificationSnapshot>(state.VerificationSnapshotJson, Options)
                ?? throw new InvalidDataException("Missing verification snapshot.");
    }

    public static void Validate(MealPlanningPackageData data, IReadOnlySet<Guid> diets,
        IReadOnlySet<Guid> allergens, IReadOnlySet<Guid> substances)
    {
        foreach (var state in data.MealStates)
        foreach (var snapshot in Documents(state))
        {
            if (snapshot.RequirementGroups is null || snapshot.Supply is null || !Enum.IsDefined(snapshot.DemandBasis) ||
                snapshot.Demand is < 0 || snapshot.VerifiedAt == default || snapshot.Reason?.Length > 2000)
                throw Invalid();
            if (snapshot.MealPlanId is Guid plan)
            {
                if (!data.MealPlans.Any(value => value.Id == plan) || !data.Snapshots.Any(value =>
                    value.Id == snapshot.MealPlanSnapshotId && value.MealPlanId == plan && value.Version == snapshot.MealPlanVersion))
                    throw new InvalidDataException("Verification meal-plan snapshot reference is missing.");
            }
            else if (snapshot.MealPlanSnapshotId is not null || snapshot.MealPlanVersion is not null) throw Invalid();
            if (snapshot.RequirementGroups.Any(value => value is null || value.Requirements is null || value.Portions < 0) ||
                snapshot.RequirementGroups.Select(value => value.Requirements.Key).Distinct().Count() != snapshot.RequirementGroups.Count)
                throw Invalid();
            var signatures = snapshot.RequirementGroups.Select(value => value.Requirements)
                .Concat((snapshot.Supply.Requirements ?? []).Select(value => value.Requirements)).ToArray();
            foreach (var signature in signatures)
                if (signature is null || signature.DietTypeId is Guid diet && !diets.Contains(diet) ||
                    signature.AllergenIds.Any(id => !allergens.Contains(id)) || signature.Intolerances.Any(value => !substances.Contains(value.SubstanceId)))
                    throw new InvalidDataException("Verification requirement catalogue reference is missing.");
            var keys = signatures.Select(value => value.Key).ToHashSet(StringComparer.Ordinal);
            if (snapshot.DemandBasis != EffectiveDemandBasis.ActualParticipants) keys.Add(new RequirementSignature(null, [], []).Key);
            foreach (var solution in snapshot.Supply.Solutions)
                if (solution.Allocations.Any(value => !keys.Contains(value.RequirementGroupKey)) &&
                    !snapshot.Supply.Problems.Any(value => value.Code == MealPlanningProblemCode.UnassignedPlannedPortions && value.ReferenceId == solution.Id))
                    throw new InvalidDataException("Verification allocation has no requirement signature or explicit coverage problem.");
            // Keys are derived in the domain constructor; do not silently accept a forged serialized key.
            using var json = JsonDocument.Parse(state.VerificationSnapshotJson!);
            CheckKeys(json.RootElement);
        }
    }

    private static void CheckKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array) { foreach (var item in element.EnumerateArray()) CheckKeys(item); }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("allergenIds", out _) && element.TryGetProperty("intolerances", out _))
            {
                var signature = element.Deserialize<RequirementSignature>(Options) ?? throw Invalid();
                if (!element.TryGetProperty("key", out var key) || key.GetString() != signature.Key) throw Invalid();
            }
            foreach (var property in element.EnumerateObject()) CheckKeys(property.Value);
        }
    }

    private static InvalidDataException Invalid() => new("Invalid verification snapshot references or signature.");
}
