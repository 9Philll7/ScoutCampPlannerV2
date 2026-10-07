namespace ScoutCampPlanner.Catering.Domain;

/// <summary>Interprets the existing revision values without inventing quantities or modifying historical revisions.</summary>
public sealed record IngredientSubstanceProfile(
    IngredientSubstanceAssessmentMode Mode, IngredientSubstanceContent? Content, bool? Contained)
{
    public static bool HasExclusiveModes(IEnumerable<IngredientSubstanceContent> contents,
        IEnumerable<IngredientPropertyValue> qualitative) =>
        !contents.Select(value => value.SubstanceId).Intersect(qualitative.Select(value => value.PropertyId)).Any();

    public static IngredientSubstanceProfile Resolve(Guid substanceId,
        IEnumerable<IngredientSubstanceContent> contents, IEnumerable<IngredientPropertyValue> qualitative,
        IEnumerable<IngredientSubstanceContent>? variantContents = null,
        IEnumerable<IngredientPropertyValue>? variantQualitative = null)
    {
        if (substanceId == Guid.Empty) throw new ArgumentException("Substance ID is required.", nameof(substanceId));
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(qualitative);
        var overrideContent = variantContents?.SingleOrDefault(value => value.SubstanceId == substanceId);
        var overrideState = variantQualitative?.SingleOrDefault(value => value.PropertyId == substanceId);
        // Any explicit variant value replaces the entire base assessment, including explicit Unknown.
        if (overrideContent is not null || overrideState is not null) return From(overrideContent, overrideState);
        return From(contents.SingleOrDefault(value => value.SubstanceId == substanceId),
            qualitative.SingleOrDefault(value => value.PropertyId == substanceId));
    }

    private static IngredientSubstanceProfile From(IngredientSubstanceContent? content, IngredientPropertyValue? state)
    {
        // Ambiguous legacy records and MayContain are not evidence of a known amount or absence.
        if (content is not null && state is not null) return new(IngredientSubstanceAssessmentMode.Unknown, null, null);
        if (content is not null) return new(IngredientSubstanceAssessmentMode.Quantitative, content, null);
        return state?.State switch
        {
            IngredientPropertyState.Contains => new(IngredientSubstanceAssessmentMode.Qualitative, null, true),
            IngredientPropertyState.DoesNotContain => new(IngredientSubstanceAssessmentMode.Qualitative, null, false),
            _ => new(IngredientSubstanceAssessmentMode.Unknown, null, null)
        };
    }
}
