namespace ScoutCampPlanner.Catering.Domain;

public enum MealPlanEntryRole
{
    MainDish,
    SideDish,
    Starter,
    Dessert,
    Beverage,
    Other,
}

public enum MealPlanSubscriptionState
{
    FollowStandard,
    Custom,
    NoSupplyRequired,
}

public enum OperationalMealPlanStatus
{
    Current,
    Stale,
    Incomplete,
}

public sealed class MealPlanSnapshot
{
    private MealPlanSnapshot() { }

    public MealPlanSnapshot(
        Guid id, Guid mealPlanId, Guid campId, int version, string contentJson, DateTimeOffset savedAtUtc)
    {
        Id = Required(id, nameof(id));
        MealPlanId = Required(mealPlanId, nameof(mealPlanId));
        CampId = Required(campId, nameof(campId));
        Version = version > 0 ? version : throw new ArgumentOutOfRangeException(nameof(version));
        ContentJson = string.IsNullOrWhiteSpace(contentJson)
            ? throw new ArgumentException("Snapshot content is required.", nameof(contentJson))
            : contentJson;
        SavedAtUtc = savedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MealPlanId { get; private set; }
    public Guid CampId { get; private set; }
    public int Version { get; private set; }
    public string ContentJson { get; private set; } = string.Empty;
    public DateTimeOffset SavedAtUtc { get; private set; }

    private static Guid Required(Guid value, string name) => value == Guid.Empty
        ? throw new ArgumentException("ID is required.", name)
        : value;
}

public sealed class MealPlanOfferGroup
{
    private MealPlanOfferGroup() { }

    public MealPlanOfferGroup(Guid id, Guid mealPlanId, Guid campMealId, string? name, int sortOrder)
    {
        Id = Required(id, nameof(id));
        MealPlanId = Required(mealPlanId, nameof(mealPlanId));
        CampMealId = Required(campMealId, nameof(campMealId));
        Update(name, sortOrder);
    }

    public Guid Id { get; private set; }
    public Guid MealPlanId { get; private set; }
    public Guid CampMealId { get; private set; }
    public string? Name { get; private set; }
    public int SortOrder { get; private set; }

    public void Update(string? name, int sortOrder)
    {
        string? trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (trimmed?.Length > 200) throw new ArgumentException("Name is too long.", nameof(name));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        Name = trimmed;
        SortOrder = sortOrder;
    }

    private static Guid Required(Guid value, string name) => value == Guid.Empty
        ? throw new ArgumentException("ID is required.", name)
        : value;
}

public sealed class MealPlanEntry
{
    private MealPlanEntry() { }

    public MealPlanEntry(
        Guid id, Guid offerGroupId, Guid recipeRevisionId, bool isStandard, string? displayName,
        MealPlanEntryRole? role, string? note, int sortOrder)
    {
        Id = Required(id, nameof(id));
        OfferGroupId = Required(offerGroupId, nameof(offerGroupId));
        RecipeRevisionId = Required(recipeRevisionId, nameof(recipeRevisionId));
        Update(recipeRevisionId, isStandard, displayName, role, note, sortOrder);
    }

    public Guid Id { get; private set; }
    public Guid OfferGroupId { get; private set; }
    public Guid RecipeRevisionId { get; private set; }
    public bool IsStandard { get; private set; }
    public string? DisplayName { get; private set; }
    public MealPlanEntryRole? Role { get; private set; }
    public string? Note { get; private set; }
    public int SortOrder { get; private set; }

    public void Update(
        Guid recipeRevisionId, bool isStandard, string? displayName, MealPlanEntryRole? role,
        string? note, int sortOrder)
    {
        RecipeRevisionId = Required(recipeRevisionId, nameof(recipeRevisionId));
        DisplayName = Optional(displayName, 200, nameof(displayName));
        Note = Optional(note, 2_000, nameof(note));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        IsStandard = isStandard;
        Role = role;
        SortOrder = sortOrder;
    }

    private static string? Optional(string? value, int maximum, string name)
    {
        string? result = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (result?.Length > maximum) throw new ArgumentException("Value is too long.", name);
        return result;
    }

    private static Guid Required(Guid value, string name) => value == Guid.Empty
        ? throw new ArgumentException("ID is required.", name)
        : value;
}

public sealed class CookingUnitGroup
{
    private CookingUnitGroup() { }

