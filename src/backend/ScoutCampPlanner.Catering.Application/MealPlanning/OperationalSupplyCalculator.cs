using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed class OperationalSupplyCalculator(ISupplySuitabilityReferences references)
{
    public OperationalSupplyDocument Calculate(IReadOnlyList<SupplyOffer> offers, IReadOnlyList<SupplyRequirement> groups,
        IReadOnlyList<SupplySolution> manual, CookingTogetherPreference preference, CancellationToken ct = default,
        IReadOnlyList<SupplyScenarioSelection>? scenarioSelections = null, bool completeOwnSupply = false)
    {
        var evaluator = new SupplySuitabilityEvaluator(references);
        var solutions = manual.ToList();
        var candidates = new List<SupplySolution>();
        var problems = new List<MealPlanningProblem>();
        var decisions = new List<SupplyDecisionGroup>();
        var questions = new List<CommonSupplyChoice>();
        var preferenceOptions = new List<CommonSupplyChoice>();
        var targets = new List<SupplyCoverageTarget>();
        foreach (var offer in offers)
        {
            ct.ThrowIfCancellationRequested();
            if (completeOwnSupply)
            {
                foreach (var group in groups)
                    targets.Add(new(offer.OfferGroupId, group.Requirements.Key, group.Portions));
                continue;
            }
            var automatic = groups.Where(group => group.Portions > 0 && !manual.Any(value => value.OfferGroupId == offer.OfferGroupId &&
                value.Allocations.Any(allocation => allocation.RequirementGroupKey == group.Requirements.Key))).ToArray();
            foreach (var group in groups.Where(group => !automatic.Any(value => value.Requirements.Key == group.Requirements.Key)))
                targets.Add(new(offer.OfferGroupId, group.Requirements.Key, group.Portions));
            var configured = scenarioSelections?.SingleOrDefault(value => value.OfferGroupId == offer.OfferGroupId)?.Preference ?? preference;
            var selected = configured == CookingTogetherPreference.Ask ? (CookingTogetherPreference?)null : configured;
            bool legacyDecided = configured == CookingTogetherPreference.Ask && automatic.Length == 0 &&
                groups.Any(group => group.Portions > 0) && manual.Any(value => value.OfferGroupId == offer.OfferGroupId);
            bool equivalent = false;
            SupplyScenario? together = null, separate = null;
            bool previewMatchesAutomatic = false;
            if (groups.Any(group => group.Portions > 0))
            {
                var previewGroups = selected.HasValue || legacyDecided ? groups.Where(group => group.Portions > 0).ToArray() : automatic;
                previewMatchesAutomatic = previewGroups.Length == automatic.Length;
                if (previewGroups.Length > 0)
                {
                    together = Plan(offer, previewGroups, true);
                    separate = Plan(offer, previewGroups, false);
                    equivalent = Equivalent(together, separate);
                    var togetherOptions = together.Decisions.Count == 1 && together.Decisions[0].RequirementGroupKeys.Count == previewGroups.Length
                        ? together.Decisions[0].Options : together.Solutions.Count == 1 && together.Decisions.Count == 0 && together.Problems.Count == 0 ? together.Solutions : [];
                    var choice = new CommonSupplyChoice(offer.OfferGroupId, previewGroups.Select(group => group.Requirements.Key).ToArray(), togetherOptions, separate.Solutions, together, separate);
                    preferenceOptions.Add(choice);
                    if (configured == CookingTogetherPreference.Ask && !equivalent) questions.Add(choice);
                }
                if (configured == CookingTogetherPreference.Ask && !legacyDecided && !equivalent)
                {
                    problems.Add(new(MealPlanningProblemCategory.ManualDecision, MealPlanningProblemCode.CommonSupplyDecisionRequired, ReferenceId: offer.OfferGroupId));
                    // Neither scenario becomes active before a persisted explicit confirmation.
                    continue;
                }
            }
            var scenario = previewMatchesAutomatic && together is not null && separate is not null
                ? configured == CookingTogetherPreference.AlwaysTogether ? together : separate
                : Plan(offer, automatic, configured == CookingTogetherPreference.AlwaysTogether);
            solutions.AddRange(scenario.Solutions); decisions.AddRange(scenario.Decisions); problems.AddRange(scenario.Problems);
            candidates.AddRange(scenario.Decisions.SelectMany(value => value.Options));
            foreach (var group in groups)
                if (!scenario.Decisions.Any(value => value.RequirementGroupKeys.Contains(group.Requirements.Key)))
                    targets.Add(new(offer.OfferGroupId, group.Requirements.Key, group.Portions));
        }
        solutions = SupplySolutionCoverage.MergeIdentical(solutions).ToList();
        foreach (var solution in solutions)
            foreach (var allocation in solution.Allocations.Where(value => value.Portions > 0))
            {
                var group = groups.SingleOrDefault(value => value.Requirements.Key == allocation.RequirementGroupKey);
                if (group is null) continue;
                var assessment = evaluator.Evaluate(solution, group.Requirements);
                problems.AddRange(assessment.Problems);
                if (!assessment.IsSuitable && solution.Source == SupplyDecisionSource.Manual)
                    problems.Add(new(MealPlanningProblemCategory.ManualDecision, MealPlanningProblemCode.ManualDecisionNoLongerSuitable, group.Requirements.Key, solution.Id));
            }
        problems.AddRange(SupplySolutionCoverage.Evaluate(solutions, targets));
        var quantities = solutions.SelectMany(solution => evaluator.Evaluate(solution, new(null, [], [])).Ingredients.Select(ingredient => {
            var position = references.GetRevision(ingredient.RecipeRevisionPath.Last()).IngredientPositions.Single(value => value.Id == ingredient.PositionId);
            return new SupplyQuantity(solution.Id, ingredient.PositionPath, ingredient.IngredientName, ingredient.Quantity, ingredient.Unit.Symbol, position.ScalingMode, position.StepwiseScaling);
        })).ToArray();
        return new(solutions, candidates, problems.Distinct().ToArray(), groups, quantities, questions, decisions, preferenceOptions);

        static bool Equivalent(SupplyScenario first, SupplyScenario second)
        {
            static string SolutionKey(SupplySolution value) => System.Text.Json.JsonSerializer.Serialize(new {
                value.RecipeRevisionId, value.OfferGroupId, value.Portions,
                positions = value.PositionSelections.OrderBy(item => item.Key, StringComparer.Ordinal),
                allocations = value.Allocations.OrderBy(item => item.RequirementGroupKey, StringComparer.Ordinal)
            });
            static string Key(SupplyScenario value) => System.Text.Json.JsonSerializer.Serialize(new {
                solutions = value.Solutions.Select(SolutionKey).Order(StringComparer.Ordinal),
                decisions = value.Decisions.Select(choice => System.Text.Json.JsonSerializer.Serialize(new {
                    choice.OfferGroupId, keys = choice.RequirementGroupKeys.Order(StringComparer.Ordinal),
                    options = choice.Options.Select(SolutionKey).Order(StringComparer.Ordinal)
                })).Order(StringComparer.Ordinal),
                problems = value.Problems.Select(problem => System.Text.Json.JsonSerializer.Serialize(problem)).Distinct().Order(StringComparer.Ordinal)
            });
            return Key(first) == Key(second);
        }

        SupplyScenario Plan(SupplyOffer offer, SupplyRequirement[] scope, bool together)
        {
            var resolver = new SupplySolutionResolver(references);
            var planned = new List<SupplySolution>(); var choices = new List<SupplyDecisionGroup>(); var issues = new List<MealPlanningProblem>();
            if (scope.Length == 0) return new([], [], []);
            decimal total = groups.Sum(group => group.Portions);
            SupplySolution Batch(SupplySolution template, IReadOnlyList<SupplyRequirement> batch) => template with {
                Id = Guid.NewGuid(), Portions = batch.Sum(group => total > 0 ? offer.Portions * group.Portions / total : 0),
                Allocations = batch.Select(group => new SupplyGroupAllocation(group.Requirements.Key, total > 0 ? offer.Portions * group.Portions / total : 0)).ToArray()
            };
            var original = new SupplySolution(Guid.NewGuid(), offer.RecipeRevisionId, offer.OfferGroupId, offer.Portions,
                new Dictionary<string, ScoutCampPlanner.Catering.Application.Recipes.RecipePositionSelection>(), [], SupplyDecisionSource.Automatic);
            if (offer.Portions <= 0) return new([], [], []);
            var resolved = scope.ToDictionary(group => group.Requirements.Key, group => resolver.Resolve(Batch(original, [group]), group.Requirements, offer.Alternatives, ct));
            var remaining = scope.ToList();
            if (together)
            {
                var joint = resolver.ResolveTogether(Batch(original, scope), scope.Select(group => group.Requirements).ToArray(), offer.Alternatives, ct);
                var jointOptions = joint.Selected is null ? joint.SuitableCandidates ?? [] : [joint.Selected];
                if (jointOptions.Count > 0) { Add(scope, jointOptions); return Finish(); }
                // Reuse explicit published templates and reassess at each proposed batch size.
                // Prefer the largest compatible group set, never select a recipe by ordering.
                var templates = resolved.Values.SelectMany(value => value.Selected is null ? value.Candidates : value.Candidates.Prepend(value.Selected))
                    .Concat(joint.Candidates).Append(original).DistinctBy(value => value.MergeKey).ToArray();
                while (remaining.Count > 0)
                {
                    var fits = new List<(SupplyRequirement[] Groups, SupplySolution Solution)>();
                    foreach (var template in templates)
                    {
                        ct.ThrowIfCancellationRequested();
                        var batch = remaining.ToArray();
                        while (batch.Length > 0)
                        {
                            var solution = Batch(template, batch);
                            var suitable = batch.Where(group => evaluator.Evaluate(solution, group.Requirements).IsSuitable).ToArray();
                            if (suitable.Length == batch.Length) { fits.Add((batch, solution)); break; }
                            batch = suitable;
                        }
                    }
                    if (fits.Count == 0) break;
                    var best = fits.OrderByDescending(value => value.Groups.Length).ThenByDescending(value => value.Solution.Portions)
                        .ThenBy(value => string.Join('|', value.Groups.Select(group => group.Requirements.Key).Order(StringComparer.Ordinal)), StringComparer.Ordinal).First();
                    var batchKeys = best.Groups.Select(group => group.Requirements.Key).ToHashSet();
                    Add(best.Groups, fits.Where(value => value.Groups.Select(group => group.Requirements.Key).ToHashSet().SetEquals(batchKeys)).Select(value => value.Solution).DistinctBy(value => value.MergeKey).ToArray());
                    remaining.RemoveAll(group => batchKeys.Contains(group.Requirements.Key));
                }
            }
            foreach (var group in remaining)
            {
                var resolution = resolved[group.Requirements.Key];
                var options = resolution.Selected is null ? resolution.SuitableCandidates ?? [] : [resolution.Selected];
                if (options.Count > 0) Add([group], options);
                else issues.AddRange(resolution.Problems);
            }
            return Finish();

            SupplyScenario Finish()
            {
                var merged = SupplySolutionCoverage.MergeIdentical(planned);
                foreach (var solution in merged)
                    foreach (var group in scope.Where(group => solution.Allocations.Any(allocation => allocation.RequirementGroupKey == group.Requirements.Key)))
                        issues.AddRange(evaluator.Evaluate(solution, group.Requirements).Problems);
                return new(merged, choices, issues.Distinct().ToArray());
            }

            void Add(IReadOnlyList<SupplyRequirement> batch, IReadOnlyList<SupplySolution> options)
            {
                // All candidates have already been assessed for this exact batch.
                // An unchanged recipe dominates its adjusted versions; other recipes remain choices.
                options = options.GroupBy(value => value.RecipeRevisionId).SelectMany(group =>
                    group.Any(value => value.PositionSelections.Count == 0)
                        ? group.Where(value => value.PositionSelections.Count == 0) : group).ToArray();
                if (options.Count == 1) { planned.Add(options[0]); return; }
                string[] keys = batch.Select(group => group.Requirements.Key).Order(StringComparer.Ordinal).ToArray();
                choices.Add(new($"{offer.OfferGroupId}:{string.Join(';', keys)}", offer.OfferGroupId, keys, options, together));
                issues.Add(new(MealPlanningProblemCategory.ManualDecision, options.All(value => value.RecipeRevisionId == offer.RecipeRevisionId)
                    ? MealPlanningProblemCode.MultipleDirectSolutions : MealPlanningProblemCode.MultipleMatchingOfferAlternatives,
                    keys.Length == 1 ? keys[0] : null, offer.OfferGroupId));
            }
        }
    }
}
