using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public static class ParticipantFilterEvaluator
{
    public static bool Matches(ParticipantFilterDefinition filter, CateringParticipantData person)
    {
        filter.Validate();
        return filter.Groups.Any(group => group.Conditions.All(condition => Matches(condition, person)));
    }

    private static bool Matches(ParticipantFilterCondition condition, CateringParticipantData person)
    {
        bool any = condition.Values.Any(value => condition.Field switch
        {
            ParticipantFilterField.Stage => value is null ? person.CampStageId is null : person.CampStageId == Guid.Parse(value),
            ParticipantFilterField.Category => person.Category == value,
            ParticipantFilterField.DietType => person.DietTypeId == Guid.Parse(value!),
            ParticipantFilterField.Allergen => person.AllergenIds.Contains(Guid.Parse(value!)),
            ParticipantFilterField.Substance => person.Intolerances.Any(item => item.SubstanceId == Guid.Parse(value!)),
            ParticipantFilterField.SpecialCatering => person.RequiresSpecialCatering == (value == "true"),
            _ => throw new ArgumentOutOfRangeException(nameof(condition))
        });
        return condition.Operator == ParticipantFilterOperator.AnyOf ? any : !any;
    }
}
