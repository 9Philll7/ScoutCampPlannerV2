using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddIngredientSubstanceContents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngredientRevisionSubstanceContents",
                columns: table => new
                {
                    IngredientRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    AmountUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    ReferenceUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReviewState = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientRevisionSubstanceContents", x => new { x.IngredientRevisionId, x.SubstanceId });
                    table.CheckConstraint("CK_IngredientRevisionSubstanceContents_Amount_NonNegative", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_IngredientRevisionSubstanceContents_ReferenceQuantity_Positive", "\"ReferenceQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientRevisionSubstanceContents_IngredientIntoleranceDefinitions_SubstanceId",
                        column: x => x.SubstanceId,
                        principalTable: "IngredientIntoleranceDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionSubstanceContents_IngredientRevisions_IngredientRevisionId",
                        column: x => x.IngredientRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionSubstanceContents_MeasurementUnits_AmountUnitId",
                        column: x => x.AmountUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientRevisionSubstanceContents_MeasurementUnits_ReferenceUnitId",
                        column: x => x.ReferenceUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IngredientVariantSubstanceContentOverrides",
                columns: table => new
                {
                    VariantRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    AmountUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    ReferenceUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceReference = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReviewState = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientVariantSubstanceContentOverrides", x => new { x.VariantRevisionId, x.SubstanceId });
                    table.CheckConstraint("CK_IngredientVariantSubstanceContentOverrides_Amount_NonNegative", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_IngredientVariantSubstanceContentOverrides_ReferenceQuantity_Positive", "\"ReferenceQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_IngredientVariantSubstanceContentOverrides_IngredientIntoleranceDefinitions_SubstanceId",
                        column: x => x.SubstanceId,
                        principalTable: "IngredientIntoleranceDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientVariantSubstanceContentOverrides_IngredientVariantRevisions_VariantRevisionId",
                        column: x => x.VariantRevisionId,
                        principalTable: "IngredientVariantRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientVariantSubstanceContentOverrides_MeasurementUnits_AmountUnitId",
                        column: x => x.AmountUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientVariantSubstanceContentOverrides_MeasurementUnits_ReferenceUnitId",
                        column: x => x.ReferenceUnitId,
                        principalTable: "MeasurementUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000001"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000002"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000003"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000006"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000007"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000008"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000009"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000010"),
                column: "IsQuantityDependent",
                value: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionSubstanceContents_AmountUnitId",
                table: "IngredientRevisionSubstanceContents",
                column: "AmountUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionSubstanceContents_ReferenceUnitId",
                table: "IngredientRevisionSubstanceContents",
                column: "ReferenceUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientRevisionSubstanceContents_SubstanceId",
                table: "IngredientRevisionSubstanceContents",
                column: "SubstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantSubstanceContentOverrides_AmountUnitId",
                table: "IngredientVariantSubstanceContentOverrides",
                column: "AmountUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantSubstanceContentOverrides_ReferenceUnitId",
                table: "IngredientVariantSubstanceContentOverrides",
                column: "ReferenceUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientVariantSubstanceContentOverrides_SubstanceId",
                table: "IngredientVariantSubstanceContentOverrides",
                column: "SubstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientRevisionSubstanceContents");

            migrationBuilder.DropTable(
                name: "IngredientVariantSubstanceContentOverrides");

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000001"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000002"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000003"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000006"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000007"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000008"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000009"),
                column: "IsQuantityDependent",
                value: false);

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000010"),
                column: "IsQuantityDependent",
                value: false);
        }
    }
}
