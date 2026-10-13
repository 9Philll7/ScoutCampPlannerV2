using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class SupplySolutionTests
{
    [Fact]
    public void Variant_replaces_allergen_and_substance_profile_but_inherits_origin()
    {
        var f = new Fixture();
        var ingredient = f.Ingredient(IngredientPropertyState.Contains, f.Plant, amount: 10m,
            variants: [new(Guid.NewGuid(), "free", "Free", true, 0,
                allergenOverrides: [new(f.Allergen, IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)],
                intoleranceOverrides: [new(f.Substance, IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)])]);
        var position = f.Position(ingredient);
        var recipe = f.Recipe(position);
        var requirements = new RequirementSignature(f.Diet, [f.Allergen], [new(f.Substance, 0)]);
        var result = new SupplySolutionResolver(f).Resolve(f.Solution(recipe), requirements, [], TestContext.Current.CancellationToken);
        Assert.Equal("free", Assert.Single(result.Selected!.PositionSelections).Value.VariantKey);
        Assert.Empty(new SupplySuitabilityEvaluator(f).Evaluate(result.Selected, requirements).Problems);
    }

    [Fact]
    public void Unknown_variant_content_masks_quantitative_base_and_inactive_variant_is_not_automatic()
    {
        var f = new Fixture();
        var ingredient = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, amount: 0,
            variants: [new(Guid.NewGuid(), "unknown", "Unknown", false, 0,
                intoleranceOverrides: [new(f.Substance, IngredientPropertyState.Unknown, IngredientPropertySource.ManuallyVerified)])]);
        var position = f.Position(ingredient);
        var recipe = f.Recipe(position);
        var solution = f.Solution(recipe) with { PositionSelections = new Dictionary<string, RecipePositionSelection> {
            [$"{recipe:N}/{position.Id:N}"] = new(VariantKey: "unknown") } };
        Assert.Contains(new SupplySuitabilityEvaluator(f).Evaluate(solution, new(null, [], [new(f.Substance, 0)])).Problems,
            value => value.Code == MealPlanningProblemCode.UnknownSubstanceData);
        var bad = f.Ingredient(IngredientPropertyState.Contains, f.Plant,
            variants: [new(Guid.NewGuid(), "retired", "Retired", false, 0,
                allergenOverrides: [new(f.Allergen, IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)])]);
        Assert.Null(new SupplySolutionResolver(f).Resolve(f.Solution(f.Recipe(f.Position(bad))), f.Requirements(), [], TestContext.Current.CancellationToken).Selected);
    }

    [Fact]
    public void Subrecipe_replacement_is_not_followed_by_a_recursive_replacement_chain()
    {
        var f = new Fixture();
        var bad = f.Ingredient(IngredientPropertyState.Contains, f.Plant);
        var good = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant);
        var child = f.Recipe(f.Position(bad));
        var replacementChild = f.Recipe(f.Position(bad) with { Replacements = [f.Replacement(good)] });
        var root = f.Recipe();
        var replacement = new RecipeReplacementSnapshot(Guid.NewGuid(), replacementChild, 1, null, null,
            [new(ConflictType.Allergen, f.Allergen)], []);
        var position = new SubrecipePositionSnapshot(Guid.NewGuid(), null, 0, child, 1, null, null, [], [replacement]);
        f.Recipes[root] = f.Recipes[root] with { SubrecipePositions = [position] };
        Assert.Null(new SupplySolutionResolver(f).Resolve(f.Solution(root), f.Requirements(), [], TestContext.Current.CancellationToken).Selected);
        var nested = f.Recipes[replacementChild].IngredientPositions[0];
        var selections = new Dictionary<string, RecipePositionSelection> {
            [$"{root:N}/{position.Id:N}"] = new(replacement.Id),
            [$"{root:N}/{position.Id:N}/{replacementChild:N}/{nested.Id:N}"] = new(nested.Replacements[0].Id)
        };
        Assert.Throws<InvalidOperationException>(() => new SupplySuitabilityEvaluator(f)
            .Evaluate(f.Solution(root) with { PositionSelections = selections }, f.Requirements()));
    }

    [Fact]
    public void Replacement_removes_original_allergen_and_origin_and_is_selected_directly()
    {
        var f = new Fixture();
        var original = f.Ingredient(IngredientPropertyState.Contains, f.Animal);
        var replacement = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant);
        var rule = f.Replacement(replacement);
        var recipe = f.Recipe(f.Position(original) with { Replacements = [rule] });
        var result = new SupplySolutionResolver(f).Resolve(f.Solution(recipe), f.Requirements(), [], TestContext.Current.CancellationToken);
        Assert.Equal(SupplyResolutionStage.Direct, result.Stage);
        Assert.Equal(rule.Id, Assert.Single(result.Selected!.PositionSelections).Value.ReplacementRuleId);
        var evaluated = new SupplySuitabilityEvaluator(f).Evaluate(result.Selected, f.Requirements());
        Assert.Empty(evaluated.Problems);
        Assert.Equal(replacement, Assert.Single(evaluated.Ingredients).IngredientId);
    }

    [Fact]
    public void Independent_unique_changes_are_combined_but_multiple_solutions_are_not_ranked()
    {
        var f = new Fixture();
        var bad = f.Ingredient(IngredientPropertyState.Contains, f.Plant);
        var good = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant);
        var first = f.Position(bad) with { Replacements = [f.Replacement(good)] };
        var second = f.Position(bad) with { Replacements = [f.Replacement(good)] };
        var root = f.Recipe(first, second);
        var result = new SupplySolutionResolver(f).Resolve(f.Solution(root), f.Requirements(), [], TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Selected!.PositionSelections.Count);
        f.Recipes[root] = f.Recipes[root] with { IngredientPositions = [first with {
            Replacements = [f.Replacement(good), f.Replacement(good)] }] };
        result = new SupplySolutionResolver(f).Resolve(f.Solution(root), f.Requirements(), [], TestContext.Current.CancellationToken);
        Assert.Null(result.Selected);
        Assert.Contains(result.Problems, value => value.Code == MealPlanningProblemCode.MultipleDirectSolutions);
    }

    [Fact]
    public void Partial_direct_resolution_stays_open_and_only_one_offer_alternative_is_automatic()
    {
        var f = new Fixture();
        var bad = f.Ingredient(IngredientPropertyState.Contains, f.Plant);
        var good = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant);
        var root = f.Recipe(f.Position(bad), f.Position(bad) with { Replacements = [f.Replacement(good)] });
        var resolver = new SupplySolutionResolver(f);
        var unresolved = resolver.Resolve(f.Solution(root), f.Requirements(), [], TestContext.Current.CancellationToken);
        Assert.Null(unresolved.Selected);
        Assert.Contains(unresolved.Problems, value => value.Code == MealPlanningProblemCode.PartialDirectResolution);
        var alternative = f.Recipe(f.Position(good));
        Assert.Equal(SupplyResolutionStage.OfferAlternative,
            resolver.Resolve(f.Solution(root), f.Requirements(), [alternative], TestContext.Current.CancellationToken).Stage);
        var ambiguous = resolver.Resolve(f.Solution(root), f.Requirements(), [alternative, f.Recipe(f.Position(good))], TestContext.Current.CancellationToken);
        Assert.Null(ambiguous.Selected);
        Assert.Contains(ambiguous.Problems, value => value.Code == MealPlanningProblemCode.MultipleMatchingOfferAlternatives);
    }

    [Fact]
    public void Unsuitable_manual_decision_is_preserved()
    {
        var f = new Fixture(); var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Animal)));
        var manual = f.Solution(recipe) with { Source = SupplyDecisionSource.Manual };
        var result = new SupplySolutionResolver(f).Resolve(manual, f.Requirements(), [], TestContext.Current.CancellationToken);
        Assert.Same(manual, result.Selected);
        Assert.Contains(result.Problems, value => value.Code == MealPlanningProblemCode.ManualDecisionNoLongerSuitable);
    }

    [Theory]
    [InlineData(ScalingMode.Linear, 10, 10)]
    [InlineData(ScalingMode.Fixed, 10, 1)]
    [InlineData(ScalingMode.Stepwise, 15, 4)]
    public void Substance_amount_uses_actual_batch_scaling(ScalingMode mode, int portions, int gramsPerPortion)
    {
        var f = new Fixture();
        var ingredient = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, amount: 10m);
        var position = f.Position(ingredient) with { Quantity = 100m, ScalingMode = mode,
            StepwiseScaling = mode == ScalingMode.Stepwise ? new(10m, 300m) : null };
        var solution = f.Solution(f.Recipe(position)) with { Portions = portions };
        var signature = new RequirementSignature(null, [], [new(f.Substance, gramsPerPortion)]);
        Assert.True(new SupplySuitabilityEvaluator(f).Evaluate(solution, signature).IsSuitable);
        signature = new(null, [], [new(f.Substance, gramsPerPortion - 0.01m)]);
        Assert.Contains(new SupplySuitabilityEvaluator(f).Evaluate(solution, signature).Problems,
            value => value.Code == MealPlanningProblemCode.SubstanceThresholdExceeded);
    }

    [Fact]
    public void Unknown_substance_is_not_zero_and_missing_diet_rule_is_not_allowed()
    {
        var f = new Fixture(); var ingredient = f.Ingredient(IngredientPropertyState.DoesNotContain, Guid.NewGuid());
        var solution = f.Solution(f.Recipe(f.Position(ingredient)));
        var result = new SupplySuitabilityEvaluator(f).Evaluate(solution,
            new(f.Diet, [], [new(f.Substance, 1m)]));
        Assert.Contains(result.Problems, value => value.Code == MealPlanningProblemCode.UnknownSubstanceData);
        Assert.Contains(result.Problems, value => value.Code == MealPlanningProblemCode.UnknownDietTypeRule);
    }

    [Fact]
    public void Coverage_keeps_offer_contexts_separate_and_merges_identical_solutions()
    {
        var f = new Fixture(); var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant)));
        var first = f.Solution(recipe) with { Portions = 2, Allocations = [new("group", 1)] };
        var second = first with { Id = Guid.NewGuid(), Portions = 3, Allocations = [new("group", 3)] };
        var merged = Assert.Single(SupplySolutionCoverage.MergeIdentical([first, second]));
        Assert.Equal(5, merged.Portions);
        Assert.Equal(4, Assert.Single(merged.Allocations).Portions);
        var issues = SupplySolutionCoverage.Evaluate([merged], [new(null, "group", 3), new(Guid.NewGuid(), "group", 1)]);
        Assert.Contains(issues, value => value.Code == MealPlanningProblemCode.RequirementGroupOvercovered);
        Assert.Contains(issues, value => value.Code == MealPlanningProblemCode.RequirementGroupUndercovered);
        Assert.Contains(issues, value => value.Code == MealPlanningProblemCode.UnassignedPlannedPortions);
    }

    internal sealed class Fixture : ISupplySuitabilityReferences
    {
        public Guid Allergen { get; } = Guid.NewGuid();
        public Guid Plant { get; } = Guid.NewGuid();
        public Guid Animal { get; } = Guid.NewGuid();
        public Guid Diet { get; } = Guid.NewGuid();
        public Guid Substance { get; } = Guid.NewGuid();
        public MeasurementUnit Gram { get; } = new(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1);
        public Dictionary<Guid, RecipeSnapshot> Recipes { get; } = [];
        public Dictionary<(Guid Diet, Guid Origin), DietaryOriginDecision?> DietRules { get; } = [];
        private readonly Dictionary<Guid, IngredientRevisionDraftContent> ingredients = [];
        public Guid Ingredient(IngredientPropertyState allergen, Guid origin, decimal? amount = null,
            IReadOnlyList<IngredientVariantDraftContent>? variants = null)
        {
            var id = Guid.NewGuid();
            ingredients.Add(id, IngredientRevisionDraftContent.Create("Dummy", Guid.NewGuid(), Gram.Id,
                IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed,
                allergens: [new(Allergen, allergen, IngredientPropertySource.ManuallyVerified)],
                origins: [new(origin, IngredientPropertyState.Contains, IngredientPropertySource.ManuallyVerified)], variants: variants,
                substanceContents: amount.HasValue ? [new(Substance, amount.Value, Gram.Id, 100, Gram.Id,
                    IngredientSubstanceContentSourceType.ManualEstimate, "Dummy", IngredientSubstanceContentReviewState.Reviewed)] : []));
            return id;
        }
        public IngredientPositionSnapshot Position(Guid ingredient) => new(Guid.NewGuid(), null, 0,
            new(ingredient, "Dummy", []), 1m, new(new(Gram.Id, Gram.Name, Gram.Symbol, Gram.Dimension, 1), 1),
            ScalingMode.Linear, AgeGroupScalingMode.Ignore, null, []);
        public IngredientReplacementSnapshot Replacement(Guid ingredient) => new(Guid.NewGuid(),
            new(ingredient, "Dummy replacement", []), 1, Position(ingredient).Unit, [new(ConflictType.Allergen, Allergen)]);
        public Guid Recipe(params IngredientPositionSnapshot[] positions)
        {
            var id = Guid.NewGuid(); Recipes.Add(id, new(2, "Dummy", null, null, null, RecipeType.PortionBased,
                new(1, 1, null, null), null, false, [], [], positions, [], [])); return id;
        }
        public SupplySolution Solution(Guid recipe) => new(Guid.NewGuid(), recipe, null, 1,
            new Dictionary<string, RecipePositionSelection>(), [], SupplyDecisionSource.Automatic);
        public RequirementSignature Requirements() => new(Diet, [Allergen], []);
        public RecipeSnapshot GetRevision(Guid revisionId) => Recipes[revisionId];
        IngredientRevisionDraftContent ISupplySuitabilityReferences.Ingredient(Guid revisionId) => ingredients[revisionId];
        public MeasurementUnit Unit(Guid id) => id == Gram.Id ? Gram : throw new InvalidOperationException();
        public IReadOnlyList<IngredientPropertyDefinition> Allergens => [new(Allergen, "MILK")];
        public IReadOnlyList<IngredientPropertyDefinition> MainOrigins => [new(Plant, "PLANT"), new(Animal, "DAIRY")];
        public bool IsQuantitativeSubstance(Guid id) => id == Substance;
        public DietaryOriginDecision? DietDecision(Guid diet, Guid origin) => DietRules.TryGetValue((diet, origin), out var changed) ? changed : diet != Diet ? null :
            origin == Plant ? DietaryOriginDecision.Allowed : DietaryOriginDecision.Excluded;
        public (IngredientSnapshotSource Ingredient, IngredientUnitSnapshot Unit) ResolveVariant(Guid revision, string key, Guid unit)
        {
            var variant = ingredients[revision].Variants!.Single(value => value.VariantKey == key);
            return (new(revision, variant.Name, []), Position(revision).Unit);
        }
    }
}
