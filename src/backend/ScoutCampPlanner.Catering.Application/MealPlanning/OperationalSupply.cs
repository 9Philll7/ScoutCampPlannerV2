using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Application.Recipes;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record SupplyOffer(Guid? OfferGroupId, Guid RecipeRevisionId, decimal Portions,
    IReadOnlyList<Guid> Alternatives);
public sealed record SupplyRequirement(RequirementSignature Requirements, decimal Portions);
public sealed record CommonSupplyChoice(Guid? OfferGroupId, IReadOnlyList<string> RequirementGroupKeys,
    IReadOnlyList<SupplySolution> TogetherOptions, IReadOnlyList<SupplySolution> SeparateSolutions);
public sealed record OperationalSupplyDocument(IReadOnlyList<SupplySolution> Solutions,
    IReadOnlyList<SupplySolution> Candidates, IReadOnlyList<MealPlanningProblem> Problems,
    IReadOnlyList<SupplyRequirement>? Requirements = null, IReadOnlyList<SupplyQuantity>? Quantities = null,
    IReadOnlyList<CommonSupplyChoice>? CommonSupplyChoices = null);
public sealed record SupplyQuantity(Guid SolutionId, string PositionPath, string IngredientName,
    decimal Quantity, string Unit, ScalingMode ScalingMode, StepwiseScaling? StepwiseScaling);
public enum MealVerificationKind { Clean, VerifiedWithDeviation }
public sealed record MealVerificationSnapshot(Guid CookingUnitId, Guid CampMealId, Guid? MealPlanId,
    int? MealPlanVersion, Guid? MealPlanSnapshotId, EffectiveDemandBasis DemandBasis, decimal? Demand,
    IReadOnlyList<RequirementGroup> RequirementGroups, OperationalSupplyDocument Supply,
    MealVerificationKind Kind, string? Reason, Guid VerifiedBy, DateTimeOffset VerifiedAt);
public sealed record SaveSupplyRequest(long ExpectedVersion, IReadOnlyList<SupplySolution> Solutions);
public sealed record VerifyMealRequest(long ExpectedVersion, MealVerificationKind Kind, string? Reason);

