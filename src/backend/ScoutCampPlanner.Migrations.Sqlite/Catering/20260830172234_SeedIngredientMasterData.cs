using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class SeedIngredientMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "IngredientAllergenDefinitions",
                columns: new[] { "Id", "Code", "IsEuMajorAllergen", "Name", "ParentAllergenId", "Status" },
                values: new object[,]
                {
                    { new Guid("21111111-1111-1111-1111-000000000001"), "GLUTEN_CEREALS", true, "Glutenhaltiges Getreide", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000002"), "CRUSTACEANS", true, "Krebstiere", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000003"), "EGGS", true, "Eier", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000004"), "FISH", true, "Fisch", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000005"), "PEANUTS", true, "Erdnüsse", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000006"), "SOYBEANS", true, "Sojabohnen", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000007"), "MILK", true, "Milch", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000008"), "TREE_NUTS", true, "Schalenfrüchte", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000009"), "CELERY", true, "Sellerie", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000010"), "MUSTARD", true, "Senf", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000011"), "SESAME", true, "Sesamsamen", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000012"), "SULPHUR_DIOXIDE_AND_SULPHITES", true, "Schwefeldioxid und Sulfite", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000013"), "LUPIN", true, "Lupinen", null, 0 },
                    { new Guid("21111111-1111-1111-1111-000000000014"), "MOLLUSCS", true, "Weichtiere", null, 0 }
                });

            migrationBuilder.InsertData(
                table: "IngredientIntoleranceDefinitions",
                columns: new[] { "Id", "Code", "IsQuantityDependent", "Name", "Status" },
                values: new object[,]
                {
                    { new Guid("31111111-1111-1111-1111-000000000001"), "LACTOSE", false, "Laktose", 0 },
                    { new Guid("31111111-1111-1111-1111-000000000002"), "FRUCTOSE", false, "Fruktose", 0 },
                    { new Guid("31111111-1111-1111-1111-000000000003"), "SORBITOL", false, "Sorbit", 0 },
                    { new Guid("31111111-1111-1111-1111-000000000004"), "HISTAMINE", false, "Histamin", 0 },
                    { new Guid("31111111-1111-1111-1111-000000000005"), "GLUTEN", false, "Gluten", 0 },
                    { new Guid("31111111-1111-1111-1111-000000000006"), "FRUCTANS", false, "Fruktane", 0 },
                    { new Guid("31111111-1111-1111-1111-000000000007"), "GALACTANS", false, "Galaktane", 0 },
                    { new Guid("31111111-1111-1111-1111-000000000008"), "MANNITOL", false, "Mannit", 0 },
                    { new Guid("31111111-1111-1111-1111-000000000009"), "XYLITOL", false, "Xylit", 0 },
                    { new Guid("31111111-1111-1111-1111-000000000010"), "OTHER_POLYOLS", false, "Andere Polyole", 0 }
                });

            migrationBuilder.UpdateData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000003"),
                column: "Name",
                value: "Ungeklärter Ursprung");

            migrationBuilder.InsertData(
                table: "IngredientOriginProperties",
                columns: new[] { "Id", "Code", "IsAnimalOrigin", "Name", "Status" },
                values: new object[,]
                {
                    { new Guid("41111111-1111-1111-1111-000000000001"), "PLANT", false, "Pflanzlich", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000002"), "FUNGI", false, "Pilze", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000003"), "MINERAL", false, "Mineralisch", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000004"), "SYNTHETIC", false, "Synthetisch", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000005"), "MICROBIAL", false, "Mikrobiell", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000006"), "MEAT", true, "Fleisch", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000007"), "POULTRY", true, "Geflügel", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000008"), "FISH", true, "Fisch", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000009"), "CRUSTACEAN", true, "Krebstier", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000010"), "MOLLUSC", true, "Weichtier", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000011"), "DAIRY", true, "Milcherzeugnis", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000012"), "EGG", true, "Ei", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000013"), "HONEY", true, "Honig", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000014"), "INSECT", true, "Insekt", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000015"), "ANIMAL_FAT", true, "Tierisches Fett", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000016"), "GELATIN", true, "Gelatine", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000017"), "ANIMAL_RENNET", true, "Tierisches Lab", 0 },
                    { new Guid("41111111-1111-1111-1111-000000000018"), "OTHER_ANIMAL_DERIVED", true, "Sonstiger tierischer Ursprung", 0 }
                });

            migrationBuilder.InsertData(
                table: "IngredientAllergenDefinitions",
                columns: new[] { "Id", "Code", "IsEuMajorAllergen", "Name", "ParentAllergenId", "Status" },
                values: new object[,]
                {
                    { new Guid("21111111-1111-1111-1111-000000000015"), "WHEAT", false, "Weizen", new Guid("21111111-1111-1111-1111-000000000001"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000016"), "RYE", false, "Roggen", new Guid("21111111-1111-1111-1111-000000000001"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000017"), "BARLEY", false, "Gerste", new Guid("21111111-1111-1111-1111-000000000001"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000018"), "OATS", false, "Hafer", new Guid("21111111-1111-1111-1111-000000000001"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000019"), "SPELT", false, "Dinkel", new Guid("21111111-1111-1111-1111-000000000001"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000020"), "KHORASAN_WHEAT", false, "Khorasan-Weizen", new Guid("21111111-1111-1111-1111-000000000001"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000021"), "HYBRID_STRAINS", false, "Hybridstämme", new Guid("21111111-1111-1111-1111-000000000001"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000022"), "ALMONDS", false, "Mandeln", new Guid("21111111-1111-1111-1111-000000000008"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000023"), "HAZELNUTS", false, "Haselnüsse", new Guid("21111111-1111-1111-1111-000000000008"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000024"), "WALNUTS", false, "Walnüsse", new Guid("21111111-1111-1111-1111-000000000008"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000025"), "CASHEWS", false, "Cashewkerne", new Guid("21111111-1111-1111-1111-000000000008"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000026"), "PECANS", false, "Pekannüsse", new Guid("21111111-1111-1111-1111-000000000008"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000027"), "BRAZIL_NUTS", false, "Paranüsse", new Guid("21111111-1111-1111-1111-000000000008"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000028"), "PISTACHIOS", false, "Pistazien", new Guid("21111111-1111-1111-1111-000000000008"), 0 },
                    { new Guid("21111111-1111-1111-1111-000000000029"), "MACADAMIA_NUTS", false, "Macadamianüsse", new Guid("21111111-1111-1111-1111-000000000008"), 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000002"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000003"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000004"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000005"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000006"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000007"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000009"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000010"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000011"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000012"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000013"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000014"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000015"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000016"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000017"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000018"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000019"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000020"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000021"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000022"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000023"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000024"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000025"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000026"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000027"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000028"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000029"));

            migrationBuilder.DeleteData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000001"));

            migrationBuilder.DeleteData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000002"));

            migrationBuilder.DeleteData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000003"));

            migrationBuilder.DeleteData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000004"));

            migrationBuilder.DeleteData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000005"));

            migrationBuilder.DeleteData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000006"));

            migrationBuilder.DeleteData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000007"));

            migrationBuilder.DeleteData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000008"));

            migrationBuilder.DeleteData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000009"));

            migrationBuilder.DeleteData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000010"));

            migrationBuilder.UpdateData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-000000000003"),
                column: "Name",
                value: "Unbekannte Herkunft");

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000001"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000002"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000003"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000004"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000005"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000006"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000007"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000008"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000009"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000010"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000011"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000012"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000013"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000014"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000015"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000016"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000017"));

            migrationBuilder.DeleteData(
                table: "IngredientOriginProperties",
                keyColumn: "Id",
                keyValue: new Guid("41111111-1111-1111-1111-000000000018"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000001"));

            migrationBuilder.DeleteData(
                table: "IngredientAllergenDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("21111111-1111-1111-1111-000000000008"));
        }
    }
}
