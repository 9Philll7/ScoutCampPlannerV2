using System.Globalization;

namespace ScoutCampPlanner.Catering.Domain;

public enum MealPlanningDemandMode { EstimatedPlanning, UseActualParticipants }
public enum EffectiveDemandBasis { Estimated, ActualParticipants, EstimatedFallback }
public enum CookingTogetherPreference { AlwaysTogether, Ask, NeverTogether }
public enum MealPlanningProblemCategory { Suitability, Coverage, DataQuality, ManualDecision }
public enum MealPlanningProblemCode
{
    NoMatchingDirectSolution, MultipleDirectSolutions, PartialDirectResolution,
    NoMatchingOfferAlternative, MultipleMatchingOfferAlternatives,
    UnknownSubstanceData, QualitativeOnlySubstanceData, MissingThreshold,
    SubstanceThresholdExceeded, SubstancePresentWithoutThreshold, AllergenConflict, UnknownAllergenData,
    DietTypeExcludedOrigin, UnknownDietTypeRule, ManualDecisionNoLongerSuitable,
    RequirementGroupUndercovered, RequirementGroupOvercovered, UnassignedPlannedPortions,
    UnassignedParticipant,
}

public sealed record MealPlanningProblem(MealPlanningProblemCategory Category,
    MealPlanningProblemCode Code, string? RequirementGroupKey = null,
    Guid? ReferenceId = null, string? PositionPath = null);

public sealed record IntoleranceRequirement
{
    public IntoleranceRequirement(Guid substanceId, decimal? thresholdGramsPerPortion)
    {
        if (substanceId == Guid.Empty) throw new ArgumentException("Substance ID is required.", nameof(substanceId));
        if (thresholdGramsPerPortion is < 0) throw new ArgumentOutOfRangeException(nameof(thresholdGramsPerPortion));
        SubstanceId = substanceId;
        ThresholdGramsPerPortion = thresholdGramsPerPortion;
    }
    public Guid SubstanceId { get; }
    public decimal? ThresholdGramsPerPortion { get; }
}

/// <summary>Calculation identity only. Names, source metadata and participant identities cannot split groups.</summary>
public sealed class RequirementSignature
{
    public RequirementSignature(Guid? dietTypeId, IEnumerable<Guid> allergenIds,
        IEnumerable<IntoleranceRequirement> intolerances)
    {
        if (dietTypeId == Guid.Empty) throw new ArgumentException("Diet type ID is invalid.", nameof(dietTypeId));
        ArgumentNullException.ThrowIfNull(allergenIds);
        ArgumentNullException.ThrowIfNull(intolerances);
        Guid[] allergens = allergenIds.Distinct().Order().ToArray();
        if (allergens.Contains(Guid.Empty)) throw new ArgumentException("Allergen ID is required.", nameof(allergenIds));
        IntoleranceRequirement[] requirements = intolerances.ToArray();
        if (requirements.Any(value => value is null) || requirements.Select(value => value.SubstanceId).Distinct().Count() != requirements.Length)
            throw new ArgumentException("Intolerance references must be unique.", nameof(intolerances));
        DietTypeId = dietTypeId;
        AllergenIds = Array.AsReadOnly(allergens);
        Intolerances = Array.AsReadOnly(requirements.OrderBy(value => value.SubstanceId).ToArray());
        Key = $"diet:{dietTypeId?.ToString("N") ?? "-"}|allergens:{string.Join(",", allergens.Select(id => id.ToString("N")))}|substances:" +
            string.Join(",", Intolerances.Select(value => $"{value.SubstanceId:N}={value.ThresholdGramsPerPortion?.ToString("G29", CultureInfo.InvariantCulture) ?? "?"}"));
    }
    public Guid? DietTypeId { get; }
    public IReadOnlyList<Guid> AllergenIds { get; }
    public IReadOnlyList<IntoleranceRequirement> Intolerances { get; }
    public string Key { get; }
}

public sealed record RequirementGroup(RequirementSignature Requirements, int Portions);

public sealed record MealDemandBasisResult(EffectiveDemandBasis Basis, decimal? Demand, bool Complete);

