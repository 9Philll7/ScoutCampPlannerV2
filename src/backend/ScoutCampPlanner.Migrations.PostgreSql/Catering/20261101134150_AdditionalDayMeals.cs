using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AdditionalDayMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "catering",
                table: "CampMealTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "catering",
                table: "CampMeals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CampMealScheduleStates",
                schema: "catering",
                columns: table => new
                {
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampMealScheduleStates", x => x.CampId);
                });
            migrationBuilder.Sql("""
WITH ranked AS (
    SELECT m."Id", ROW_NUMBER() OVER (PARTITION BY m."CampId", m."Date" ORDER BY t."SortOrder", m."Id") - 1 AS position
    FROM catering."CampMeals" m JOIN catering."CampMealTypes" t ON m."MealTypeId" = t."Id"
)
UPDATE catering."CampMeals" SET "SortOrder" = (SELECT CAST(position AS INTEGER) FROM ranked WHERE ranked."Id" = catering."CampMeals"."Id");
INSERT INTO catering."CampMealScheduleStates" ("CampId", "Version") SELECT DISTINCT "CampId", 0 FROM catering."CampMealTypes";
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampMealScheduleStates",
                schema: "catering");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "catering",
                table: "CampMealTypes");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "catering",
                table: "CampMeals");
        }
    }
}
