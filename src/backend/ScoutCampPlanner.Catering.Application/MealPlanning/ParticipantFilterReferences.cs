using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

/// <summary>Allowed IDs must be resolved for the requesting camp/tenant, not from a global unscoped catalog.</summary>
public sealed record ParticipantFilterReferences(IReadOnlySet<Guid> StageIds, IReadOnlySet<Guid> DietTypeIds,
    IReadOnlySet<Guid> AllergenIds, IReadOnlySet<Guid> SubstanceIds)
{
    public void Validate(ParticipantFilterDefinition definition)
    {
        definition.Validate();
        foreach (var condition in definition.Groups.SelectMany(group => group.Conditions))
        {
            IReadOnlySet<Guid>? allowed = condition.Field switch
            {
                ParticipantFilterField.Stage => StageIds,
                ParticipantFilterField.DietType => DietTypeIds,
                ParticipantFilterField.Allergen => AllergenIds,
                ParticipantFilterField.Substance => SubstanceIds,
                _ => null
            };
            if (allowed is null) continue;
            foreach (var value in condition.Values.Where(value => value is not null))
                if (!allowed.Contains(Guid.Parse(value!)))
                    throw new ArgumentException("A participant filter reference is missing or outside the allowed scope.");
        }
    }
}
