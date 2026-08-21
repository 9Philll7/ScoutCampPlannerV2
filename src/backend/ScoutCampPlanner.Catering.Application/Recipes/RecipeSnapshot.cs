using System.Text.Json;
using System.Text.Json.Serialization;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public sealed record MeasurementUnitSnapshot(
    Guid UnitId,
    string Name,
    string Symbol,
    MeasurementDimension Dimension,
    decimal BaseUnitFactor);

public sealed record IngredientSnapshotSource(
    Guid IngredientId,
    string Name,
    IReadOnlyList<ConflictReference> Conflicts);

public sealed record IngredientUnitSnapshot(
    MeasurementUnitSnapshot Unit,
    decimal ReferenceQuantityPerUnit);

public interface IRecipeSnapshotReferences
{
    IngredientSnapshotSource GetIngredient(Guid ingredientId);
    IngredientUnitSnapshot GetIngredientUnit(Guid ingredientId, Guid unitId);
    MeasurementUnitSnapshot GetUnit(Guid unitId);
    IReadOnlySet<ConflictReference> GetRevisionConflicts(Guid revisionId);
}

public sealed record RecipeReferenceSnapshot(
    decimal? StandardServings,
    decimal StandardPortionFactor,
    decimal? ReferenceQuantity,
    MeasurementUnitSnapshot? ReferenceUnit);

public sealed record RecipeAuthoringStageSnapshot(Guid StageId, string StageName, decimal Factor, decimal EnteredServings);

public sealed record RecipeGroupSnapshot(Guid Id, string Name, int SortOrder);

public sealed record IngredientReplacementSnapshot(
    Guid Id,
    IngredientSnapshotSource Ingredient,
    decimal Quantity,
    IngredientUnitSnapshot Unit,
    IReadOnlyList<ConflictReference> ApplicableConflicts);

public sealed record IngredientPositionSnapshot(
    Guid Id,
    Guid? GroupId,
    int SortOrder,
    IngredientSnapshotSource Ingredient,
    decimal Quantity,
    IngredientUnitSnapshot Unit,
    ScalingMode ScalingMode,
    AgeGroupScalingMode AgeGroupScaling,
    StepwiseScaling? StepwiseScaling,
    IReadOnlyList<IngredientReplacementSnapshot> Replacements);

public sealed record RecipeReplacementSnapshot(
    Guid Id,
    Guid RecipeRevisionId,
    decimal? Servings,
    decimal? Quantity,
    MeasurementUnitSnapshot? Unit,
    IReadOnlyList<ConflictReference> ApplicableConflicts,
    IReadOnlyList<ConflictReference> RevisionConflicts);

public sealed record SubrecipePositionSnapshot(
    Guid Id,
    Guid? GroupId,
    int SortOrder,
    Guid RecipeRevisionId,
    decimal? RequiredServings,
    decimal? RequiredQuantity,
    MeasurementUnitSnapshot? RequiredUnit,
    IReadOnlyList<ConflictReference> RevisionConflicts,
    IReadOnlyList<RecipeReplacementSnapshot> Replacements);

public sealed record RecipeSnapshot(
    int SchemaVersion,
    string Name,
    string? Description,
    string? Source,
    string? InternalNotes,
    RecipeType RecipeType,
    RecipeReferenceSnapshot Reference,
    RecipeAuthoringStageSnapshot? AuthoringStage,
    bool? DefaultAgeGroupScalingApplies,
    IReadOnlyList<string> Tags,
    IReadOnlyList<RecipeGroupSnapshot> Groups,
    IReadOnlyList<IngredientPositionSnapshot> IngredientPositions,
    IReadOnlyList<SubrecipePositionSnapshot> SubrecipePositions,
    IReadOnlyList<ConflictReference> ExposedConflicts);

public sealed class RecipeSnapshotBuilder(IRecipeSnapshotReferences references)
{
    public const int CurrentSchemaVersion = 1;

    public RecipeSnapshot Build(RecipeDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RecipeReferenceSnapshot reference = BuildReference(draft);
        RecipeAuthoringStageSnapshot? authoring = draft.AuthoringStage is null
            ? null
            : new RecipeAuthoringStageSnapshot(
                draft.AuthoringStage.StageId, draft.AuthoringStage.StageName, draft.AuthoringStage.Factor,
                draft.ReferenceServings ?? throw new InvalidOperationException("Entered servings are required."));
        IngredientPositionSnapshot[] ingredients = draft.IngredientPositions
            .OrderBy(value => value.SortOrder).ThenBy(value => value.Id)
            .Select(BuildIngredientPosition).ToArray();
        SubrecipePositionSnapshot[] subrecipes = draft.SubrecipePositions
            .OrderBy(value => value.SortOrder).ThenBy(value => value.Id)
            .Select(BuildSubrecipePosition).ToArray();
        ConflictReference[] conflicts = ingredients.SelectMany(value => value.Ingredient.Conflicts)
            .Concat(subrecipes.SelectMany(value => value.RevisionConflicts))
            .Distinct().OrderBy(value => value.Type).ThenBy(value => value.Id).ToArray();

        return new RecipeSnapshot(
            CurrentSchemaVersion, draft.Name, draft.Description, draft.Source, draft.InternalNotes,
            draft.RecipeType, reference, authoring, draft.DefaultAgeGroupScalingApplies,
            draft.Tags.Order(StringComparer.Ordinal).ToArray(),
            draft.Groups.OrderBy(value => value.SortOrder).ThenBy(value => value.Id)
                .Select(value => new RecipeGroupSnapshot(value.Id, value.Name, value.SortOrder)).ToArray(),
            ingredients, subrecipes, conflicts);
    }

