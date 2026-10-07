using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class SubstancePortionEvaluatorTests
{
    [Fact]
    public void Missing_threshold_reports_presence_without_inventing_an_exceeded_limit()
    {
        var result = SubstancePortionEvaluator.Evaluate(new(Guid.NewGuid(), null),
            [SubstancePortionContribution.Quantitative("a", 0.25m)]);
        Assert.Equal(0.25m, result.ExactGramsPerPortion);
        Assert.Contains(result.Problems, value => value.Code == MealPlanningProblemCode.MissingThreshold);
        Assert.Contains(result.Problems, value => value.Code == MealPlanningProblemCode.SubstancePresentWithoutThreshold);
        Assert.DoesNotContain(result.Problems, value => value.Code == MealPlanningProblemCode.SubstanceThresholdExceeded);
    }

    [Fact]
    public void Quantitative_contributions_sum_per_portion_and_compare_only_supplied_threshold()
    {
        var requirement = new IntoleranceRequirement(Guid.NewGuid(), 2m);
        var result = SubstancePortionEvaluator.Evaluate(requirement,
            [SubstancePortionContribution.Quantitative("root/a", 1.5m),
             SubstancePortionContribution.Quantitative("root/sub/b", .5m)]);
        Assert.Equal(2m, result.ExactGramsPerPortion);
        Assert.True(result.IsSuitable);
        var exceeded = SubstancePortionEvaluator.Evaluate(new(requirement.SubstanceId, 1.9m),
            [SubstancePortionContribution.Quantitative("root/a", 2m)]);
        Assert.Contains(exceeded.Problems, value => value.Code == MealPlanningProblemCode.SubstanceThresholdExceeded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mixed_unknown_or_qualitative_contained_never_returns_an_exact_sum(bool unknown)
    {
        var result = SubstancePortionEvaluator.Evaluate(new(Guid.NewGuid(), 5m),
            [SubstancePortionContribution.Quantitative("a", .2m), unknown
                ? SubstancePortionContribution.Unknown("nested/b")
                : SubstancePortionContribution.Qualitative("nested/b", true)]);
        Assert.Null(result.ExactGramsPerPortion);
        Assert.False(result.IsSuitable);
        Assert.Equal("nested/b", Assert.Single(result.Problems).PositionPath);
    }

    [Fact]
    public void Qualitative_absence_adds_no_content_but_missing_threshold_remains_visible()
    {
        var result = SubstancePortionEvaluator.Evaluate(new(Guid.NewGuid(), null),
            [SubstancePortionContribution.Qualitative("a", false)]);
        Assert.Equal(0m, result.ExactGramsPerPortion);
        Assert.Equal(MealPlanningProblemCode.MissingThreshold, Assert.Single(result.Problems).Code);
    }

    [Fact]
    public void Missing_contributions_are_unknown_not_zero()
    {
        var result = SubstancePortionEvaluator.Evaluate(new(Guid.NewGuid(), 5m), []);
        Assert.Null(result.ExactGramsPerPortion);
        Assert.Equal(MealPlanningProblemCode.UnknownSubstanceData, Assert.Single(result.Problems).Code);
    }
}
