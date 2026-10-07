using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class ExtendDietaryCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DietaryRequirements_NormalizedName",
                table: "DietaryRequirements");

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultThresholdGramsPerPortion",
                table: "IngredientIntoleranceDefinitions",
                type: "TEXT",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultThresholdSource",
                table: "IngredientIntoleranceDefinitions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultThresholdVersion",
                table: "IngredientIntoleranceDefinitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "DietaryRequirements",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "DietaryRequirements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DietaryRequirements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "DietaryRequirements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DietaryOriginRules",
                columns: table => new
                {
                    OriginId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DietaryRequirementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Decision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietaryOriginRules", x => new { x.DietaryRequirementId, x.OriginId });
                    table.ForeignKey(
                        name: "FK_DietaryOriginRules_DietaryRequirements_DietaryRequirementId",
                        column: x => x.DietaryRequirementId,
                        principalTable: "DietaryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DietaryOriginRules_IngredientOriginProperties_OriginId",
                        column: x => x.OriginId,
                        principalTable: "IngredientOriginProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DietaryRequirementRevisions",
                columns: table => new
                {
                    DietaryRequirementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietaryRequirementRevisions", x => new { x.DietaryRequirementId, x.Version });
                    table.ForeignKey(
                        name: "FK_DietaryRequirementRevisions_DietaryRequirements_DietaryRequirementId",
                        column: x => x.DietaryRequirementId,
                        principalTable: "DietaryRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DietaryRequirementContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DietaryRequirementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CentralDietaryRequirementId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DietaryRequirementContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DietaryRequirementContributions_DietaryRequirementRevisions_DietaryRequirementId_Version",
                        columns: x => new { x.DietaryRequirementId, x.Version },
                        principalTable: "DietaryRequirementRevisions",
                        principalColumns: new[] { "DietaryRequirementId", "Version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000001"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000002"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000003"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000004"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000005"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000006"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000007"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000008"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000009"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.UpdateData(
                table: "IngredientIntoleranceDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("31111111-1111-1111-1111-000000000010"),
                columns: new[] { "DefaultThresholdGramsPerPortion", "DefaultThresholdSource", "DefaultThresholdVersion" },
                values: new object[] { null, null, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_DietaryRequirements_NormalizedName",
                table: "DietaryRequirements",
                column: "NormalizedName",
                unique: true,
                filter: "\"TenantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DietaryRequirements_TenantId_NormalizedName",
                table: "DietaryRequirements",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true,
                filter: "\"TenantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DietaryOriginRules_OriginId",
                table: "DietaryOriginRules",
                column: "OriginId");

            migrationBuilder.CreateIndex(
                name: "IX_DietaryRequirementContributions_DietaryRequirementId_Version",
                table: "DietaryRequirementContributions",
                columns: new[] { "DietaryRequirementId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DietaryOriginRules");

            migrationBuilder.DropTable(
                name: "DietaryRequirementContributions");

            migrationBuilder.DropTable(
                name: "DietaryRequirementRevisions");

            migrationBuilder.DropIndex(
                name: "IX_DietaryRequirements_NormalizedName",
                table: "DietaryRequirements");

            migrationBuilder.DropIndex(
                name: "IX_DietaryRequirements_TenantId_NormalizedName",
                table: "DietaryRequirements");

            migrationBuilder.DropColumn(
                name: "DefaultThresholdGramsPerPortion",
                table: "IngredientIntoleranceDefinitions");

            migrationBuilder.DropColumn(
                name: "DefaultThresholdSource",
                table: "IngredientIntoleranceDefinitions");

            migrationBuilder.DropColumn(
                name: "DefaultThresholdVersion",
                table: "IngredientIntoleranceDefinitions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "DietaryRequirements");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "DietaryRequirements");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DietaryRequirements");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "DietaryRequirements");

            migrationBuilder.CreateIndex(
                name: "IX_DietaryRequirements_NormalizedName",
                table: "DietaryRequirements",
                column: "NormalizedName",
                unique: true);
        }
    }
}
