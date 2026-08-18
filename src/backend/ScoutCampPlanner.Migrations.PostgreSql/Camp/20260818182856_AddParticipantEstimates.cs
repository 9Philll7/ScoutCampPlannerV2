using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Camp
{
    /// <inheritdoc />
    public partial class AddParticipantEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParticipantEstimates",
                schema: "camp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    StructureNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildYouthCount = table.Column<int>(type: "integer", nullable: false),
                    LeaderCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantEstimates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantEstimates_CampStages_CampStageId",
                        column: x => x.CampStageId,
                        principalSchema: "camp",
                        principalTable: "CampStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParticipantEstimates_Camps_CampId",
                        column: x => x.CampId,
                        principalSchema: "camp",
                        principalTable: "Camps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantEstimates_StructureNodes_StructureNodeId",
                        column: x => x.StructureNodeId,
                        principalSchema: "camp",
                        principalTable: "StructureNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantEstimates_CampId",
                schema: "camp",
                table: "ParticipantEstimates",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantEstimates_CampStageId",
                schema: "camp",
                table: "ParticipantEstimates",
                column: "CampStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantEstimates_StructureNodeId_CampStageId",
                schema: "camp",
                table: "ParticipantEstimates",
                columns: new[] { "StructureNodeId", "CampStageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParticipantEstimates",
                schema: "camp");
        }
    }
}
