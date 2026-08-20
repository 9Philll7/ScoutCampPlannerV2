namespace ScoutCampPlanner.Catering.Domain;

public sealed class IngredientVariant
{
    private IngredientVariant() { }

    public IngredientVariant(Guid id, Guid baseIngredientId, string name)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Variant ID is required.", nameof(id)) : id;
        BaseIngredientId = baseIngredientId == Guid.Empty
            ? throw new ArgumentException("Base ingredient ID is required.", nameof(baseIngredientId))
            : baseIngredientId;
        Rename(name);
    }

    public Guid Id { get; private set; }
    public Guid BaseIngredientId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;

    public void Rename(string name) =>
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 200);
}
