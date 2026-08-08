using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ScoutCampPlanner.Catering.Infrastructure;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering;

[DbContext(typeof(CateringDbContext))]
[Migration("20260808210001_AddCateringIndexes")]
public sealed class AddCateringIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateIndex(
        name: "IX_MealPlans_CampId",
        schema: "catering",
        table: "MealPlans",
        column: "CampId");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropIndex(
        name: "IX_MealPlans_CampId",
        schema: "catering",
        table: "MealPlans");
}
