using Microsoft.EntityFrameworkCore;

namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

internal static class IngredientMasterDataSeed
{
    private const int Active = 0;

    public static void Configure(ModelBuilder modelBuilder)
    {
        IngredientAllergenDefinitionRecord glutenCereals = Allergen(1, "GLUTEN_CEREALS", "Glutenhaltiges Getreide", true);
        IngredientAllergenDefinitionRecord treeNuts = Allergen(8, "TREE_NUTS", "Schalenfrüchte", true);
        modelBuilder.Entity<IngredientAllergenDefinitionRecord>().HasData(
            glutenCereals,
            Allergen(2, "CRUSTACEANS", "Krebstiere", true),
            Allergen(3, "EGGS", "Eier", true),
            Allergen(4, "FISH", "Fisch", true),
            Allergen(5, "PEANUTS", "Erdnüsse", true),
            Allergen(6, "SOYBEANS", "Sojabohnen", true),
            Allergen(7, "MILK", "Milch", true),
            treeNuts,
            Allergen(9, "CELERY", "Sellerie", true),
            Allergen(10, "MUSTARD", "Senf", true),
            Allergen(11, "SESAME", "Sesamsamen", true),
            Allergen(12, "SULPHUR_DIOXIDE_AND_SULPHITES", "Schwefeldioxid und Sulfite", true),
            Allergen(13, "LUPIN", "Lupinen", true),
            Allergen(14, "MOLLUSCS", "Weichtiere", true),
            Allergen(15, "WHEAT", "Weizen", false, glutenCereals.Id),
            Allergen(16, "RYE", "Roggen", false, glutenCereals.Id),
            Allergen(17, "BARLEY", "Gerste", false, glutenCereals.Id),
            Allergen(18, "OATS", "Hafer", false, glutenCereals.Id),
            Allergen(19, "SPELT", "Dinkel", false, glutenCereals.Id),
            Allergen(20, "KHORASAN_WHEAT", "Khorasan-Weizen", false, glutenCereals.Id),
            Allergen(21, "HYBRID_STRAINS", "Hybridstämme", false, glutenCereals.Id),
            Allergen(22, "ALMONDS", "Mandeln", false, treeNuts.Id),
            Allergen(23, "HAZELNUTS", "Haselnüsse", false, treeNuts.Id),
            Allergen(24, "WALNUTS", "Walnüsse", false, treeNuts.Id),
            Allergen(25, "CASHEWS", "Cashewkerne", false, treeNuts.Id),
            Allergen(26, "PECANS", "Pekannüsse", false, treeNuts.Id),
            Allergen(27, "BRAZIL_NUTS", "Paranüsse", false, treeNuts.Id),
            Allergen(28, "PISTACHIOS", "Pistazien", false, treeNuts.Id),
            Allergen(29, "MACADAMIA_NUTS", "Macadamianüsse", false, treeNuts.Id));

        modelBuilder.Entity<IngredientIntoleranceDefinitionRecord>().HasData(
            Intolerance(1, "LACTOSE", "Laktose"),
            Intolerance(2, "FRUCTOSE", "Fruktose"),
            Intolerance(3, "SORBITOL", "Sorbit"),
            Intolerance(4, "HISTAMINE", "Histamin"),
            Intolerance(5, "GLUTEN", "Gluten"),
            Intolerance(6, "FRUCTANS", "Fruktane"),
            Intolerance(7, "GALACTANS", "Galaktane"),
            Intolerance(8, "MANNITOL", "Mannit"),
            Intolerance(9, "XYLITOL", "Xylit"),
            Intolerance(10, "OTHER_POLYOLS", "Andere Polyole"));

        modelBuilder.Entity<IngredientOriginPropertyRecord>().HasData(
            Origin(1, "PLANT", "Pflanzlich", false),
            Origin(2, "FUNGI", "Pilze", false),
            Origin(3, "MINERAL", "Mineralisch", false),
            Origin(4, "SYNTHETIC", "Synthetisch", false),
            Origin(5, "MICROBIAL", "Mikrobiell", false),
            Origin(6, "MEAT", "Fleisch", true),
            Origin(7, "POULTRY", "Geflügel", true),
            Origin(8, "FISH", "Fisch", true),
            Origin(9, "CRUSTACEAN", "Krebstier", true),
            Origin(10, "MOLLUSC", "Weichtier", true),
            Origin(11, "DAIRY", "Milcherzeugnis", true),
            Origin(12, "EGG", "Ei", true),
            Origin(13, "HONEY", "Honig", true),
            Origin(14, "INSECT", "Insekt", true),
            Origin(15, "ANIMAL_FAT", "Tierisches Fett", true),
            Origin(16, "GELATIN", "Gelatine", true),
            Origin(17, "ANIMAL_RENNET", "Tierisches Lab", true),
            Origin(18, "OTHER_ANIMAL_DERIVED", "Sonstiger tierischer Ursprung", true),
            Origin(19, "UNKNOWN_ORIGIN", "Ungeklärter Ursprung", false));
    }

    private static IngredientAllergenDefinitionRecord Allergen(
        int number,
        string code,
        string name,
        bool isMajor,
        Guid? parentId = null) => new()
        {
            Id = Id("21111111", number),
            ParentAllergenId = parentId,
            Code = code,
            Name = name,
            IsEuMajorAllergen = isMajor,
            Status = Active,
        };

    private static IngredientIntoleranceDefinitionRecord Intolerance(int number, string code, string name) => new()
    {
        Id = Id("31111111", number),
        Code = code,
        Name = name,
        IsQuantityDependent = false,
        Status = Active,
    };

    private static IngredientOriginPropertyRecord Origin(
        int number,
        string code,
        string name,
        bool isAnimalOrigin) => new()
        {
            Id = number == 19
                ? Guid.Parse("11111111-1111-1111-1111-000000000003")
                : Id("41111111", number),
            Code = code,
            Name = name,
            IsAnimalOrigin = isAnimalOrigin,
            Status = Active,
        };

    private static Guid Id(string prefix, int number) =>
        Guid.Parse($"{prefix}-1111-1111-1111-{number:D12}");
}
