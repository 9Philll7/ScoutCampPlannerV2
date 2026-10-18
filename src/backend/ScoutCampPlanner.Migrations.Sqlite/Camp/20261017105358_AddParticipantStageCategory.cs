using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Camp
{
    /// <inheritdoc />
    public partial class AddParticipantStageCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampStageId",
                table: "CampParticipants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "CampParticipants",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampParticipants_CampId_CampStageId",
                table: "CampParticipants",
                columns: new[] { "CampId", "CampStageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CampParticipants_CampId_CampStageId",
                table: "CampParticipants");

            migrationBuilder.DropColumn(
                name: "CampStageId",
                table: "CampParticipants");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "CampParticipants");
        }
    }
}
