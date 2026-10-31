using System.Text.Json;

namespace ScoutCampPlanner.Catering.Domain;

public enum ParticipantFilterField { Stage, Category, DietType, Allergen, Substance, SpecialCatering }
public enum ParticipantFilterOperator { AnyOf, NoneOf }

public sealed record ParticipantFilterCondition(ParticipantFilterField Field,
    ParticipantFilterOperator Operator, IReadOnlyList<string?> Values);
public sealed record ParticipantFilterGroup(IReadOnlyList<ParticipantFilterCondition> Conditions);

/// <summary>OR of AND groups. Values within a condition are ORed before negation.</summary>
public sealed record ParticipantFilterDefinition(int SchemaVersion, IReadOnlyList<ParticipantFilterGroup> Groups)
{
    public void Validate()
    {
        if (SchemaVersion != 1 || Groups is null || Groups.Count == 0)
            throw new ArgumentException("Invalid participant filter version or empty filter.");
        foreach (var group in Groups)
        {
            if (group?.Conditions is null || group.Conditions.Count == 0)
                throw new ArgumentException("A filter group must contain conditions.");
            foreach (var condition in group.Conditions)
            {
                if (condition is null || !Enum.IsDefined(condition.Field) || !Enum.IsDefined(condition.Operator) ||
                    condition.Values is null || condition.Values.Count == 0)
                    throw new ArgumentException("Invalid filter condition.");
                foreach (var value in condition.Values)
                {
                    bool valid = condition.Field switch
                    {
                        ParticipantFilterField.Category => value is null or "ChildYouth" or "LeaderAdult",
                        ParticipantFilterField.SpecialCatering => value is "true" or "false",
                        ParticipantFilterField.Stage when value is null => true,
                        _ => Guid.TryParse(value, out var id) && id != Guid.Empty
                    };
                    if (!valid) throw new ArgumentException("Invalid filter value.");
                }
            }
        }
    }

    /// <summary>Order/duplicates and GUID formatting must not change the expression identity.</summary>
    public string CanonicalKey()
    {
        Validate();
        return JsonSerializer.Serialize(Groups.Select(group => JsonSerializer.Serialize(
            group.Conditions.Select(condition => JsonSerializer.Serialize(new
            {
                condition.Field, condition.Operator,
                Values = condition.Values.Select(value => Guid.TryParse(value, out var id) ? id.ToString("D") : value)
                    .Distinct().OrderBy(value => value, StringComparer.Ordinal).ToArray()
            })).Distinct().Order(StringComparer.Ordinal).ToArray()))
            .Distinct().Order(StringComparer.Ordinal).ToArray());
    }
}
