using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Camp
{
    /// <inheritdoc />
    public partial class AddParticipantStageCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampStageId",
                schema: "camp",
                table: "CampParticipants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "camp",
                table: "CampParticipants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampParticipants_CampId_CampStageId",
                schema: "camp",
                table: "CampParticipants",
                columns: new[] { "CampId", "CampStageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CampParticipants_CampId_CampStageId",
                schema: "camp",
                table: "CampParticipants");

            migrationBuilder.DropColumn(
                name: "CampStageId",
                schema: "camp",
                table: "CampParticipants");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "camp",
                table: "CampParticipants");
        }
    }
}
