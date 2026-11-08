using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Application.Recipes;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record SupplyOffer(Guid? OfferGroupId, Guid RecipeRevisionId, decimal Portions,
    IReadOnlyList<Guid> Alternatives);
public sealed record SupplyRequirement(RequirementSignature Requirements, decimal Portions);
public sealed record CommonSupplyChoice(Guid? OfferGroupId, IReadOnlyList<string> RequirementGroupKeys,
    IReadOnlyList<SupplySolution> TogetherOptions, IReadOnlyList<SupplySolution> SeparateSolutions,
    SupplyScenario? TogetherScenario = null, SupplyScenario? SeparateScenario = null);
public sealed record OperationalSupplyDocument(IReadOnlyList<SupplySolution> Solutions,
    IReadOnlyList<SupplySolution> Candidates, IReadOnlyList<MealPlanningProblem> Problems,
    IReadOnlyList<SupplyRequirement>? Requirements = null, IReadOnlyList<SupplyQuantity>? Quantities = null,
    IReadOnlyList<CommonSupplyChoice>? CommonSupplyChoices = null,
    IReadOnlyList<SupplyDecisionGroup>? Decisions = null,
    IReadOnlyList<CommonSupplyChoice>? PreferenceOptions = null);
public sealed record SupplyQuantity(Guid SolutionId, string PositionPath, string IngredientName,
    decimal Quantity, string Unit, ScalingMode ScalingMode, StepwiseScaling? StepwiseScaling);
public enum MealVerificationKind { Clean, VerifiedWithDeviation }
public sealed record MealVerificationSnapshot(Guid CookingUnitId, Guid CampMealId, Guid? MealPlanId,
    int? MealPlanVersion, Guid? MealPlanSnapshotId, EffectiveDemandBasis DemandBasis, decimal? Demand,
    IReadOnlyList<RequirementGroup> RequirementGroups, OperationalSupplyDocument Supply,
    MealVerificationKind Kind, string? Reason, Guid VerifiedBy, DateTimeOffset VerifiedAt);
public sealed record SaveSupplyRequest(long ExpectedVersion, IReadOnlyList<SupplySolution> Solutions,
    IReadOnlyList<SupplyScenarioSelection>? ScenarioSelections = null, bool? CompleteOwnSupply = null);
public sealed record VerifyMealRequest(long ExpectedVersion, MealVerificationKind Kind, string? Reason);
