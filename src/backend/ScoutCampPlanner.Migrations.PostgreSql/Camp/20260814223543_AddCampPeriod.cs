using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Camp
{
    /// <inheritdoc />
    public partial class AddCampPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                schema: "camp",
                table: "Camps",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                schema: "camp",
                table: "Camps",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                schema: "camp",
                table: "Camps",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Camps_TenantId_NormalizedName_StartDate_EndDate",
                schema: "camp",
                table: "Camps",
                columns: new[] { "TenantId", "NormalizedName", "StartDate", "EndDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Camps_TenantId_NormalizedName_StartDate_EndDate",
                schema: "camp",
                table: "Camps");

            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "camp",
                table: "Camps");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                schema: "camp",
                table: "Camps");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "camp",
                table: "Camps");
        }
    }
}
