namespace ScoutCampPlanner.Catering.Domain;

public enum IngredientCatalogEntryStatus
{
    Active,
    Archived,
}

public sealed class IngredientCategory
{
    private IngredientCategory() { }

    public IngredientCategory(Guid id, string code, string name, Guid? parentCategoryId = null)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Category ID is required.", nameof(id)) : id;
        Code = NormalizeCode(code);
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 150);
        if (parentCategoryId == Guid.Empty)
            throw new ArgumentException("Parent category ID must be non-empty when specified.", nameof(parentCategoryId));
        ParentCategoryId = parentCategoryId;
    }

    public Guid Id { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public IngredientCatalogEntryStatus Status { get; private set; }

    public void Archive() => Status = IngredientCatalogEntryStatus.Archived;

    private static string NormalizeCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > 80 || normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new ArgumentException("Category code must use at most 80 ASCII letters, digits, or underscores.", nameof(value));
        return normalized;
    }
}

