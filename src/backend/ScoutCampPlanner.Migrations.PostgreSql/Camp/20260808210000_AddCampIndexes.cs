using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ScoutCampPlanner.Camp.Infrastructure;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Camp;

[DbContext(typeof(CampDbContext))]
[Migration("20260808210000_AddCampIndexes")]
public sealed class AddCampIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Camps_TenantId_Name",
            schema: "camp",
            table: "Camps",
            columns: new[] { "TenantId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_CookingUnits_CampId",
            schema: "camp",
            table: "CookingUnits",
            column: "CampId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Camps_TenantId_Name", schema: "camp", table: "Camps");
        migrationBuilder.DropIndex(name: "IX_CookingUnits_CampId", schema: "camp", table: "CookingUnits");
    }
}
