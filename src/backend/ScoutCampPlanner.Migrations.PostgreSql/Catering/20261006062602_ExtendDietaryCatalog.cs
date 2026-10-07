using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class ExtendDietaryCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DietaryRequirements_NormalizedName",
                schema: "catering",
                table: "DietaryRequirements");

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultThresholdGramsPerPortion",
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultThresholdSource",
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultThresholdVersion",
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "catering",
                table: "DietaryRequirements",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "catering",
                table: "DietaryRequirements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "catering",
                table: "DietaryRequirements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "catering",
                table: "DietaryRequirements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DietaryOriginRules",
                schema: "catering",
                columns: table => new
                {
                    OriginId = table.Column<Guid>(type: "uuid", nullable: false),
                    DietaryRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietaryOriginRules", x => new { x.DietaryRequirementId, x.OriginId });
                    table.ForeignKey(
                        name: "FK_DietaryOriginRules_DietaryRequirements_DietaryRequirementId",
                        column: x => x.DietaryRequirementId,
                        principalSchema: "catering",
                        principalTable: "DietaryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DietaryOriginRules_IngredientOriginProperties_OriginId",
                        column: x => x.OriginId,
                        principalSchema: "catering",
                        principalTable: "IngredientOriginProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DietaryRequirementRevisions",
                schema: "catering",
                columns: table => new
                {
                    DietaryRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietaryRequirementRevisions", x => new { x.DietaryRequirementId, x.Version });
                    table.ForeignKey(
                        name: "FK_DietaryRequirementRevisions_DietaryRequirements_DietaryRequ~",
                        column: x => x.DietaryRequirementId,
                        principalSchema: "catering",
                        principalTable: "DietaryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DietaryRequirementContributions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DietaryRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CentralDietaryRequirementId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietaryRequirementContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietaryRequirementContributions_DietaryRequirementRevisions~",
                        columns: x => new { x.DietaryRequirementId, x.Version },
                        principalSchema: "catering",
                        principalTable: "DietaryRequirementRevisions",
                        principalColumns: new[] { "DietaryRequirementId", "Version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000001"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000002"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000003"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000004"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000005"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000006"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000007"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000008"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000009"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                schema: "catering",
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000010"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_DietaryRequirements_NormalizedName",
                schema: "catering",
                table: "DietaryRequirements",
                column: "NormalizedName",
                unique: true,
                filter: "\"TenantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DietaryRequirements_TenantId_NormalizedName",
                schema: "catering",
                table: "DietaryRequirements",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true,
                filter: "\"TenantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DietaryOriginRules_OriginId",
                schema: "catering",
                table: "DietaryOriginRules",
                column: "OriginId");

            migrationBuilder.CreateIndex(
                name: "IX_DietaryRequirementContributions_DietaryRequirementId_Version",
                schema: "catering",
                table: "DietaryRequirementContributions",
                columns: new[] { "DietaryRequirementId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DietaryOriginRules",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "DietaryRequirementContributions",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "DietaryRequirementRevisions",
                schema: "catering");

            migrationBuilder.DropIndex(
                name: "IX_DietaryRequirements_NormalizedName",
                schema: "catering",
                table: "DietaryRequirements");

            migrationBuilder.DropIndex(
                name: "IX_DietaryRequirements_TenantId_NormalizedName",
                schema: "catering",
                table: "DietaryRequirements");

            migrationBuilder.DropColumn(
                name: "DefaultThresholdGramsPerPortion",
                schema: "catering",
                table: "IngredientIntoleranceDefinitions");

            migrationBuilder.DropColumn(
                name: "DefaultThresholdSource",
                schema: "catering",
                table: "IngredientIntoleranceDefinitions");

            migrationBuilder.DropColumn(
                name: "DefaultThresholdVersion",
                schema: "catering",
                table: "IngredientIntoleranceDefinitions");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "catering",
                table: "DietaryRequirements");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "catering",
                table: "DietaryRequirements");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "catering",
                table: "DietaryRequirements");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "catering",
                table: "DietaryRequirements");

            migrationBuilder.CreateIndex(
                name: "IX_DietaryRequirements_NormalizedName",
                schema: "catering",
                table: "DietaryRequirements",
                column: "NormalizedName",
                unique: true);
        }
    }
}
