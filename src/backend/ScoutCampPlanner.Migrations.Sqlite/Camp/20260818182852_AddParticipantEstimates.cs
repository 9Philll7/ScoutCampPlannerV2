using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Camp
{
    /// <inheritdoc />
    public partial class AddParticipantEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParticipantEstimates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StructureNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampStageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChildYouthCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaderCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantEstimates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantEstimates_CampStages_CampStageId",
                        column: x => x.CampStageId,
                        principalTable: "CampStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParticipantEstimates_Camps_CampId",
                        column: x => x.CampId,
                        principalTable: "Camps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantEstimates_StructureNodes_StructureNodeId",
                        column: x => x.StructureNodeId,
                        principalTable: "StructureNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantEstimates_CampId",
                table: "ParticipantEstimates",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantEstimates_CampStageId",
                table: "ParticipantEstimates",
                column: "CampStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantEstimates_StructureNodeId_CampStageId",
                table: "ParticipantEstimates",
                columns: new[] { "StructureNodeId", "CampStageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParticipantEstimates");
        }
    }
}
