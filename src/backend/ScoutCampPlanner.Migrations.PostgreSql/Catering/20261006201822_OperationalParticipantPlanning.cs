using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class OperationalParticipantPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DemandBasis",
                schema: "catering",
                table: "CookingUnitMealStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MealPlanningParticipantConfigurations",
                schema: "catering",
                columns: table => new
                {
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DemandMode = table.Column<int>(type: "integer", nullable: false),
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
                name: "MealPlanningParticipantConfigurations",
                schema: "catering");

            migrationBuilder.DropColumn(
                name: "DemandBasis",
                schema: "catering",
                table: "CookingUnitMealStates");
        }
    }
}
