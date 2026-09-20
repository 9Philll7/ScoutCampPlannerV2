using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record MealPlanningRecipeOption(
    Guid RevisionId, Guid RecipeId, string Name, int RevisionNumber, bool IsPortionBased,
    MealPlanEntryRole? SuggestedRole = null);

public sealed record MealPlanEntryDocument(
    Guid Id, Guid RecipeRevisionId, bool IsStandard, string? DisplayName,
    MealPlanEntryRole? Role, string? Note, int SortOrder);

public sealed record MealPlanOfferGroupDocument(
    Guid Id, Guid CampMealId, string? Name, int SortOrder,
    IReadOnlyList<MealPlanEntryDocument> Entries);

public sealed record MealPlanDocument(
    Guid Id, Guid CampId, string Name, int SortOrder, int Version,
    IReadOnlyList<MealPlanOfferGroupDocument> OfferGroups);

public sealed record MealPlanSummary(
    Guid Id, string Name, int SortOrder, int Version, int MissingActiveMealCount);

public sealed record MealSlotSummary(
    Guid Id, Guid MealTypeId, string MealTypeName, DateOnly Date, bool IsActive,
    bool IsInsideCampPeriod, int ChangeVersion);

public sealed record StructureNodeOption(Guid Id, Guid? ParentId, string Name);

public sealed record CookingUnitGroupDocument(Guid Id, Guid CampId, string Name, int SortOrder);

public sealed record CookingUnitDocument(
    Guid Id, Guid CampId, string Name, int SortOrder, Guid? GroupId, Guid? StandardMealPlanId,
    IReadOnlyList<Guid> DefaultStructureNodeIds);

public sealed record CookingUnitMealDocument(
    Guid Id, Guid CookingUnitId, Guid CampMealId, MealPlanSubscriptionState SubscriptionState,
    decimal? DemandOverride, decimal? CalculatedDemand, decimal? EffectiveDemand,
    OperationalMealPlanStatus Status, Guid? MealPlanId, int? MealPlanVersion,
    DateTimeOffset? CalculatedAtUtc, IReadOnlyList<Guid> StructureOverrideNodeIds,
    IReadOnlyList<OfferTargetDocument> OfferTargets, IReadOnlyList<RecipeChoiceDocument> RecipeChoices,
    IReadOnlyList<string> Warnings);

public sealed record OfferTargetDocument(Guid Id, Guid OfferGroupId, decimal? TargetOverride);

public sealed record RecipeChoiceDocument(
    Guid Id, Guid RecipeRevisionId, Guid? OfferGroupId, Guid? MealPlanEntryId, int SortOrder);

public sealed record MealPlanningOverview(
    Guid CampId, IReadOnlyList<MealSlotSummary> Meals, IReadOnlyList<StructureNodeOption> StructureNodes,
    IReadOnlyList<MealPlanSummary> MealPlans, IReadOnlyList<MealPlanDocument> MealPlanDocuments,
    IReadOnlyList<CookingUnitGroupDocument> CookingUnitGroups,
    IReadOnlyList<CookingUnitDocument> CookingUnits,
    IReadOnlyList<CookingUnitMealDocument> OperationalMeals,
    IReadOnlyList<MealPlanningRecipeOption> RecipeOptions,
    IReadOnlyList<string> CoverageWarnings);

public sealed record SaveMealPlanRequest(
    int ExpectedVersion, string Name, int SortOrder, IReadOnlyList<MealPlanOfferGroupDocument>? OfferGroups);

public sealed record SaveCookingUnitRequest(
    string Name, int SortOrder, Guid? GroupId, Guid? StandardMealPlanId,
    IReadOnlyList<Guid>? DefaultStructureNodeIds, Guid? InitialStructureNodeId = null);

public sealed record SaveCookingUnitGroupRequest(string Name, int SortOrder);

public sealed record ConfigureCookingUnitMealRequest(
    MealPlanSubscriptionState SubscriptionState, decimal? DemandOverride,
    IReadOnlyList<Guid>? StructureOverrideNodeIds,
    IReadOnlyList<OfferTargetDocument>? OfferTargets,
    IReadOnlyList<RecipeChoiceDocument>? RecipeChoices);

public enum MealPlanningMutationStatus
{
    Success,
    NotFound,
    Invalid,
    Conflict,
    Blocked,
}

public sealed record MealPlanningMutationResult(
    MealPlanningMutationStatus Status,
    string? Code = null,
    string? Message = null,
    IReadOnlyList<string>? References = null,
    Guid? Id = null,
    int? Version = null)
{
    public bool IsSuccess => Status == MealPlanningMutationStatus.Success;
}