    public CookingUnitGroup(Guid id, Guid campId, string name, int sortOrder)
    {
        Id = Required(id, nameof(id));
        CampId = Required(campId, nameof(campId));
        Update(name, sortOrder);
    }

    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public void Update(string name, int sortOrder)
    {
        string trimmed = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Name is required.", nameof(name))
            : name.Trim();
        if (trimmed.Length > 200) throw new ArgumentException("Name is too long.", nameof(name));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        Name = trimmed;
        SortOrder = sortOrder;
    }

    private static Guid Required(Guid value, string name) => value == Guid.Empty
        ? throw new ArgumentException("ID is required.", name)
        : value;
}

public sealed class CookingUnit
{
    private CookingUnit() { }

    public CookingUnit(
        Guid id, Guid campId, string name, int sortOrder, Guid? groupId = null, Guid? standardMealPlanId = null)
    {
        Id = Required(id, nameof(id));
        CampId = Required(campId, nameof(campId));
        Update(name, sortOrder, groupId, standardMealPlanId);
    }

    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public Guid? GroupId { get; private set; }
    public Guid? StandardMealPlanId { get; private set; }

    public void Update(string name, int sortOrder, Guid? groupId, Guid? standardMealPlanId)
    {
        string trimmed = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Name is required.", nameof(name))
            : name.Trim();
        if (trimmed.Length > 200) throw new ArgumentException("Name is too long.", nameof(name));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        if (groupId == Guid.Empty) throw new ArgumentException("Group ID is invalid.", nameof(groupId));
        if (standardMealPlanId == Guid.Empty) throw new ArgumentException("Meal plan ID is invalid.", nameof(standardMealPlanId));
        Name = trimmed;
        SortOrder = sortOrder;
        GroupId = groupId;
        StandardMealPlanId = standardMealPlanId;
    }

    private static Guid Required(Guid value, string name) => value == Guid.Empty
        ? throw new ArgumentException("ID is required.", name)
        : value;
}

public sealed class CookingUnitStructureAssignment
{
    private CookingUnitStructureAssignment() { }

    public CookingUnitStructureAssignment(Guid id, Guid campId, Guid cookingUnitId, Guid? campMealId, Guid structureNodeId)
    {
        Id = Required(id, nameof(id));
        CampId = Required(campId, nameof(campId));
        CookingUnitId = Required(cookingUnitId, nameof(cookingUnitId));
        if (campMealId == Guid.Empty) throw new ArgumentException("Meal ID is invalid.", nameof(campMealId));
        CampMealId = campMealId;
        StructureNodeId = Required(structureNodeId, nameof(structureNodeId));
    }

    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public Guid CookingUnitId { get; private set; }
    public Guid? CampMealId { get; private set; }
    public Guid StructureNodeId { get; private set; }

    private static Guid Required(Guid value, string name) => value == Guid.Empty
        ? throw new ArgumentException("ID is required.", name)
        : value;
}

public sealed class CookingUnitMealState
{
    private CookingUnitMealState() { }

    public CookingUnitMealState(Guid id, Guid campId, Guid cookingUnitId, Guid campMealId)
    {
        Id = Required(id, nameof(id));
        CampId = Required(campId, nameof(campId));
        CookingUnitId = Required(cookingUnitId, nameof(cookingUnitId));
        CampMealId = Required(campMealId, nameof(campMealId));
        SubscriptionState = MealPlanSubscriptionState.FollowStandard;
        Status = OperationalMealPlanStatus.Incomplete;
    }

    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public Guid CookingUnitId { get; private set; }
    public Guid CampMealId { get; private set; }
    public MealPlanSubscriptionState SubscriptionState { get; private set; }
    public decimal? DemandOverride { get; private set; }
    public decimal? CalculatedDemand { get; private set; }
    public decimal? EffectiveDemand { get; private set; }
    public OperationalMealPlanStatus Status { get; private set; }
    public Guid? MealPlanId { get; private set; }
    public int? MealPlanVersion { get; private set; }
    public Guid? MealPlanSnapshotId { get; private set; }
    public string? CalculationSnapshotJson { get; private set; }
    public string? SourceFingerprint { get; private set; }
    public string? WarningsJson { get; private set; }
    public DateTimeOffset? CalculatedAtUtc { get; private set; }

