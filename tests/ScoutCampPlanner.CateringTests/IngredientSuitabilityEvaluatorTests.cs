using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientSuitabilityEvaluatorTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
    private readonly IngredientPropertyDefinition milk = Definition("MILK");
    private readonly IngredientPropertyDefinition glutenCereals = Definition("GLUTEN_CEREALS");
    private readonly IngredientPropertyDefinition wheat;
    private readonly IngredientPropertyDefinition lactose = Definition("LACTOSE");
    private readonly IngredientPropertyDefinition gluten = Definition("GLUTEN");
    private readonly IReadOnlyList<IngredientPropertyDefinition> origins;
    private readonly IngredientSuitabilityEvaluator evaluator;

    public IngredientSuitabilityEvaluatorTests()
    {
        wheat = Definition("WHEAT", glutenCereals.Id);
        origins =
        [
            Definition("PLANT"), Definition("MEAT"), Definition("POULTRY"), Definition("FISH"),
            Definition("CRUSTACEAN"), Definition("MOLLUSC"), Definition("DAIRY"), Definition("EGG"),
            Definition("HONEY"), Definition("INSECT"), Definition("ANIMAL_FAT"), Definition("GELATIN"),
            Definition("ANIMAL_RENNET"), Definition("OTHER_ANIMAL_DERIVED"), Definition("UNKNOWN_ORIGIN"),
        ];
        evaluator = new IngredientSuitabilityEvaluator(
            [milk, glutenCereals, wheat],
            [lactose, gluten],
            origins);
    }

    [Fact]
    public void Lactose_free_butter_remains_incompatible_with_milk_allergy()
    {
        IngredientRevision revision = Draft();
        revision.SetAllergen(Value(milk, IngredientPropertyState.Contains), UserId, Now);
        revision.SetIntolerance(Value(lactose, IngredientPropertyState.Contains), UserId, Now);
        IngredientVariantRevision variant = revision.AddVariant(
            Guid.NewGuid(), "lactose_free", "Laktosefrei", UserId, Now);
        revision.SetVariantIntoleranceOverride(
            variant.VariantKey,
            Value(lactose, IngredientPropertyState.DoesNotContain),
            UserId,
            Now);

        Assert.Equal(IngredientCompatibility.Compatible,
            evaluator.EvaluateLactoseFree(revision, variant.VariantKey));
        Assert.Equal(IngredientCompatibility.Incompatible,
            evaluator.EvaluateMilkFree(revision, variant.VariantKey));
    }

    [Fact]
    public void Animal_origin_is_incompatible_with_vegan()
    {
        IngredientRevision revision = ReviewedDraft();
        revision.SetOrigin(Value(Origin("DAIRY"), IngredientPropertyState.Contains), UserId, Now);

        Assert.Equal(IngredientCompatibility.Incompatible, evaluator.EvaluateVegan(revision));
        Assert.Equal(IngredientCompatibility.Compatible, evaluator.EvaluateVegetarian(revision));
    }

    [Fact]
    public void Fish_is_allowed_for_pescetarian_but_not_vegetarian()
    {
        IngredientRevision revision = ReviewedDraft();
        revision.SetOrigin(Value(Origin("FISH"), IngredientPropertyState.Contains), UserId, Now);

        Assert.Equal(IngredientCompatibility.Compatible, evaluator.EvaluatePescetarian(revision));
        Assert.Equal(IngredientCompatibility.Incompatible, evaluator.EvaluateVegetarian(revision));
    }

    [Fact]
    public void Unknown_origin_prevents_positive_dietary_result()
    {
        IngredientRevision revision = ReviewedDraft();
        revision.SetOrigin(Value(Origin("UNKNOWN_ORIGIN"), IngredientPropertyState.Contains), UserId, Now);

        Assert.Equal(IngredientCompatibility.Unknown, evaluator.EvaluateVegan(revision));
    }

    [Fact]
    public void Unreviewed_missing_origin_is_unknown()
    {
        Assert.Equal(IngredientCompatibility.Unknown, evaluator.EvaluateVegan(Draft()));
    }

    [Fact]
    public void Allergen_subtype_implies_parent_group()
    {
        IngredientRevision revision = ReviewedDraft();
        revision.SetAllergen(Value(wheat, IngredientPropertyState.Contains), UserId, Now);

        Assert.Equal(IngredientCompatibility.Incompatible,
            evaluator.EvaluateAllergen(revision, glutenCereals.Code));
        Assert.Equal(IngredientCompatibility.Incompatible,
            evaluator.EvaluateGlutenFree(revision));
    }

    [Fact]
    public void Gluten_free_is_derived_only_from_official_allergen_group()
    {
        IngredientRevision revision = ReviewedDraft();
        revision.SetAllergen(
            Value(glutenCereals, IngredientPropertyState.DoesNotContain), UserId, Now);
        revision.SetIntolerance(
            Value(gluten, IngredientPropertyState.Contains), UserId, Now);

        Assert.Equal(IngredientCompatibility.Compatible,
            evaluator.EvaluateGlutenFree(revision));
    }

    [Fact]
    public void May_contain_requires_review_instead_of_positive_result()
    {
        IngredientRevision revision = ReviewedDraft();
        revision.SetAllergen(Value(milk, IngredientPropertyState.MayContain), UserId, Now);

        Assert.Equal(IngredientCompatibility.Unknown, evaluator.EvaluateMilkFree(revision));
    }

    [Fact]
    public void Unknown_variant_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => evaluator.EvaluateVegan(Draft(), "missing"));
    }

    private IngredientRevision ReviewedDraft()
    {
        IngredientRevision revision = Draft();
        revision.SetReviewStates(
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed,
            UserId,
            Now);
        return revision;
    }

    private static IngredientRevision Draft()
    {
        var ingredient = IngredientIdentity.CreateCentral(Guid.NewGuid());
        return ingredient.CreateDraft(
            Guid.NewGuid(), "Testzutat", Guid.NewGuid(), Guid.NewGuid(), UserId, Now);
    }

    private IngredientPropertyDefinition Origin(string code) => origins.Single(value => value.Code == code);

    private static IngredientPropertyDefinition Definition(string code, Guid? parentId = null) =>
        new(Guid.NewGuid(), code, parentId);

    private static IngredientPropertyValue Value(
        IngredientPropertyDefinition definition,
        IngredientPropertyState state) =>
        new(definition.Id, state, IngredientPropertySource.ManuallyVerified);
}