public sealed class OperationalSupplyCalculator(ISupplySuitabilityReferences references)
{
    public OperationalSupplyDocument Calculate(IReadOnlyList<SupplyOffer> offers,
        IReadOnlyList<SupplyRequirement> groups, IReadOnlyList<SupplySolution> manual,
        CookingTogetherPreference preference, CancellationToken ct = default)
    {
        var evaluator = new SupplySuitabilityEvaluator(references);
        var resolver = new SupplySolutionResolver(references);
        var solutions = manual.ToList();
        var candidates = new List<SupplySolution>();
        var problems = new List<MealPlanningProblem>();
        var targets = new List<SupplyCoverageTarget>();
        var commonChoices = new List<CommonSupplyChoice>();
        foreach (var offer in offers)
        {
            // Manual allocations remain authoritative; they must not disable preference
            // handling for other, still automatically planned requirement groups.
            var automaticGroups = groups.Where(group => group.Portions > 0 && !manual.Any(value => value.OfferGroupId == offer.OfferGroupId &&
                value.Allocations.Any(item => item.RequirementGroupKey == group.Requirements.Key))).ToArray();
            var offerSolutions = new List<SupplySolution>();
            var offerCandidates = new List<SupplySolution>();
            var offerProblems = new List<MealPlanningProblem>();
            foreach (var group in groups)
            {
                targets.Add(new(offer.OfferGroupId, group.Requirements.Key, group.Portions));
                if (manual.Any(value => value.OfferGroupId == offer.OfferGroupId &&
                        value.Allocations.Any(item => item.RequirementGroupKey == group.Requirements.Key))) continue;
                decimal total = groups.Sum(value => value.Portions);
                decimal portions = total > 0 ? offer.Portions * group.Portions / total : 0;
                if (portions <= 0) continue;
                var standard = new SupplySolution(Guid.NewGuid(), offer.RecipeRevisionId, offer.OfferGroupId,
                    portions, new Dictionary<string, RecipePositionSelection>(),
                    [new(group.Requirements.Key, portions)], SupplyDecisionSource.Automatic);
                var resolved = resolver.Resolve(standard, group.Requirements, offer.Alternatives, ct);
                if (resolved.Selected is not null) offerSolutions.Add(resolved.Selected);
                offerCandidates.AddRange(resolved.Candidates);
                offerProblems.AddRange(resolved.Problems);
            }
            if (preference != CookingTogetherPreference.NeverTogether && automaticGroups.Length > 1 && offer.Portions > 0)
            {
                decimal total = groups.Sum(value => value.Portions);
                var allocations = automaticGroups.Select(group => new SupplyGroupAllocation(group.Requirements.Key,
                    total > 0 ? offer.Portions * group.Portions / total : 0)).ToArray();
                var commonStandard = new SupplySolution(Guid.NewGuid(), offer.RecipeRevisionId, offer.OfferGroupId,
                    allocations.Sum(value => value.Portions), new Dictionary<string, RecipePositionSelection>(), allocations,
                    SupplyDecisionSource.Automatic);
                // Search the same explicit direct/offer space against every group, rather than
                // just the first two suggestions found while resolving each group separately.
                var common = resolver.ResolveTogether(commonStandard, automaticGroups.Select(value => value.Requirements).ToArray(), offer.Alternatives, ct);
                var separate = SupplySolutionCoverage.MergeIdentical(offerSolutions);
                bool separateSuitable = automaticGroups.All(group => separate.Any(value =>
                    value.Allocations.Any(allocation => allocation.RequirementGroupKey == group.Requirements.Key))) &&
                    separate.All(value => groups.Where(group => value.Allocations.Any(allocation => allocation.RequirementGroupKey == group.Requirements.Key))
                        .All(group => evaluator.Evaluate(value, group.Requirements).IsSuitable));
                if (common.Selected is not null && (preference == CookingTogetherPreference.AlwaysTogether || !separateSuitable))
                {
                    offerSolutions = [common.Selected];
                    offerProblems.Clear();
                    offerCandidates.Clear();
                }
                else if (common.Selected is not null && separateSuitable && separate.Select(value => value.MergeKey).Distinct().Count() > 1)
                {
                    AwaitChoice([common.Selected]);
                }
                else if (common.Selected is null)
                {
                    var suitable = common.Candidates.Where(value => automaticGroups.All(group => evaluator.Evaluate(value, group.Requirements).IsSuitable))
                        .DistinctBy(value => value.MergeKey).ToArray();
                    if (suitable.Length > 1)
                    {
                        if (preference == CookingTogetherPreference.Ask && separateSuitable && separate.Count > 1)
                            AwaitChoice(suitable);
                        else
                        {
                            offerCandidates.AddRange(suitable);
                            if (preference == CookingTogetherPreference.AlwaysTogether)
                            offerProblems.Add(new(MealPlanningProblemCategory.ManualDecision,
                                MealPlanningProblemCode.CommonSupplyDecisionRequired, ReferenceId: offer.OfferGroupId));
                        }
                    }
                }

                void AwaitChoice(IReadOnlyList<SupplySolution> together)
                {
                    commonChoices.Add(new(offer.OfferGroupId, automaticGroups.Select(group => group.Requirements.Key).ToArray(),
                        together, separate));
                    // Neither valid path is active before the operator chooses one.
                    offerSolutions.Clear();
                    offerCandidates.Clear();
                    offerProblems.Clear();
                    offerProblems.Add(new(MealPlanningProblemCategory.ManualDecision,
                        MealPlanningProblemCode.CommonSupplyDecisionRequired, ReferenceId: offer.OfferGroupId));
                }
            }
            solutions.AddRange(offerSolutions);
            candidates.AddRange(offerCandidates);
            problems.AddRange(offerProblems);
        }
        // Keep valid alternatives available for an explicit switch after save/reload/recalc.
        // These are options only: stored manual decisions remain authoritative.
        if (preference == CookingTogetherPreference.Ask && manual.Count > 0)
        {
            var decidedOffers = offers.Where(offer => manual.Any(value => value.OfferGroupId == offer.OfferGroupId)).ToArray();
            if (decidedOffers.Length > 0)
            {
                var alternatives = (Calculate(decidedOffers, groups, [], preference, ct).CommonSupplyChoices ?? [])
                    .Where(choice => choice.RequirementGroupKeys.All(key => manual.Any(solution =>
                        solution.OfferGroupId == choice.OfferGroupId && solution.Allocations.Any(allocation => allocation.RequirementGroupKey == key))))
                    .ToArray();
                commonChoices.RemoveAll(choice => alternatives.Any(value => value.OfferGroupId == choice.OfferGroupId));
                commonChoices.AddRange(alternatives);
            }
        }
        // Identical decisions are merged even for NeverTogether. Fixed/Stepwise is re-evaluated below.
        solutions = SupplySolutionCoverage.MergeIdentical(solutions).ToList();
        foreach (var solution in solutions)
        {
            foreach (var allocation in solution.Allocations.Where(value => value.Portions > 0))
            {
                var group = groups.SingleOrDefault(value => value.Requirements.Key == allocation.RequirementGroupKey);
                if (group is null) continue; // Coverage reports obsolete allocations, preserving the decision.
                var assessment = evaluator.Evaluate(solution, group.Requirements);
                problems.AddRange(assessment.Problems);
                if (!assessment.IsSuitable && solution.Source == SupplyDecisionSource.Manual)
                    problems.Add(new(MealPlanningProblemCategory.ManualDecision,
                        MealPlanningProblemCode.ManualDecisionNoLongerSuitable, group.Requirements.Key, solution.Id));
            }
        }
        problems.AddRange(SupplySolutionCoverage.Evaluate(solutions, targets));
        var quantities = solutions.SelectMany(solution => evaluator.Evaluate(solution, new(null, [], [])).Ingredients.Select(ingredient => {
            var position = references.GetRevision(ingredient.RecipeRevisionPath.Last()).IngredientPositions.Single(value => value.Id == ingredient.PositionId);
            return new SupplyQuantity(solution.Id, ingredient.PositionPath, ingredient.IngredientName,
                ingredient.Quantity, ingredient.Unit.Symbol, position.ScalingMode, position.StepwiseScaling);
        })).ToArray();
        return new(solutions, candidates, problems.Distinct().ToArray(), groups, quantities, commonChoices);
    }
}
