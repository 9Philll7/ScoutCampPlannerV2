using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientSubstanceContentTests
{
    [Fact]
    public void Content_keeps_measured_value_source_and_review_state()
    {
        Guid substanceId = Guid.NewGuid();
        Guid gramId = Guid.NewGuid();
        Guid milliliterId = Guid.NewGuid();

        var content = new IngredientSubstanceContent(
            substanceId, 4.8m, gramId, 100m, milliliterId,
            IngredientSubstanceContentSourceType.OfficialDatabase,
            "BLS 4.0", IngredientSubstanceContentReviewState.Reviewed);

        Assert.Equal(4.8m, content.Amount);
        Assert.Equal(100m, content.ReferenceQuantity);
        Assert.Equal("BLS 4.0", content.SourceReference);
        Assert.Equal(IngredientSubstanceContentReviewState.Reviewed, content.ReviewState);
    }

    [Theory]
    [InlineData(-0.01, 100)]
    [InlineData(1, 0)]
    public void Content_rejects_invalid_quantities(double amount, double referenceQuantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IngredientSubstanceContent(
            Guid.NewGuid(), (decimal)amount, Guid.NewGuid(), (decimal)referenceQuantity, Guid.NewGuid(),
            IngredientSubstanceContentSourceType.Manufacturer, "Etikett",
            IngredientSubstanceContentReviewState.Unreviewed));
    }

    [Fact]
    public void Draft_rejects_duplicate_substances()
    {
        Guid substanceId = Guid.NewGuid();
        var content = new IngredientSubstanceContent(
            substanceId, 1m, Guid.NewGuid(), 100m, Guid.NewGuid(),
            IngredientSubstanceContentSourceType.Manufacturer, "Etikett",
            IngredientSubstanceContentReviewState.Unreviewed);

        Assert.Throws<ArgumentException>(() => IngredientRevisionDraftContent.Create(
            "Milch", Guid.NewGuid(), Guid.NewGuid(),
            IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed, substanceContents: [content, content]));
    }
}