public static class MealDemandBasisPolicy
{
    public static MealDemandBasisResult Resolve(MealPlanningDemandMode mode, int realContextParticipantCount,
        int presentAssignedCount, int presentUnassignedCount, decimal? estimatedDemand)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (realContextParticipantCount < 0 || presentAssignedCount < 0 || presentUnassignedCount < 0 ||
            (long)presentAssignedCount + presentUnassignedCount > realContextParticipantCount)
            throw new ArgumentException("Participant counts are inconsistent.");
        if (estimatedDemand is < 0) throw new ArgumentOutOfRangeException(nameof(estimatedDemand));
        if (mode == MealPlanningDemandMode.EstimatedPlanning)
            return new(EffectiveDemandBasis.Estimated, estimatedDemand, estimatedDemand.HasValue);
        if (realContextParticipantCount == 0)
            return new(EffectiveDemandBasis.EstimatedFallback, estimatedDemand, estimatedDemand.HasValue);
        return new(EffectiveDemandBasis.ActualParticipants, presentAssignedCount, presentUnassignedCount == 0);
    }
}

public enum IngredientSubstanceAssessmentMode { Quantitative, Qualitative, Unknown }

/// <summary>One already scaled effective position contribution, expressed in grams per solution portion.</summary>
public sealed record SubstancePortionContribution
{
    private SubstancePortionContribution(string positionPath, IngredientSubstanceAssessmentMode mode,
        decimal? grams, bool? contained)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionPath);
        PositionPath = positionPath;
        Mode = mode;
        Grams = grams;
        Contained = contained;
    }
    public string PositionPath { get; }
    public IngredientSubstanceAssessmentMode Mode { get; }
    public decimal? Grams { get; }
    public bool? Contained { get; }
    public static SubstancePortionContribution Quantitative(string path, decimal grams) => grams < 0
        ? throw new ArgumentOutOfRangeException(nameof(grams)) : new(path, IngredientSubstanceAssessmentMode.Quantitative, grams, null);
    public static SubstancePortionContribution Qualitative(string path, bool contained) => new(path, IngredientSubstanceAssessmentMode.Qualitative, null, contained);
    public static SubstancePortionContribution Unknown(string path) => new(path, IngredientSubstanceAssessmentMode.Unknown, null, null);
}

public sealed record SubstancePortionAssessment(decimal? ExactGramsPerPortion,
    IReadOnlyList<MealPlanningProblem> Problems)
{
    public bool IsSuitable => Problems.Count == 0;
}

public static class SubstancePortionEvaluator
{
    public static SubstancePortionAssessment Evaluate(IntoleranceRequirement requirement,
        IEnumerable<SubstancePortionContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(contributions);
        SubstancePortionContribution[] values = contributions.ToArray();
        if (values.Any(value => value is null)) throw new ArgumentException("Null contribution.", nameof(contributions));
        var problems = new List<MealPlanningProblem>();
        foreach (var value in values.Where(value => value.Mode == IngredientSubstanceAssessmentMode.Unknown))
            problems.Add(new(MealPlanningProblemCategory.DataQuality, MealPlanningProblemCode.UnknownSubstanceData,
                ReferenceId: requirement.SubstanceId, PositionPath: value.PositionPath));
        foreach (var value in values.Where(value => value.Contained == true))
            problems.Add(new(MealPlanningProblemCategory.DataQuality, MealPlanningProblemCode.QualitativeOnlySubstanceData,
                ReferenceId: requirement.SubstanceId, PositionPath: value.PositionPath));
        if (!requirement.ThresholdGramsPerPortion.HasValue)
            problems.Add(new(MealPlanningProblemCategory.DataQuality, MealPlanningProblemCode.MissingThreshold,
                ReferenceId: requirement.SubstanceId));
        // An empty projection is missing data, not proof of absence.
        if (values.Length == 0)
            problems.Add(new(MealPlanningProblemCategory.DataQuality, MealPlanningProblemCode.UnknownSubstanceData,
                ReferenceId: requirement.SubstanceId));
        bool exact = values.Length > 0 && values.All(value =>
            value.Mode == IngredientSubstanceAssessmentMode.Quantitative || value.Contained == false);
        decimal? total = exact ? values.Sum(value => value.Grams ?? 0m) : null;
        if (total.HasValue && (requirement.ThresholdGramsPerPortion.HasValue
                ? total.Value > requirement.ThresholdGramsPerPortion.Value : total.Value > 0))
            problems.Add(new(MealPlanningProblemCategory.Suitability, requirement.ThresholdGramsPerPortion.HasValue
                    ? MealPlanningProblemCode.SubstanceThresholdExceeded
                    : MealPlanningProblemCode.SubstancePresentWithoutThreshold,
                ReferenceId: requirement.SubstanceId));
        return new(total, problems.AsReadOnly());
    }
}
