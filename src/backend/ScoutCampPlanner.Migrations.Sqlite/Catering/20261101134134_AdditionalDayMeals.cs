using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AdditionalDayMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "CampMealTypes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "CampMeals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CampMealScheduleStates",
                columns: table => new
                {
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampMealScheduleStates", x => x.CampId);
                });
            migrationBuilder.Sql("""
WITH ranked AS (
    SELECT m."Id", ROW_NUMBER() OVER (PARTITION BY m."CampId", m."Date" ORDER BY t."SortOrder", m."Id") - 1 AS position
    FROM "CampMeals" m JOIN "CampMealTypes" t ON m."MealTypeId" = t."Id"
)
UPDATE "CampMeals" SET "SortOrder" = (SELECT CAST(position AS INTEGER) FROM ranked WHERE ranked."Id" = "CampMeals"."Id");
INSERT INTO "CampMealScheduleStates" ("CampId", "Version") SELECT DISTINCT "CampId", 0 FROM "CampMealTypes";
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampMealScheduleStates");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "CampMealTypes");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "CampMeals");
        }
    }
}
