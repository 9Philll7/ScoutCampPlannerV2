using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Camp
{
    /// <inheritdoc />
    public partial class AddCampPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "Camps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Camps",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Camps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Camps_TenantId_NormalizedName_StartDate_EndDate",
                table: "Camps",
                columns: new[] { "TenantId", "NormalizedName", "StartDate", "EndDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Camps_TenantId_NormalizedName_StartDate_EndDate",
                table: "Camps");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Camps");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Camps");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Camps");
        }
    }
}
