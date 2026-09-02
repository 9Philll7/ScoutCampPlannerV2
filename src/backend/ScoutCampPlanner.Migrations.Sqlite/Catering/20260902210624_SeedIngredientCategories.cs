using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class SeedIngredientCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "IngredientCategories",
                columns: new[] { "Id", "Code", "Name", "NormalizedName", "ParentCategoryId", "Status" },
                values: new object[,]
                {
                    { new Guid("51111111-1111-1111-1111-000000000001"), "CEREALS_GRAIN_PRODUCTS", "Getreide & Getreideprodukte", "GETREIDE & GETREIDEPRODUKTE", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000002"), "LEGUMES", "Hülsenfrüchte", "HÜLSENFRÜCHTE", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000003"), "VEGETABLES", "Gemüse", "GEMÜSE", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000004"), "FRUIT", "Obst", "OBST", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000005"), "NUTS_SEEDS", "Nüsse & Samen", "NÜSSE & SAMEN", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000006"), "HERBS_SPICES", "Kräuter & Gewürze", "KRÄUTER & GEWÜRZE", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000007"), "MEAT_SAUSAGE", "Fleisch & Wurstwaren", "FLEISCH & WURSTWAREN", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000008"), "FISH_SEAFOOD", "Fisch & Meeresfrüchte", "FISCH & MEERESFRÜCHTE", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000009"), "DAIRY", "Milchprodukte", "MILCHPRODUKTE", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000010"), "EGGS", "Eier", "EIER", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000011"), "FATS_OILS", "Fette & Öle", "FETTE & ÖLE", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000012"), "SWEETENERS", "Süßungsmittel", "SÜßUNGSMITTEL", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000013"), "BAKING_INGREDIENTS", "Backzutaten", "BACKZUTATEN", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000014"), "BEVERAGES", "Getränke", "GETRÄNKE", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000015"), "PREPARED_PRESERVED", "Fertigprodukte & Konserven", "FERTIGPRODUKTE & KONSERVEN", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000016"), "SAUCES_CONDIMENTS", "Saucen & Würzmittel", "SAUCEN & WÜRZMITTEL", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000017"), "YEAST_CULTURES", "Hefen & Kulturen", "HEFEN & KULTUREN", null, 0 },
                    { new Guid("51111111-1111-1111-1111-000000000018"), "OTHER", "Sonstiges", "SONSTIGES", null, 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000001"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000002"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000003"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000004"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000005"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000006"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000007"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000008"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000009"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000010"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000011"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000012"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000013"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000014"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000015"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000016"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000017"));

            migrationBuilder.DeleteData(
                table: "IngredientCategories",
                keyColumn: "Id",
                keyValue: new Guid("51111111-1111-1111-1111-000000000018"));
        }
    }
}