public sealed record MealPlanningData(
    IReadOnlyList<MealPlan> MealPlans,
    IReadOnlyList<MealPlanSnapshot> Snapshots,
    IReadOnlyList<MealPlanOfferGroup> OfferGroups,
    IReadOnlyList<MealPlanEntry> Entries,
    IReadOnlyList<CampMealType> MealTypes,
    IReadOnlyList<CampMeal> Meals,
    IReadOnlyList<CampStageFoodFactor> FoodFactors,
    IReadOnlyList<CookingUnitGroup> CookingUnitGroups,
    IReadOnlyList<CookingUnit> CookingUnits,
    IReadOnlyList<CookingUnitStructureAssignment> StructureAssignments,
    IReadOnlyList<CookingUnitMealState> MealStates,
    IReadOnlyList<CookingUnitMealOfferTarget> OfferTargets,
    IReadOnlyList<CookingUnitMealRecipeChoice> RecipeChoices);

public interface IMealPlanningStore
{
    Task<MealPlanningData> LoadAsync(Guid campId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MealPlanningRecipeOption>> ListAccessibleRecipesAsync(
        Guid campId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> AreRecipeRevisionsAccessibleAsync(
        Guid campId, Guid tenantId, IReadOnlyCollection<Guid> revisionIds,
        CancellationToken cancellationToken = default);
    Task EnsureCampRecipeLibraryAsync(
        Guid campId, Guid actorUserId, IReadOnlyCollection<Guid> revisionIds,
        CancellationToken cancellationToken = default);
    Task<MealPlanningMutationResult> CreateMealPlanAsync(
        MealPlan mealPlan, CancellationToken cancellationToken = default);
    Task<MealPlanningMutationResult> SaveMealPlanAsync(
        Guid campId, Guid mealPlanId, int expectedVersion, string name, int sortOrder,
        IReadOnlyList<MealPlanOfferGroupDocument> groups, string snapshotJson,
        DateTimeOffset savedAtUtc, CancellationToken cancellationToken = default);
    Task<MealPlanningMutationResult> DeleteMealPlanAsync(
        Guid campId, Guid mealPlanId, CancellationToken cancellationToken = default);
    Task<MealPlanningMutationResult> ReorderMealPlansAsync(
        Guid campId, IReadOnlyList<Guid> mealPlanIds, CancellationToken cancellationToken = default);
    Task<MealPlanningMutationResult> SaveCookingUnitGroupAsync(
        CookingUnitGroup group, CancellationToken cancellationToken = default);
    Task<MealPlanningMutationResult> DeleteCookingUnitGroupAsync(
        Guid campId, Guid groupId, CancellationToken cancellationToken = default);
    Task<MealPlanningMutationResult> SaveCookingUnitAsync(
        CookingUnit unit, IReadOnlyCollection<Guid> defaultStructureNodeIds,
        CancellationToken cancellationToken = default);
    Task<MealPlanningMutationResult> DeleteCookingUnitAsync(
        Guid campId, Guid cookingUnitId, CancellationToken cancellationToken = default);
    Task<MealPlanningMutationResult> ConfigureCookingUnitMealAsync(
        Guid campId, Guid cookingUnitId, Guid mealId, ConfigureCookingUnitMealRequest request,
        CancellationToken cancellationToken = default);
    Task<MealPlanningMutationResult> ResetStructureOverrideAsync(
        Guid campId, Guid cookingUnitId, Guid mealId, CancellationToken cancellationToken = default);
    Task SaveCalculationAsync(
        Guid campId, Guid cookingUnitId, Guid mealId, decimal? calculatedDemand,
        Guid? mealPlanId, int? mealPlanVersion, Guid? snapshotId, string calculationJson,
        string sourceFingerprint, IReadOnlyList<string> warnings, bool complete,
        DateTimeOffset calculatedAtUtc, CancellationToken cancellationToken = default);
}

public sealed class MealPlanningService(
    IMealPlanningStore store,
    ICampPlanningLookup campPlanning,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MealPlanningOverview?> GetOverviewAsync(
        Guid campId, CancellationToken cancellationToken = default)
    {
        CampPlanningData? camp = await campPlanning.GetPlanningDataAsync(campId, cancellationToken);
        if (camp is null) return null;
        MealPlanningData data = await store.LoadAsync(campId, cancellationToken);
        IReadOnlyList<MealPlanningRecipeOption> recipes = await store.ListAccessibleRecipesAsync(
            campId, camp.TenantId, cancellationToken);
        var mealTypes = data.MealTypes.ToDictionary(value => value.Id);
        MealSlotSummary[] meals = data.Meals.OrderBy(value => value.Date)
            .ThenBy(value => mealTypes.GetValueOrDefault(value.MealTypeId)?.SortOrder ?? int.MaxValue)
            .Select(value => new MealSlotSummary(
                value.Id, value.MealTypeId, mealTypes.GetValueOrDefault(value.MealTypeId)?.Name ?? "?",
                value.Date, value.IsActive, IsInsideCamp(value.Date, camp), value.ChangeVersion)).ToArray();
        Guid[] activeMealIds = data.Meals.Where(value => value.IsActive && IsInsideCamp(value.Date, camp))
            .Select(value => value.Id).ToArray();
        MealPlanSummary[] plans = data.MealPlans.OrderBy(value => value.SortOrder).Select(plan =>
        {
            var usable = data.OfferGroups.Where(group => group.MealPlanId == plan.Id)
                .Where(group => data.Entries.Count(entry => entry.OfferGroupId == group.Id && entry.IsStandard) == 1)
                .Select(group => group.CampMealId).Distinct().ToHashSet();
            return new MealPlanSummary(plan.Id, plan.Name, plan.SortOrder, plan.Version,
                activeMealIds.Count(id => !usable.Contains(id)));
        }).ToArray();
        CookingUnitGroupDocument[] groups = data.CookingUnitGroups.OrderBy(value => value.SortOrder)
            .Select(value => new CookingUnitGroupDocument(value.Id, value.CampId, value.Name, value.SortOrder)).ToArray();
        CookingUnitDocument[] units = data.CookingUnits.OrderBy(value => value.SortOrder).Select(value =>
            new CookingUnitDocument(value.Id, value.CampId, value.Name, value.SortOrder, value.GroupId,
                value.StandardMealPlanId, data.StructureAssignments
                    .Where(item => item.CookingUnitId == value.Id && item.CampMealId is null)
                    .Select(item => item.StructureNodeId).ToArray())).ToArray();
        CookingUnitMealDocument[] operational = data.MealStates.Select(state =>
            MapOperationalMeal(state, data, camp)).ToArray();
        StructureNodeOption[] nodes = camp.Nodes.Select(value =>
            new StructureNodeOption(value.Id, value.ParentId, value.Name)).ToArray();
        MealPlanDocument[] documents = data.MealPlans.Select(value => ToDocument(value, data)).ToArray();
        return new MealPlanningOverview(campId, meals, nodes, plans, documents, groups, units, operational, recipes,
            BuildCoverageWarnings(data, camp));
    }

    public async Task<MealPlanDocument?> GetMealPlanAsync(
        Guid campId, Guid mealPlanId, CancellationToken cancellationToken = default)
    {
        MealPlanningData data = await store.LoadAsync(campId, cancellationToken);
        MealPlan? plan = data.MealPlans.SingleOrDefault(value => value.Id == mealPlanId);
        if (plan is null) return null;
        return ToDocument(plan, data);
    }

    public async Task<MealPlanningMutationResult> CreateMealPlanAsync(
        Guid campId, string name, CancellationToken cancellationToken = default)
    {
        if (campId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            return Invalid("meal_plan_invalid", "Name und Lager sind erforderlich.");
        MealPlanningData data = await store.LoadAsync(campId, cancellationToken);
        if (data.MealPlans.Any(value => string.Equals(value.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return Invalid("meal_plan_name_duplicate", "Ein Mahlzeitenplan mit diesem Namen existiert bereits.");
        var plan = new MealPlan(Guid.NewGuid(), campId, name, data.MealPlans.Count, 0);
        return await store.CreateMealPlanAsync(plan, cancellationToken);
    }

    public async Task<MealPlanningMutationResult> SaveMealPlanAsync(
        Guid campId, Guid mealPlanId, Guid actorUserId, SaveMealPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        CampPlanningData? camp = await campPlanning.GetPlanningDataAsync(campId, cancellationToken);
        if (camp is null) return NotFound();
        MealPlanningData data = await store.LoadAsync(campId, cancellationToken);
        MealPlan? current = data.MealPlans.SingleOrDefault(value => value.Id == mealPlanId);
        if (current is null) return NotFound();
        MealPlanOfferGroupDocument[] groups = request.OfferGroups?.ToArray() ?? [];
        string? error = ValidatePlan(request.Name, request.SortOrder, groups, data.Meals, camp);
        if (error is not null) return Invalid("meal_plan_invalid", error);
        Guid[] revisionIds = groups.SelectMany(value => value.Entries)
            .Select(value => value.RecipeRevisionId).Distinct().ToArray();
        if (!await store.AreRecipeRevisionsAccessibleAsync(campId, camp.TenantId, revisionIds, cancellationToken))
            return Invalid("recipe_revision_unavailable", "Mindestens eine Rezeptrevision ist nicht veröffentlicht, nicht portionenbasiert oder in diesem Lager nicht zugänglich.");
        var snapshotDocument = new MealPlanDocument(
            current.Id, current.CampId, request.Name.Trim(), request.SortOrder,
            checked(request.ExpectedVersion + 1), groups);
        string snapshotJson = JsonSerializer.Serialize(snapshotDocument, JsonOptions);
        MealPlanningMutationResult result = await store.SaveMealPlanAsync(
            campId, mealPlanId, request.ExpectedVersion, request.Name.Trim(), request.SortOrder,
            groups, snapshotJson, timeProvider.GetUtcNow(), cancellationToken);
        if (!result.IsSuccess) return result;
        await store.EnsureCampRecipeLibraryAsync(campId, actorUserId, revisionIds, cancellationToken);
        return result;
    }

    public Task<MealPlanningMutationResult> DeleteMealPlanAsync(
        Guid campId, Guid mealPlanId, CancellationToken cancellationToken = default) =>
        store.DeleteMealPlanAsync(campId, mealPlanId, cancellationToken);

    public async Task<MealPlanningMutationResult> ReorderMealPlansAsync(
        Guid campId, IReadOnlyList<Guid>? mealPlanIds, CancellationToken cancellationToken = default)
    {
        Guid[] ids = mealPlanIds?.ToArray() ?? [];
        MealPlanningData data = await store.LoadAsync(campId, cancellationToken);
        if (ids.Length != data.MealPlans.Count || ids.Distinct().Count() != ids.Length ||
            ids.Except(data.MealPlans.Select(value => value.Id)).Any())
            return Invalid("meal_plan_order_invalid", "Die Planreihenfolge ist ungültig.");
        return await store.ReorderMealPlansAsync(campId, ids, cancellationToken);
    }

    public async Task<MealPlanningMutationResult> SaveCookingUnitGroupAsync(
        Guid campId, Guid? groupId, SaveCookingUnitGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var group = new CookingUnitGroup(groupId ?? Guid.NewGuid(), campId, request.Name, request.SortOrder);
            return await store.SaveCookingUnitGroupAsync(group, cancellationToken);
        }
        catch (ArgumentException exception) { return Invalid("cooking_unit_group_invalid", exception.Message); }
    }

    public Task<MealPlanningMutationResult> DeleteCookingUnitGroupAsync(
        Guid campId, Guid groupId, CancellationToken cancellationToken = default) =>
        store.DeleteCookingUnitGroupAsync(campId, groupId, cancellationToken);

    public async Task<MealPlanningMutationResult> SaveCookingUnitAsync(
        Guid campId, Guid? cookingUnitId, SaveCookingUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        CampPlanningData? camp = await campPlanning.GetPlanningDataAsync(campId, cancellationToken);
        if (camp is null) return NotFound();
        Guid[] nodeIds = (request.DefaultStructureNodeIds ?? [])
            .Append(request.InitialStructureNodeId ?? Guid.Empty).Where(value => value != Guid.Empty).Distinct().ToArray();
        string? assignmentError = ValidateStructureNodes(nodeIds, camp.Nodes);
        if (assignmentError is not null) return Invalid("structure_assignment_invalid", assignmentError);
        try
        {
            string name = request.Name;
            if (string.IsNullOrWhiteSpace(name) && request.InitialStructureNodeId is Guid sourceId)
                name = camp.Nodes.Single(value => value.Id == sourceId).Name;
            var unit = new CookingUnit(cookingUnitId ?? Guid.NewGuid(), campId, name, request.SortOrder,
                request.GroupId, request.StandardMealPlanId);
            return await store.SaveCookingUnitAsync(unit, nodeIds, cancellationToken);
        }
        catch (ArgumentException exception) { return Invalid("cooking_unit_invalid", exception.Message); }
    }

    public Task<MealPlanningMutationResult> DeleteCookingUnitAsync(
        Guid campId, Guid cookingUnitId, CancellationToken cancellationToken = default) =>
        store.DeleteCookingUnitAsync(campId, cookingUnitId, cancellationToken);

    public async Task<MealPlanningMutationResult> ConfigureCookingUnitMealAsync(
        Guid campId, Guid cookingUnitId, Guid mealId, Guid actorUserId,
        ConfigureCookingUnitMealRequest request, CancellationToken cancellationToken = default)
    {
        CampPlanningData? camp = await campPlanning.GetPlanningDataAsync(campId, cancellationToken);
        if (camp is null) return NotFound();
        MealPlanningData data = await store.LoadAsync(campId, cancellationToken);
        CookingUnit? unit = data.CookingUnits.SingleOrDefault(value => value.Id == cookingUnitId);
        CampMeal? meal = data.Meals.SingleOrDefault(value => value.Id == mealId);
        if (unit is null || meal is null || !meal.IsActive || !IsInsideCamp(meal.Date, camp))
            return Invalid("meal_slot_invalid", "Die Mahlzeit ist nicht als aktiver Lager-Slot verfügbar.");
        Guid[] nodes = request.StructureOverrideNodeIds?.Distinct().ToArray() ?? [];
        if (request.StructureOverrideNodeIds is not null)
        {
            string? error = ValidateStructureNodes(nodes, camp.Nodes);
            if (error is not null) return Invalid("structure_assignment_invalid", error);
        }
        RecipeChoiceDocument[] choices = request.RecipeChoices?.ToArray() ?? [];
        OfferTargetDocument[] targets = request.OfferTargets?.ToArray() ?? [];
        Guid[] applicableGroups = unit.StandardMealPlanId.HasValue
            ? data.OfferGroups.Where(value => value.MealPlanId == unit.StandardMealPlanId &&
                value.CampMealId == mealId).Select(value => value.Id).ToArray()
            : [];
        if (targets.Select(value => value.Id).Distinct().Count() != targets.Length ||
            targets.Select(value => value.OfferGroupId).Distinct().Count() != targets.Length ||
            targets.Any(value => value.Id == Guid.Empty || !applicableGroups.Contains(value.OfferGroupId)))
            return Invalid("offer_target_invalid", "Ein Angebots-Zielbedarf verweist nicht auf den wirksamen Standardplan.");
        if (choices.Select(value => value.Id).Distinct().Count() != choices.Length ||
            choices.Select(value => value.SortOrder).Distinct().Count() != choices.Length ||
            choices.Any(value => value.Id == Guid.Empty || value.RecipeRevisionId == Guid.Empty || value.SortOrder < 0))
            return Invalid("recipe_choice_invalid", "Die individuelle Rezeptauswahl ist ungültig oder doppelt.");
        foreach (RecipeChoiceDocument choice in choices)
        {
            MealPlanEntry? entry = choice.MealPlanEntryId.HasValue
                ? data.Entries.SingleOrDefault(value => value.Id == choice.MealPlanEntryId.Value)
                : null;
            if (choice.MealPlanEntryId.HasValue && (entry is null || entry.RecipeRevisionId != choice.RecipeRevisionId) ||
                choice.OfferGroupId.HasValue && !applicableGroups.Contains(choice.OfferGroupId.Value) ||
                entry is not null && (!applicableGroups.Contains(entry.OfferGroupId) ||
                    choice.OfferGroupId.HasValue && choice.OfferGroupId != entry.OfferGroupId))
                return Invalid("recipe_choice_reference_invalid", "Eine individuelle Rezeptauswahl verweist nicht auf den wirksamen MealPlan-Eintrag.");
        }
        Guid[] revisions = choices.Select(value => value.RecipeRevisionId).Distinct().ToArray();
        if (!await store.AreRecipeRevisionsAccessibleAsync(campId, camp.TenantId, revisions, cancellationToken))
            return Invalid("recipe_revision_unavailable", "Mindestens eine individuelle Rezeptrevision ist nicht zugänglich.");
        MealPlanningMutationResult result = await store.ConfigureCookingUnitMealAsync(
            campId, cookingUnitId, mealId, request with
            {
                StructureOverrideNodeIds = request.StructureOverrideNodeIds is null ? null : nodes,
                OfferTargets = targets,
                RecipeChoices = choices,
            }, cancellationToken);
        if (result.IsSuccess)
            await store.EnsureCampRecipeLibraryAsync(campId, actorUserId, revisions, cancellationToken);
        return result;
    }

    public Task<MealPlanningMutationResult> ResetStructureOverrideAsync(
        Guid campId, Guid cookingUnitId, Guid mealId, CancellationToken cancellationToken = default) =>
        store.ResetStructureOverrideAsync(campId, cookingUnitId, mealId, cancellationToken);

    public async Task<MealPlanningMutationResult> CalculateAsync(
        Guid campId, Guid cookingUnitId, Guid mealId, CancellationToken cancellationToken = default)
    {
        CampPlanningData? camp = await campPlanning.GetPlanningDataAsync(campId, cancellationToken);
        if (camp is null) return NotFound();
        MealPlanningData data = await store.LoadAsync(campId, cancellationToken);
        CookingUnit? unit = data.CookingUnits.SingleOrDefault(value => value.Id == cookingUnitId);
        CampMeal? meal = data.Meals.SingleOrDefault(value => value.Id == mealId);
        if (unit is null || meal is null) return NotFound();
        CookingUnitMealState? state = data.MealStates.SingleOrDefault(
            value => value.CookingUnitId == cookingUnitId && value.CampMealId == mealId);
        MealPlanSubscriptionState subscription = state?.SubscriptionState ?? MealPlanSubscriptionState.FollowStandard;
        Guid[] assignedNodes = EffectiveStructureNodes(cookingUnitId, mealId, data);
        decimal? demand = subscription == MealPlanSubscriptionState.NoSupplyRequired
            ? 0m
            : CalculateDemand(assignedNodes, camp, data.FoodFactors);
        MealPlan? plan = subscription == MealPlanSubscriptionState.FollowStandard && unit.StandardMealPlanId.HasValue
            ? data.MealPlans.SingleOrDefault(value => value.Id == unit.StandardMealPlanId)
            : null;
        MealPlanSnapshot? snapshot = plan is null ? null : data.Snapshots
            .Where(value => value.MealPlanId == plan.Id && value.Version == plan.Version).SingleOrDefault();
        bool complete = meal.IsActive && IsInsideCamp(meal.Date, camp) &&
            (subscription == MealPlanSubscriptionState.NoSupplyRequired || assignedNodes.Length > 0 && demand.HasValue) &&
            (subscription != MealPlanSubscriptionState.FollowStandard || plan is not null && snapshot is not null &&
                data.OfferGroups.Any(value => value.MealPlanId == plan.Id && value.CampMealId == meal.Id &&
                    data.Entries.Count(entry => entry.OfferGroupId == value.Id && entry.IsStandard) == 1)) &&
            (subscription != MealPlanSubscriptionState.Custom || state is not null &&
                data.RecipeChoices.Any(value => value.CookingUnitMealStateId == state.Id));
        List<string> warnings = BuildSlotWarnings(unit, meal, assignedNodes, data, camp);
        MealPlanOfferGroup[] effectiveGroups = plan is null ? [] : data.OfferGroups
            .Where(value => value.MealPlanId == plan.Id && value.CampMealId == meal.Id).ToArray();
        var effectiveTargets = effectiveGroups.Select(group => new
        {
            offerGroupId = group.Id,
            target = data.OfferTargets.SingleOrDefault(value => state is not null &&
                value.CookingUnitMealStateId == state.Id && value.OfferGroupId == group.Id)?.TargetOverride ??
                state?.DemandOverride ?? demand,
            isOverride = data.OfferTargets.Any(value => state is not null &&
                value.CookingUnitMealStateId == state.Id && value.OfferGroupId == group.Id),
        }).ToArray();
        var selectedRecipes = subscription switch
        {
            MealPlanSubscriptionState.FollowStandard => effectiveGroups.SelectMany(group => data.Entries
                .Where(entry => entry.OfferGroupId == group.Id && entry.IsStandard)
                .Select(entry => new { entry.RecipeRevisionId, OfferGroupId = (Guid?)group.Id,
                    MealPlanEntryId = (Guid?)entry.Id, Source = "standard" })).ToArray(),
            MealPlanSubscriptionState.Custom when state is not null => data.RecipeChoices
                .Where(value => value.CookingUnitMealStateId == state.Id).OrderBy(value => value.SortOrder)
                .Select(value => new { value.RecipeRevisionId, value.OfferGroupId,
                    value.MealPlanEntryId, Source = "custom" }).ToArray(),
            _ => [],
        };
        string calculationJson = JsonSerializer.Serialize(new
        {
            structureNodeIds = assignedNodes,
            planningEstimates = camp.Estimates.Where(value => IsCovered(value.StructureNodeId, assignedNodes, camp.Nodes)),
            foodFactors = data.FoodFactors.OrderBy(value => value.CampStageId)
                .Select(value => new { value.CampStageId, value.Factor }),
            calculatedDemand = demand,
            effectiveDemand = state?.DemandOverride ?? demand,
            mealPlanId = plan?.Id,
            mealPlanVersion = plan?.Version,
            mealPlanSnapshotId = snapshot?.Id,
            subscription,
            offerGroupTargets = effectiveTargets,
            recipeSelections = selectedRecipes,
            manualDecisions = new
            {
                demandOverride = state?.DemandOverride,
                structureOverride = data.StructureAssignments.Any(value => value.CookingUnitId == cookingUnitId &&
                    value.CampMealId == mealId),
            },
        }, JsonOptions);
        string fingerprint = Fingerprint(BuildSourceFingerprint(unit, meal, subscription, assignedNodes, plan, camp, data));
        await store.SaveCalculationAsync(campId, cookingUnitId, mealId, demand, plan?.Id, plan?.Version,
            snapshot?.Id, calculationJson, fingerprint, warnings, complete, timeProvider.GetUtcNow(), cancellationToken);
        return new MealPlanningMutationResult(MealPlanningMutationStatus.Success);
    }

    private static MealPlanDocument ToDocument(MealPlan plan, MealPlanningData data) => new(
        plan.Id, plan.CampId, plan.Name, plan.SortOrder, plan.Version,
        data.OfferGroups.Where(value => value.MealPlanId == plan.Id)
            .OrderBy(value => value.CampMealId).ThenBy(value => value.SortOrder)
            .Select(group => new MealPlanOfferGroupDocument(
                group.Id, group.CampMealId, group.Name, group.SortOrder,
                data.Entries.Where(value => value.OfferGroupId == group.Id).OrderBy(value => value.SortOrder)
                    .Select(value => new MealPlanEntryDocument(
                        value.Id, value.RecipeRevisionId, value.IsStandard, value.DisplayName,
                        value.Role, value.Note, value.SortOrder)).ToArray())).ToArray());

    private static string? ValidatePlan(
        string name, int sortOrder, IReadOnlyList<MealPlanOfferGroupDocument> groups,
        IReadOnlyList<CampMeal> meals, CampPlanningData camp)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 || sortOrder < 0)
            return "Name oder Sortierreihenfolge ist ungültig.";
        if (groups.Select(value => value.Id).Distinct().Count() != groups.Count ||
            groups.SelectMany(value => value.Entries).Select(value => value.Id).Distinct().Count() !=
            groups.Sum(value => value.Entries.Count))
            return "Angebotsgruppen oder Einträge enthalten doppelte IDs.";
        var validMeals = meals.ToDictionary(value => value.Id);
        if (groups.Any(group => group.Id == Guid.Empty || !validMeals.TryGetValue(group.CampMealId, out CampMeal? meal) ||
                !meal.IsActive || !IsInsideCamp(meal.Date, camp) || group.SortOrder < 0 || group.Entries is null))
            return "Eine Angebotsgruppe verweist nicht auf eine aktive Lagermahlzeit.";
        foreach (IGrouping<Guid, MealPlanOfferGroupDocument> slot in groups.GroupBy(value => value.CampMealId))
        {
            if (slot.Select(value => value.SortOrder).Distinct().Count() != slot.Count())
                return "Angebotsgruppen einer Mahlzeit benötigen eindeutige Sortierreihenfolgen.";
        }
        foreach (MealPlanOfferGroupDocument group in groups)
        {
            if (group.Entries.Count(value => value.IsStandard) != 1)
                return "Jede Angebotsgruppe benötigt genau einen Standardeintrag.";
            if (group.Entries.Any(value => value.Id == Guid.Empty || value.RecipeRevisionId == Guid.Empty || value.SortOrder < 0) ||
                group.Entries.Select(value => value.RecipeRevisionId).Distinct().Count() != group.Entries.Count ||
                group.Entries.Select(value => value.SortOrder).Distinct().Count() != group.Entries.Count)
                return "Einträge einer Angebotsgruppe sind ungültig oder doppelt.";
        }
        return null;
    }

    private static string? ValidateStructureNodes(
        IReadOnlyCollection<Guid> selectedIds, IReadOnlyList<CampPlanningNode> nodes)
    {
        Dictionary<Guid, CampPlanningNode> byId = nodes.ToDictionary(value => value.Id);
        if (selectedIds.Any(value => !byId.ContainsKey(value))) return "Ein Strukturknoten existiert nicht im Lager.";
        HashSet<Guid> selected = selectedIds.ToHashSet();
        foreach (Guid nodeId in selected)
        {
            Guid? parent = byId[nodeId].ParentId;
            var visited = new HashSet<Guid> { nodeId };
            while (parent is Guid id)
            {
                if (!visited.Add(id) || !byId.TryGetValue(id, out CampPlanningNode? ancestor))
                    return "Die Lagerstruktur ist ungültig.";
                if (selected.Contains(id)) return "Über- und untergeordnete Knoten dürfen nicht gemeinsam zugeordnet werden.";
                parent = ancestor.ParentId;
            }
        }
        return null;
    }

    private static Guid[] EffectiveStructureNodes(Guid unitId, Guid mealId, MealPlanningData data)
    {
        Guid[] overridden = data.StructureAssignments
            .Where(value => value.CookingUnitId == unitId && value.CampMealId == mealId)
            .Select(value => value.StructureNodeId).ToArray();
        return overridden.Length > 0 ? overridden : data.StructureAssignments
            .Where(value => value.CookingUnitId == unitId && value.CampMealId is null)
            .Select(value => value.StructureNodeId).ToArray();
    }

    private static decimal? CalculateDemand(
        IReadOnlyCollection<Guid> assignedNodes, CampPlanningData camp,
        IReadOnlyList<CampStageFoodFactor> foodFactors)
    {
        if (assignedNodes.Count == 0) return null;
        Dictionary<Guid, decimal> factors = foodFactors.ToDictionary(value => value.CampStageId, value => value.Factor);
        return camp.Estimates.Where(value => IsCovered(value.StructureNodeId, assignedNodes, camp.Nodes))
            .Sum(value => value.ChildYouthCount * factors.GetValueOrDefault(value.CampStageId, 1m) + value.LeaderCount);
    }

    private static bool IsCovered(
        Guid estimateNodeId, IReadOnlyCollection<Guid> assignedNodes, IReadOnlyList<CampPlanningNode> nodes)
    {
        HashSet<Guid> assigned = assignedNodes.ToHashSet();
        Dictionary<Guid, Guid?> parents = nodes.ToDictionary(value => value.Id, value => value.ParentId);
        Guid? candidate = estimateNodeId;
        while (candidate is Guid id)
        {
            if (assigned.Contains(id)) return true;
            candidate = parents.GetValueOrDefault(id);
        }
        return false;
    }

    private static CookingUnitMealDocument MapOperationalMeal(
        CookingUnitMealState state, MealPlanningData data, CampPlanningData camp)
    {
        CookingUnit unit = data.CookingUnits.Single(value => value.Id == state.CookingUnitId);
        CampMeal meal = data.Meals.Single(value => value.Id == state.CampMealId);
        Guid[] nodes = EffectiveStructureNodes(state.CookingUnitId, state.CampMealId, data);
        MealPlan? plan = state.SubscriptionState == MealPlanSubscriptionState.FollowStandard && unit.StandardMealPlanId.HasValue
            ? data.MealPlans.SingleOrDefault(value => value.Id == unit.StandardMealPlanId)
            : null;
        string currentFingerprint = Fingerprint(BuildSourceFingerprint(unit, meal, state.SubscriptionState, nodes, plan, camp, data));
        OperationalMealPlanStatus status = state.Status;
        if (state.CalculatedAtUtc.HasValue && state.SourceFingerprint != currentFingerprint)
            status = OperationalMealPlanStatus.Stale;
        string[] warnings = string.IsNullOrWhiteSpace(state.WarningsJson)
            ? []
            : JsonSerializer.Deserialize<string[]>(state.WarningsJson, JsonOptions) ?? [];
        return new CookingUnitMealDocument(
            state.Id, state.CookingUnitId, state.CampMealId, state.SubscriptionState,
            state.DemandOverride, state.CalculatedDemand, state.EffectiveDemand, status,
            state.MealPlanId, state.MealPlanVersion, state.CalculatedAtUtc,
            data.StructureAssignments.Where(value => value.CookingUnitId == state.CookingUnitId &&
                    value.CampMealId == state.CampMealId).Select(value => value.StructureNodeId).ToArray(),
            data.OfferTargets.Where(value => value.CookingUnitMealStateId == state.Id)
                .Select(value => new OfferTargetDocument(value.Id, value.OfferGroupId, value.TargetOverride)).ToArray(),
            data.RecipeChoices.Where(value => value.CookingUnitMealStateId == state.Id)
                .Select(value => new RecipeChoiceDocument(value.Id, value.RecipeRevisionId,
                    value.OfferGroupId, value.MealPlanEntryId, value.SortOrder)).ToArray(), warnings);
    }

    private static string BuildSourceFingerprint(
        CookingUnit unit, CampMeal meal, MealPlanSubscriptionState subscription,
        IReadOnlyCollection<Guid> nodes, MealPlan? plan, CampPlanningData camp, MealPlanningData data) =>
        JsonSerializer.Serialize(new
        {
            meal.Id,
            meal.IsActive,
            meal.ChangeVersion,
            meal.Date,
            camp.StartDate,
            camp.EndDate,
            subscription,
            structure = nodes.Order().ToArray(),
            estimates = camp.Estimates.Where(value => IsCovered(value.StructureNodeId, nodes, camp.Nodes))
                .OrderBy(value => value.StructureNodeId).ThenBy(value => value.CampStageId).ToArray(),
            factors = data.FoodFactors.OrderBy(value => value.CampStageId)
                .Select(value => new { value.CampStageId, value.Factor }).ToArray(),
            standardMealPlanId = unit.StandardMealPlanId,
            planVersion = subscription == MealPlanSubscriptionState.FollowStandard ? plan?.Version : null,
        }, JsonOptions);

    private static string Fingerprint(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static IReadOnlyList<string> BuildCoverageWarnings(MealPlanningData data, CampPlanningData camp)
    {
        var warnings = new List<string>();
        Guid[] estimatedNodes = camp.Estimates.Where(value => value.ChildYouthCount + value.LeaderCount > 0)
            .Select(value => value.StructureNodeId).Distinct().ToArray();
        foreach (CampMeal meal in data.Meals.Where(value => value.IsActive && IsInsideCamp(value.Date, camp)))
        {
            foreach (Guid nodeId in estimatedNodes)
            {
                string[] units = data.CookingUnits.Where(unit =>
                        IsCovered(nodeId, EffectiveStructureNodes(unit.Id, meal.Id, data), camp.Nodes))
                    .Select(value => value.Name).ToArray();
                if (units.Length == 0) warnings.Add($"{meal.Date}: Strukturknoten ist keiner Kocheinheit zugeordnet.");
                else if (units.Length > 1) warnings.Add($"{meal.Date}: Strukturknoten ist mehrfach zugeordnet ({string.Join(", ", units)})." );
            }
        }
        return warnings.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static List<string> BuildSlotWarnings(
        CookingUnit unit, CampMeal meal, Guid[] nodes, MealPlanningData data, CampPlanningData camp)
    {
        var warnings = new List<string>();
        if (!meal.IsActive) warnings.Add("Die Mahlzeit ist derzeit deaktiviert und wird operativ ignoriert.");
        if (!IsInsideCamp(meal.Date, camp)) warnings.Add("Die Mahlzeit liegt außerhalb des aktuellen Lagerzeitraums.");
        foreach (Guid node in camp.Estimates.Select(value => value.StructureNodeId).Distinct())
        {
            if (!IsCovered(node, nodes, camp.Nodes)) continue;
            int count = data.CookingUnits.Count(candidate =>
                IsCovered(node, EffectiveStructureNodes(candidate.Id, meal.Id, data), camp.Nodes));
            if (count > 1) warnings.Add("Die Strukturzuordnung überschneidet sich mit einer anderen Kocheinheit.");
        }
        return warnings.Distinct().ToList();
    }

    private static bool IsInsideCamp(DateOnly date, CampPlanningData camp) =>
        (!camp.StartDate.HasValue || date >= camp.StartDate) && (!camp.EndDate.HasValue || date <= camp.EndDate);

    private static MealPlanningMutationResult Invalid(string code, string message) =>
        new(MealPlanningMutationStatus.Invalid, code, message);

    private static MealPlanningMutationResult NotFound() =>
        new(MealPlanningMutationStatus.NotFound, "not_found", "Der Datensatz wurde nicht gefunden.");
}
