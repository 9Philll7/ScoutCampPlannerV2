using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Camp
{
    /// <inheritdoc />
    public partial class AddParticipantStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StructureNodeId",
                schema: "camp",
                table: "CampParticipants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampParticipants_StructureNodeId",
                schema: "camp",
                table: "CampParticipants",
                column: "StructureNodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampParticipants_StructureNodes_StructureNodeId",
                schema: "camp",
                table: "CampParticipants",
                column: "StructureNodeId",
                principalSchema: "camp",
                principalTable: "StructureNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampParticipants_StructureNodes_StructureNodeId",
                schema: "camp",
                table: "CampParticipants");

            migrationBuilder.DropIndex(
                name: "IX_CampParticipants_StructureNodeId",
                schema: "camp",
                table: "CampParticipants");

            migrationBuilder.DropColumn(
                name: "StructureNodeId",
                schema: "camp",
                table: "CampParticipants");
        }
    }
}
