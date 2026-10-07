using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientSubstanceProfileTests
{
    private readonly Guid substance = Guid.NewGuid();
    private IngredientSubstanceContent Content() => new(substance, 4.8m, Guid.NewGuid(), 100, Guid.NewGuid(),
        IngredientSubstanceContentSourceType.ManualEstimate, "Synthetic test", IngredientSubstanceContentReviewState.Unreviewed);
    private IngredientPropertyValue State(IngredientPropertyState state) => new(substance, state, IngredientPropertySource.ManuallyVerified);

    [Fact]
    public void Missing_and_ambiguous_historical_values_are_unknown_without_zero_inference()
    {
        Assert.Equal(IngredientSubstanceAssessmentMode.Unknown, IngredientSubstanceProfile.Resolve(substance, [], []).Mode);
        var ambiguous = IngredientSubstanceProfile.Resolve(substance, [Content()], [State(IngredientPropertyState.DoesNotContain)]);
        Assert.Equal(IngredientSubstanceAssessmentMode.Unknown, ambiguous.Mode);
        Assert.Null(ambiguous.Content);
        Assert.Null(ambiguous.Contained);
        Assert.Equal(IngredientSubstanceAssessmentMode.Unknown,
            IngredientSubstanceProfile.Resolve(substance, [], [State(IngredientPropertyState.MayContain)]).Mode);
    }

    [Theory]
    [InlineData(IngredientPropertyState.Contains, IngredientSubstanceAssessmentMode.Qualitative, true)]
    [InlineData(IngredientPropertyState.DoesNotContain, IngredientSubstanceAssessmentMode.Qualitative, false)]
    [InlineData(IngredientPropertyState.Unknown, IngredientSubstanceAssessmentMode.Unknown, null)]
    public void Explicit_variant_state_replaces_base_amount(IngredientPropertyState state, IngredientSubstanceAssessmentMode expected, bool? contained)
    {
        var result = IngredientSubstanceProfile.Resolve(substance, [Content()], [], [], [State(state)]);
        Assert.Equal(expected, result.Mode); Assert.Equal(contained, result.Contained); Assert.Null(result.Content);
    }

    [Fact]
    public void Variant_amount_replaces_base_state_and_missing_override_inherits_base()
    {
        var content = Content();
        Assert.Equal(content, IngredientSubstanceProfile.Resolve(substance, [], [State(IngredientPropertyState.Contains)], [content], []).Content);
        Assert.Equal(content, IngredientSubstanceProfile.Resolve(substance, [content], [], [], []).Content);
    }
}
