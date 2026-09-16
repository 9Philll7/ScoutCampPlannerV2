using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddIngredientSubstanceContents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngredientRevisionSubstanceContents",
                schema: "catering",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    AmountUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReferenceUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReviewState = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionSubstanceContents", x => new { x.IngredientRevisionId, x.SubstanceId });
                    table.CheckConstraint("CK_IngredientRevisionSubstanceContents_Amount_NonNegative", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_IngredientRevisionSubstanceContents_ReferenceQuantity_Posit~", "\"ReferenceQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientRevisionSubstanceContents_IngredientIntoleranceDe~",
                        column: x => x.SubstanceId,
                        principalSchema: "catering",
                        principalTable: "IngredientIntoleranceDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionSubstanceContents_IngredientRevisions_Ing~",
                        column: x => x.IngredientRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionSubstanceContents_MeasurementUnits_Amount~",
                        column: x => x.AmountUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionSubstanceContents_MeasurementUnits_Refere~",
                        column: x => x.ReferenceUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantSubstanceContentOverrides",
                schema: "catering",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    AmountUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReferenceUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReviewState = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantSubstanceContentOverrides", x => new { x.VariantRevisionId, x.SubstanceId });
                    table.CheckConstraint("CK_IngredientVariantSubstanceContentOverrides_Amount_NonNegati~", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_IngredientVariantSubstanceContentOverrides_ReferenceQuantit~", "\"ReferenceQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientVariantSubstanceContentOverrides_IngredientIntole~",
                        column: x => x.SubstanceId,
                        principalSchema: "catering",
                        principalTable: "IngredientIntoleranceDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientVariantSubstanceContentOverrides_IngredientVarian~",
                        column: x => x.VariantRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientVariantSubstanceContentOverrides_MeasurementUnits~",
                        column: x => x.AmountUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientVariantSubstanceContentOverrides_MeasurementUnit~1",
                        column: x => x.ReferenceUnitId,
                        principalSchema: "catering",
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000001"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000002"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000003"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000006"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000007"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000008"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000009"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000010"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionSubstanceContents_AmountUnitId",
                schema: "catering",
                table: "IngredientRevisionSubstanceContents",
                column: "AmountUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionSubstanceContents_ReferenceUnitId",
                schema: "catering",
                table: "IngredientRevisionSubstanceContents",
                column: "ReferenceUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionSubstanceContents_SubstanceId",
                schema: "catering",
                table: "IngredientRevisionSubstanceContents",
                column: "SubstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantSubstanceContentOverrides_AmountUnitId",
                schema: "catering",
                table: "IngredientVariantSubstanceContentOverrides",
                column: "AmountUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantSubstanceContentOverrides_ReferenceUnitId",
                schema: "catering",
                table: "IngredientVariantSubstanceContentOverrides",
                column: "ReferenceUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantSubstanceContentOverrides_SubstanceId",
                schema: "catering",
                table: "IngredientVariantSubstanceContentOverrides",
                column: "SubstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientRevisionSubstanceContents",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "IngredientVariantSubstanceContentOverrides",
                schema: "catering");

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000001"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000002"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000003"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000006"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000007"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000008"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000009"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000010"),
                column: "IsQuantityDependent",
                value: false);
        }
    }
}
