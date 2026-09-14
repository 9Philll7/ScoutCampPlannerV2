using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddIngredientNutritionProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngredientRevisionNutritionProfiles",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    ReferenceUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnergyKilojoules = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    FatGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    SaturatedFatGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    CarbohydrateGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    SugarsGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    ProteinGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    SaltGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    FiberGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReviewState = table.Column<int>(type: "INTEGER", nullable: false),
                    ReferenceDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionNutritionProfiles", x => x.IngredientRevisionId);
                    table.CheckConstraint("CK_IngredientRevisionNutritionProfiles_ReferenceQuantity_Positive", "\"ReferenceQuantity\" > 0");
                    table.CheckConstraint("CK_IngredientRevisionNutritionProfiles_Values_NonNegative", "(\"EnergyKilojoules\" IS NULL OR \"EnergyKilojoules\" >= 0) AND (\"FatGrams\" IS NULL OR \"FatGrams\" >= 0) AND (\"SaturatedFatGrams\" IS NULL OR \"SaturatedFatGrams\" >= 0) AND (\"CarbohydrateGrams\" IS NULL OR \"CarbohydrateGrams\" >= 0) AND (\"SugarsGrams\" IS NULL OR \"SugarsGrams\" >= 0) AND (\"ProteinGrams\" IS NULL OR \"ProteinGrams\" >= 0) AND (\"SaltGrams\" IS NULL OR \"SaltGrams\" >= 0) AND (\"FiberGrams\" IS NULL OR \"FiberGrams\" >= 0)");
                    table.ForeignKey(
                        name: "FK_IngredientRevisionNutritionProfiles_IngredientRevisions_IngredientRevisionId",
                        column: x => x.IngredientRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionNutritionProfiles_MeasurementUnits_ReferenceUnitId",
                        column: x => x.ReferenceUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantNutritionProfiles",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    ReferenceUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnergyKilojoules = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    FatGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    SaturatedFatGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    CarbohydrateGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    SugarsGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    ProteinGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    SaltGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    FiberGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReviewState = table.Column<int>(type: "INTEGER", nullable: false),
                    ReferenceDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantNutritionProfiles", x => x.VariantRevisionId);
                    table.CheckConstraint("CK_IngredientVariantNutritionProfiles_ReferenceQuantity_Positive", "\"ReferenceQuantity\" > 0");
                    table.CheckConstraint("CK_IngredientVariantNutritionProfiles_Values_NonNegative", "(\"EnergyKilojoules\" IS NULL OR \"EnergyKilojoules\" >= 0) AND (\"FatGrams\" IS NULL OR \"FatGrams\" >= 0) AND (\"SaturatedFatGrams\" IS NULL OR \"SaturatedFatGrams\" >= 0) AND (\"CarbohydrateGrams\" IS NULL OR \"CarbohydrateGrams\" >= 0) AND (\"SugarsGrams\" IS NULL OR \"SugarsGrams\" >= 0) AND (\"ProteinGrams\" IS NULL OR \"ProteinGrams\" >= 0) AND (\"SaltGrams\" IS NULL OR \"SaltGrams\" >= 0) AND (\"FiberGrams\" IS NULL OR \"FiberGrams\" >= 0)");
                    table.ForeignKey(
                        name: "FK_IngredientVariantNutritionProfiles_IngredientVariantRevisions_VariantRevisionId",
                        column: x => x.VariantRevisionId,
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientVariantNutritionProfiles_MeasurementUnits_ReferenceUnitId",
                        column: x => x.ReferenceUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionNutritionProfiles_ReferenceUnitId",
                table: "IngredientRevisionNutritionProfiles",
                column: "ReferenceUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantNutritionProfiles_ReferenceUnitId",
                table: "IngredientVariantNutritionProfiles",
                column: "ReferenceUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientRevisionNutritionProfiles");

            migrationBuilder.DropTable(
                name: "IngredientVariantNutritionProfiles");
        }
    }
}