    public static string Serialize(RecipeSnapshot snapshot) => JsonSerializer.Serialize(snapshot, JsonOptions);

    public static RecipeSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize<RecipeSnapshot>(json, JsonOptions) ??
        throw new InvalidOperationException("Recipe snapshot is empty.");

    private RecipeReferenceSnapshot BuildReference(RecipeDraft draft)
    {
        if (draft.RecipeType == RecipeType.PortionBased)
        {
            decimal entered = draft.ReferenceServings ?? throw new InvalidOperationException("Reference servings are required.");
            decimal standard = entered * (draft.AuthoringStage?.Factor ?? 1m);
            return new RecipeReferenceSnapshot(standard, 1m, null, null);
        }

        Guid unitId = draft.ReferenceUnitId ?? throw new InvalidOperationException("Reference unit is required.");
        return new RecipeReferenceSnapshot(
            null, 1m,
            draft.ReferenceQuantity ?? throw new InvalidOperationException("Reference quantity is required."),
            references.GetUnit(unitId));
    }

    private IngredientPositionSnapshot BuildIngredientPosition(RecipeIngredientPosition position)
    {
        Guid ingredientId = position.BaseIngredientId ?? throw new InvalidOperationException("Ingredient is required.");
        Guid unitId = position.UnitId ?? throw new InvalidOperationException("Ingredient unit is required.");
        return new IngredientPositionSnapshot(
            position.Id, position.GroupId, position.SortOrder, references.GetIngredient(ingredientId),
            position.Quantity ?? throw new InvalidOperationException("Ingredient quantity is required."),
            references.GetIngredientUnit(ingredientId, unitId), position.ScalingMode, position.AgeGroupScaling,
            position.StepwiseScaling,
            position.ReplacementRules.OrderBy(value => value.Id).Select(BuildIngredientReplacement).ToArray());
    }

    private IngredientReplacementSnapshot BuildIngredientReplacement(IngredientReplacementRule replacement)
    {
        Guid ingredientId = replacement.ReplacementBaseIngredientId ??
                            throw new InvalidOperationException("Replacement ingredient is required.");
        Guid unitId = replacement.ReplacementUnitId ??
                      throw new InvalidOperationException("Replacement unit is required.");
        return new IngredientReplacementSnapshot(
            replacement.Id, references.GetIngredient(ingredientId),
            replacement.ReplacementQuantity ?? throw new InvalidOperationException("Replacement quantity is required."),
            references.GetIngredientUnit(ingredientId, unitId),
            replacement.Conflicts.OrderBy(value => value.Type).ThenBy(value => value.Id).ToArray());
    }

    private SubrecipePositionSnapshot BuildSubrecipePosition(RecipeSubrecipePosition position)
    {
        Guid revisionId = position.RecipeRevisionId ?? throw new InvalidOperationException("Recipe revision is required.");
        return new SubrecipePositionSnapshot(
            position.Id, position.GroupId, position.SortOrder, revisionId, position.RequiredServings,
            position.RequiredQuantity,
            position.RequiredUnitId.HasValue ? references.GetUnit(position.RequiredUnitId.Value) : null,
            references.GetRevisionConflicts(revisionId).OrderBy(value => value.Type).ThenBy(value => value.Id).ToArray(),
            position.ReplacementRules.OrderBy(value => value.Id).Select(BuildRecipeReplacement).ToArray());
    }

    private RecipeReplacementSnapshot BuildRecipeReplacement(RecipeReplacementRule replacement)
    {
        Guid revisionId = replacement.ReplacementRecipeRevisionId ??
                          throw new InvalidOperationException("Replacement recipe revision is required.");
        return new RecipeReplacementSnapshot(
            replacement.Id, revisionId, replacement.ReplacementServings, replacement.ReplacementQuantity,
            replacement.ReplacementUnitId.HasValue ? references.GetUnit(replacement.ReplacementUnitId.Value) : null,
            replacement.Conflicts.OrderBy(value => value.Type).ThenBy(value => value.Id).ToArray(),
            references.GetRevisionConflicts(revisionId).OrderBy(value => value.Type).ThenBy(value => value.Id).ToArray());
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
