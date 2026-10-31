using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class ParticipantFilterTests
{
    private static ParticipantFilterCondition Condition(ParticipantFilterField field, params string?[] values) =>
        new(field, ParticipantFilterOperator.AnyOf, values);
    private static ParticipantFilterDefinition Filter(params ParticipantFilterCondition[] conditions) =>
        new(1, [new(conditions)]);
    private static CateringParticipantData Person() => new(Guid.NewGuid(), Guid.NewGuid(), true, null, [], []);

    [Fact]
    public void Named_filter_snapshots_input_and_distinguishes_metadata_from_selection()
    {
        var values = new List<string?> { "ChildYouth" };
        var definition = Filter(new ParticipantFilterCondition(ParticipantFilterField.Category, ParticipantFilterOperator.AnyOf, values));
        var saved = new CampParticipantFilter(Guid.NewGuid(), Guid.NewGuid(), " Test ", definition);
        values.Add("LeaderAdult");
        Assert.Equal("Test", saved.Name);
        Assert.Equal(1, saved.Version);
        Assert.Single(saved.ReadDefinition().Groups[0].Conditions[0].Values);
        Assert.False(saved.Update(1, "Renamed", saved.ReadDefinition()));
        Assert.Equal(2, saved.Version);
        Assert.Throws<InvalidOperationException>(() => saved.Update(1, "Lost update", definition));
        Assert.Equal("Renamed", saved.Name);
        Assert.Throws<ArgumentException>(() => saved.Update(2, "", definition));
        Assert.Equal(2, saved.Version);
        Assert.True(saved.Update(2, "Expanded", definition));
        Assert.Equal(3, saved.Version);
    }

    [Fact]
    public void References_are_checked_including_negated_conditions()
    {
        var allowed = Guid.NewGuid();
        var references = new ParticipantFilterReferences(new HashSet<Guid> { allowed },
            new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>());
        references.Validate(Filter(Condition(ParticipantFilterField.Stage, allowed.ToString(), null)));
        Assert.Throws<ArgumentException>(() => references.Validate(Filter(
            Condition(ParticipantFilterField.Stage, Guid.NewGuid().ToString()) with { Operator = ParticipantFilterOperator.NoneOf })));
        Assert.Throws<ArgumentException>(() => references.Validate(Filter(Condition(ParticipantFilterField.DietType, allowed.ToString()))));
    }

    [Fact]
    public void Combines_or_values_and_conditions_and_or_groups()
    {
        var stage = Guid.NewGuid(); var vegan = Guid.NewGuid(); var vegetarian = Guid.NewGuid();
        var filter = Filter(Condition(ParticipantFilterField.Stage, stage.ToString(), Guid.NewGuid().ToString()),
            Condition(ParticipantFilterField.Category, "ChildYouth"),
            Condition(ParticipantFilterField.DietType, vegan.ToString(), vegetarian.ToString()));
        var person = Person() with { CampStageId = stage, Category = "ChildYouth", DietTypeId = vegan };
        Assert.True(ParticipantFilterEvaluator.Matches(filter, person));
        Assert.False(ParticipantFilterEvaluator.Matches(filter, person with { Category = "LeaderAdult" }));
        var extended = filter with { Groups = [.. filter.Groups, new([Condition(ParticipantFilterField.Category, "LeaderAdult")])] };
        Assert.True(ParticipantFilterEvaluator.Matches(extended, person with { Category = "LeaderAdult" }));
    }

    [Fact]
    public void NoneOf_negates_the_whole_value_set()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var filter = Filter(Condition(ParticipantFilterField.Allergen, first.ToString(), second.ToString())
            with { Operator = ParticipantFilterOperator.NoneOf });
        Assert.False(ParticipantFilterEvaluator.Matches(filter, Person() with { AllergenIds = [first] }));
        Assert.False(ParticipantFilterEvaluator.Matches(filter, Person() with { AllergenIds = [second] }));
        Assert.True(ParticipantFilterEvaluator.Matches(filter, Person()));
    }

    [Fact]
    public void Null_assignments_are_explicit_and_diets_are_not_inferred()
    {
        var filter = Filter(Condition(ParticipantFilterField.Stage, new string?[] { null }),
            Condition(ParticipantFilterField.Category, new string?[] { null }));
        Assert.True(ParticipantFilterEvaluator.Matches(filter, Person()));
        Assert.False(ParticipantFilterEvaluator.Matches(filter, Person() with { CampStageId = Guid.NewGuid() }));
        Assert.False(ParticipantFilterEvaluator.Matches(Filter(Condition(ParticipantFilterField.DietType, Guid.NewGuid().ToString())), Person()));
    }

    [Fact]
    public void Substance_and_special_catering_use_requirements_not_thresholds()
    {
        var substance = Guid.NewGuid();
        var person = Person() with { Intolerances = [new(substance, null)] };
        Assert.True(ParticipantFilterEvaluator.Matches(Filter(Condition(ParticipantFilterField.Substance, substance.ToString()),
            Condition(ParticipantFilterField.SpecialCatering, "true")), person));
        Assert.True(ParticipantFilterEvaluator.Matches(Filter(Condition(ParticipantFilterField.SpecialCatering, "false")), Person()));
    }

    [Fact]
    public void Canonical_key_ignores_order_duplicates_and_guid_formatting()
    {
        var id = Guid.NewGuid();
        var first = Condition(ParticipantFilterField.Stage, id.ToString("D"));
        var second = Condition(ParticipantFilterField.Category, "ChildYouth", "LeaderAdult");
        Assert.Equal(Filter(first, second).CanonicalKey(), new ParticipantFilterDefinition(1,
            [new([second, first with { Values = [id.ToString("B").ToUpperInvariant(), id.ToString()] }, second]),
             new([first, second])]).CanonicalKey());
    }

    [Fact]
    public void Invalid_definitions_fail_even_with_no_participants()
    {
        ParticipantFilterDefinition[] invalid = [new(2, [new([Condition(ParticipantFilterField.Category, "ChildYouth")])]),
            new(1, []), new(1, [new([])]), Filter(Condition(ParticipantFilterField.Category, "unknown")),
            Filter(Condition(ParticipantFilterField.Stage, Guid.Empty.ToString())),
            Filter(Condition(ParticipantFilterField.DietType, new string?[] { null })),
            Filter(Condition((ParticipantFilterField)99, "true")),
            Filter(Condition(ParticipantFilterField.SpecialCatering, "true") with { Operator = (ParticipantFilterOperator)99 })];
        foreach (var filter in invalid)
            Assert.Throws<ArgumentException>(() => StructureParticipantProjection.Derive([], [],
                [new(Guid.NewGuid(), [], CookingUnitParticipantFilter.All, filter)]));
    }

    [Fact]
    public void Projection_preserves_structure_attendance_uniqueness_and_overlap_detection()
    {
        var root = Guid.NewGuid(); var leaf = Guid.NewGuid(); var unit = Guid.NewGuid();
        var person = Person() with { StructureNodeId = leaf, Category = "ChildYouth" };
        var filter = Filter(Condition(ParticipantFilterField.Category, "ChildYouth"));
        filter = filter with { Groups = [.. filter.Groups, .. filter.Groups] };
        CampPlanningNode[] nodes = [new(root, null, "Root"), new(leaf, root, "Leaf")];
        CateringParticipantData[] people = [person, person with { ParticipantReference = Guid.NewGuid(), IsPresent = false }];
        var selection = new StructureParticipantSelection(unit, [root], CookingUnitParticipantFilter.All, filter);
        var result = StructureParticipantProjection.Derive(nodes, people, [selection]);
        Assert.Single(result.ParticipantsByUnit[unit]);
        var overlap = StructureParticipantProjection.Derive(nodes, people,
            [selection, selection with { CookingUnitId = Guid.NewGuid() }]);
        Assert.Single(overlap.Overlapping);
        Assert.Empty(overlap.ParticipantsByUnit[unit]);
        Assert.Throws<ArgumentException>(() => StructureParticipantProjection.Derive(nodes, people,
            [selection with { Filter = CookingUnitParticipantFilter.SpecialCateringOnly }]));
    }
}
