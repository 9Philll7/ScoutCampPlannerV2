using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Camp
{
    /// <inheritdoc />
    public partial class AddParticipantRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DietTypeId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampParticipants_Camps_CampId",
                        column: x => x.CampId,
                        principalTable: "Camps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampParticipantAbsentDays",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampParticipantAbsentDays", x => new { x.ParticipantId, x.Date });
                    table.ForeignKey(
                        name: "FK_CampParticipantAbsentDays_CampParticipants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CampParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampParticipantAbsentMeals",
                columns: table => new
                {
                    MealId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampParticipantAbsentMeals", x => new { x.ParticipantId, x.MealId });
                    table.ForeignKey(
                        name: "FK_CampParticipantAbsentMeals_CampParticipants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CampParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampParticipantAllergens",
                columns: table => new
                {
                    AllergenId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampParticipantAllergens", x => new { x.ParticipantId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_CampParticipantAllergens_CampParticipants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CampParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampParticipantIntolerances",
                columns: table => new
                {
                    SubstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThresholdGramsPerPortion = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: true),
                    ThresholdSource = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampParticipantIntolerances", x => new { x.ParticipantId, x.SubstanceId });
                    table.CheckConstraint("CK_CampParticipantIntolerances_Threshold", "\"ThresholdGramsPerPortion\" IS NULL OR \"ThresholdGramsPerPortion\" >= 0");
                    table.ForeignKey(
                        name: "FK_CampParticipantIntolerances_CampParticipants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "CampParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampParticipants_CampId",
                table: "CampParticipants",
                column: "CampId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampParticipantAbsentDays");

            migrationBuilder.DropTable(
                name: "CampParticipantAbsentMeals");

            migrationBuilder.DropTable(
                name: "CampParticipantAllergens");

            migrationBuilder.DropTable(
                name: "CampParticipantIntolerances");

            migrationBuilder.DropTable(
                name: "CampParticipants");
        }
    }
}
