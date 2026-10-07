using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Camp
{
    /// <inheritdoc />
    public partial class AddParticipantStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StructureNodeId",
                table: "CampParticipants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampParticipants_StructureNodeId",
                table: "CampParticipants",
                column: "StructureNodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampParticipants_StructureNodes_StructureNodeId",
                table: "CampParticipants",
                column: "StructureNodeId",
                principalTable: "StructureNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampParticipants_StructureNodes_StructureNodeId",
                table: "CampParticipants");

            migrationBuilder.DropIndex(
                name: "IX_CampParticipants_StructureNodeId",
                table: "CampParticipants");

            migrationBuilder.DropColumn(
                name: "StructureNodeId",
                table: "CampParticipants");
        }
    }
}
