using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ScoutCampPlanner.Camp.Infrastructure;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Camp;

[DbContext(typeof(CampDbContext))]
[Migration("20260808210000_AddCampIndexes")]
public sealed class AddCampIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Camps_TenantId_Name",
            table: "Camps",
            columns: new[] { "TenantId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_CookingUnits_CampId",
            table: "CookingUnits",
            column: "CampId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Camps_TenantId_Name", table: "Camps");
        migrationBuilder.DropIndex(name: "IX_CookingUnits_CampId", table: "CookingUnits");
    }
}
