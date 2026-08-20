using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddIngredientCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Allergens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allergens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BaseIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScopeType = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OriginInformation = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseIngredients", x => x.Id);
                    table.CheckConstraint("CK_BaseIngredients_ScopeOwner", "(\"ScopeType\" = 0 AND \"ScopeId\" IS NULL) OR (\"ScopeType\" IN (1, 2) AND \"ScopeId\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "DietaryRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietaryRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Intolerances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Intolerances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeasurementUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Dimension = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseUnitFactor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeasurementUnits", x => x.Id);
                    table.CheckConstraint("CK_MeasurementUnits_BaseUnitFactor_Positive", "\"BaseUnitFactor\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "BaseIngredientAllergens",
                columns: table => new
                {
                    BaseIngredientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllergenId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseIngredientAllergens", x => new { x.BaseIngredientId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_BaseIngredientAllergens_Allergens_AllergenId",
                        column: x => x.AllergenId,
                        principalTable: "Allergens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaseIngredientAllergens_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BaseIngredientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientVariants_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BaseIngredientDietaryRequirements",
                columns: table => new
                {
                    BaseIngredientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DietaryRequirementId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseIngredientDietaryRequirements", x => new { x.BaseIngredientId, x.DietaryRequirementId });
                    table.ForeignKey(
                        name: "FK_BaseIngredientDietaryRequirements_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaseIngredientDietaryRequirements_DietaryRequirements_DietaryRequirementId",
                        column: x => x.DietaryRequirementId,
                        principalTable: "DietaryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BaseIngredientIntolerances",
                columns: table => new
                {
                    BaseIngredientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IntoleranceId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseIngredientIntolerances", x => new { x.BaseIngredientId, x.IntoleranceId });
                    table.ForeignKey(
                        name: "FK_BaseIngredientIntolerances_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaseIngredientIntolerances_Intolerances_IntoleranceId",
                        column: x => x.IntoleranceId,
                        principalTable: "Intolerances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientUnitConversions",
                columns: table => new
                {
                    BaseIngredientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceQuantityPerUnit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientUnitConversions", x => new { x.BaseIngredientId, x.UnitId });
                    table.CheckConstraint("CK_IngredientUnitConversions_Factor_Positive", "\"ReferenceQuantityPerUnit\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientUnitConversions_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientUnitConversions_MeasurementUnits_UnitId",
                        column: x => x.UnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allergens_NormalizedName",
                table: "Allergens",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredientAllergens_AllergenId",
                table: "BaseIngredientAllergens",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredientDietaryRequirements_DietaryRequirementId",
                table: "BaseIngredientDietaryRequirements",
                column: "DietaryRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredientIntolerances_IntoleranceId",
                table: "BaseIngredientIntolerances",
                column: "IntoleranceId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredients_ScopeId",
                table: "BaseIngredients",
                column: "ScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredients_ScopeType_NormalizedName",
                table: "BaseIngredients",
                columns: new[] { "ScopeType", "NormalizedName" },
                unique: true,
                filter: "\"ScopeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredients_ScopeType_ScopeId_NormalizedName",
                table: "BaseIngredients",
                columns: new[] { "ScopeType", "ScopeId", "NormalizedName" },
                unique: true,
                filter: "\"ScopeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DietaryRequirements_NormalizedName",
                table: "DietaryRequirements",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientUnitConversions_UnitId",
                table: "IngredientUnitConversions",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariants_BaseIngredientId_NormalizedName",
                table: "IngredientVariants",
                columns: new[] { "BaseIngredientId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Intolerances_NormalizedName",
                table: "Intolerances",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementUnits_Dimension_Symbol",
                table: "MeasurementUnits",
                columns: new[] { "Dimension", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementUnits_NormalizedName",
                table: "MeasurementUnits",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaseIngredientAllergens");

            migrationBuilder.DropTable(
                name: "BaseIngredientDietaryRequirements");

            migrationBuilder.DropTable(
                name: "BaseIngredientIntolerances");

            migrationBuilder.DropTable(
                name: "IngredientUnitConversions");

            migrationBuilder.DropTable(
                name: "IngredientVariants");

            migrationBuilder.DropTable(
                name: "Allergens");

            migrationBuilder.DropTable(
                name: "DietaryRequirements");

            migrationBuilder.DropTable(
                name: "Intolerances");

            migrationBuilder.DropTable(
                name: "MeasurementUnits");

            migrationBuilder.DropTable(
                name: "BaseIngredients");
        }
    }
}