    public void Configure(MealPlanSubscriptionState state, decimal? demandOverride)
    {
        if (demandOverride is < 0) throw new ArgumentOutOfRangeException(nameof(demandOverride));
        bool changed = SubscriptionState != state || DemandOverride != demandOverride;
        SubscriptionState = state;
        DemandOverride = demandOverride;
        if (changed && CalculatedAtUtc.HasValue) Status = OperationalMealPlanStatus.Stale;
    }

    public void MarkStale()
    {
        if (CalculatedAtUtc.HasValue) Status = OperationalMealPlanStatus.Stale;
    }

    public void ApplyCalculation(
        decimal? calculatedDemand, Guid? mealPlanId, int? mealPlanVersion, Guid? mealPlanSnapshotId,
        string calculationSnapshotJson, string sourceFingerprint, string warningsJson, DateTimeOffset calculatedAtUtc,
        bool prerequisitesComplete)
    {
        if (calculatedDemand is < 0) throw new ArgumentOutOfRangeException(nameof(calculatedDemand));
        CalculatedDemand = calculatedDemand;
        EffectiveDemand = DemandOverride ?? calculatedDemand;
        MealPlanId = mealPlanId;
        MealPlanVersion = mealPlanVersion;
        MealPlanSnapshotId = mealPlanSnapshotId;
        CalculationSnapshotJson = calculationSnapshotJson;
        SourceFingerprint = string.IsNullOrWhiteSpace(sourceFingerprint)
            ? throw new ArgumentException("Source fingerprint is required.", nameof(sourceFingerprint))
            : sourceFingerprint;
        WarningsJson = warningsJson;
        CalculatedAtUtc = calculatedAtUtc;
        Status = prerequisitesComplete ? OperationalMealPlanStatus.Current : OperationalMealPlanStatus.Incomplete;
    }

    private static Guid Required(Guid value, string name) => value == Guid.Empty
        ? throw new ArgumentException("ID is required.", name)
        : value;
}

public sealed class CookingUnitMealOfferTarget
{
    private CookingUnitMealOfferTarget() { }

    public CookingUnitMealOfferTarget(Guid id, Guid cookingUnitMealStateId, Guid offerGroupId, decimal? targetOverride)
    {
        Id = Required(id, nameof(id));
        CookingUnitMealStateId = Required(cookingUnitMealStateId, nameof(cookingUnitMealStateId));
        OfferGroupId = Required(offerGroupId, nameof(offerGroupId));
        SetOverride(targetOverride);
    }

    public Guid Id { get; private set; }
    public Guid CookingUnitMealStateId { get; private set; }
    public Guid OfferGroupId { get; private set; }
    public decimal? TargetOverride { get; private set; }

    public void SetOverride(decimal? value)
    {
        if (value is < 0) throw new ArgumentOutOfRangeException(nameof(value));
        TargetOverride = value;
    }

    private static Guid Required(Guid value, string name) => value == Guid.Empty
        ? throw new ArgumentException("ID is required.", name)
        : value;
}

public sealed class CookingUnitMealRecipeChoice
{
    private CookingUnitMealRecipeChoice() { }

    public CookingUnitMealRecipeChoice(
        Guid id, Guid cookingUnitMealStateId, Guid recipeRevisionId, Guid? offerGroupId,
        Guid? mealPlanEntryId, int sortOrder)
    {
        Id = Required(id, nameof(id));
        CookingUnitMealStateId = Required(cookingUnitMealStateId, nameof(cookingUnitMealStateId));
        RecipeRevisionId = Required(recipeRevisionId, nameof(recipeRevisionId));
        if (offerGroupId == Guid.Empty) throw new ArgumentException("Offer group ID is invalid.", nameof(offerGroupId));
        if (mealPlanEntryId == Guid.Empty) throw new ArgumentException("Entry ID is invalid.", nameof(mealPlanEntryId));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        OfferGroupId = offerGroupId;
        MealPlanEntryId = mealPlanEntryId;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }
    public Guid CookingUnitMealStateId { get; private set; }
    public Guid RecipeRevisionId { get; private set; }
    public Guid? OfferGroupId { get; private set; }
    public Guid? MealPlanEntryId { get; private set; }
    public int SortOrder { get; private set; }

    private static Guid Required(Guid value, string name) => value == Guid.Empty
        ? throw new ArgumentException("ID is required.", name)
        : value;
}
