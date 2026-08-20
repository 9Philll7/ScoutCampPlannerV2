using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddIngredientCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Allergens",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allergens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BaseIngredients",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginInformation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseIngredients", x => x.Id);
                    table.CheckConstraint("CK_BaseIngredients_ScopeOwner", "(\"ScopeType\" = 0 AND \"ScopeId\" IS NULL) OR (\"ScopeType\" IN (1, 2) AND \"ScopeId\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "DietaryRequirements",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietaryRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Intolerances",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Intolerances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeasurementUnits",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Dimension = table.Column<int>(type: "integer", nullable: false),
                    BaseUnitFactor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeasurementUnits", x => x.Id);
                    table.CheckConstraint("CK_MeasurementUnits_BaseUnitFactor_Positive", "\"BaseUnitFactor\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "BaseIngredientAllergens",
                schema: "catering",
                columns: table => new
                {
                    BaseIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllergenId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseIngredientAllergens", x => new { x.BaseIngredientId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_BaseIngredientAllergens_Allergens_AllergenId",
                        column: x => x.AllergenId,
                        principalSchema: "catering",
                        principalTable: "Allergens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaseIngredientAllergens_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalSchema: "catering",
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariants",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientVariants_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalSchema: "catering",
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BaseIngredientDietaryRequirements",
                schema: "catering",
                columns: table => new
                {
                    BaseIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DietaryRequirementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseIngredientDietaryRequirements", x => new { x.BaseIngredientId, x.DietaryRequirementId });
                    table.ForeignKey(
                        name: "FK_BaseIngredientDietaryRequirements_BaseIngredients_BaseIngre~",
                        column: x => x.BaseIngredientId,
                        principalSchema: "catering",
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaseIngredientDietaryRequirements_DietaryRequirements_Dieta~",
                        column: x => x.DietaryRequirementId,
                        principalSchema: "catering",
                        principalTable: "DietaryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BaseIngredientIntolerances",
                schema: "catering",
                columns: table => new
                {
                    BaseIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntoleranceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseIngredientIntolerances", x => new { x.BaseIngredientId, x.IntoleranceId });
                    table.ForeignKey(
                        name: "FK_BaseIngredientIntolerances_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalSchema: "catering",
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaseIngredientIntolerances_Intolerances_IntoleranceId",
                        column: x => x.IntoleranceId,
                        principalSchema: "catering",
                        principalTable: "Intolerances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientUnitConversions",
                schema: "catering",
                columns: table => new
                {
                    BaseIngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceQuantityPerUnit = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientUnitConversions", x => new { x.BaseIngredientId, x.UnitId });
                    table.CheckConstraint("CK_IngredientUnitConversions_Factor_Positive", "\"ReferenceQuantityPerUnit\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientUnitConversions_BaseIngredients_BaseIngredientId",
                        column: x => x.BaseIngredientId,
                        principalSchema: "catering",
                        principalTable: "BaseIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientUnitConversions_MeasurementUnits_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allergens_NormalizedName",
                schema: "catering",
                table: "Allergens",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredientAllergens_AllergenId",
                schema: "catering",
                table: "BaseIngredientAllergens",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredientDietaryRequirements_DietaryRequirementId",
                schema: "catering",
                table: "BaseIngredientDietaryRequirements",
                column: "DietaryRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredientIntolerances_IntoleranceId",
                schema: "catering",
                table: "BaseIngredientIntolerances",
                column: "IntoleranceId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredients_ScopeId",
                schema: "catering",
                table: "BaseIngredients",
                column: "ScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredients_ScopeType_NormalizedName",
                schema: "catering",
                table: "BaseIngredients",
                columns: new[] { "ScopeType", "NormalizedName" },
                unique: true,
                filter: "\"ScopeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BaseIngredients_ScopeType_ScopeId_NormalizedName",
                schema: "catering",
                table: "BaseIngredients",
                columns: new[] { "ScopeType", "ScopeId", "NormalizedName" },
                unique: true,
                filter: "\"ScopeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DietaryRequirements_NormalizedName",
                schema: "catering",
                table: "DietaryRequirements",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientUnitConversions_UnitId",
                schema: "catering",
                table: "IngredientUnitConversions",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariants_BaseIngredientId_NormalizedName",
                schema: "catering",
                table: "IngredientVariants",
                columns: new[] { "BaseIngredientId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Intolerances_NormalizedName",
                schema: "catering",
                table: "Intolerances",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementUnits_Dimension_Symbol",
                schema: "catering",
                table: "MeasurementUnits",
                columns: new[] { "Dimension", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementUnits_NormalizedName",
                schema: "catering",
                table: "MeasurementUnits",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaseIngredientAllergens",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "BaseIngredientDietaryRequirements",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "BaseIngredientIntolerances",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientUnitConversions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientVariants",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "Allergens",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "DietaryRequirements",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "Intolerances",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "MeasurementUnits",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "BaseIngredients",
                schema: "catering");
        }
    }
}
