using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class OperationalParticipantPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DemandBasis",
                table: "CookingUnitMealStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MealPlanningParticipantConfigurations",
                columns: table => new
                {
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    DemandMode = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignmentsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanningParticipantConfigurations", x => x.CampId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealPlanningParticipantConfigurations");

            migrationBuilder.DropColumn(
                name: "DemandBasis",
                table: "CookingUnitMealStates");
        }
    }
}
