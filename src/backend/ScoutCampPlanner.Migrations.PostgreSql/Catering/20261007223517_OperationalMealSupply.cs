using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class OperationalMealSupply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TogetherPreference",
                schema: "catering",
                table: "CookingUnits",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ManualSupplySolutionsJson",
                schema: "catering",
                table: "CookingUnitMealStates",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<long>(
                name: "SupplyVersion",
                schema: "catering",
                table: "CookingUnitMealStates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "VerificationFingerprint",
                schema: "catering",
                table: "CookingUnitMealStates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationSnapshotJson",
                schema: "catering",
                table: "CookingUnitMealStates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TogetherPreference",
                schema: "catering",
                table: "CookingUnits");

            migrationBuilder.DropColumn(
                name: "ManualSupplySolutionsJson",
                schema: "catering",
                table: "CookingUnitMealStates");

            migrationBuilder.DropColumn(
                name: "SupplyVersion",
                schema: "catering",
                table: "CookingUnitMealStates");

            migrationBuilder.DropColumn(
                name: "VerificationFingerprint",
                schema: "catering",
                table: "CookingUnitMealStates");

            migrationBuilder.DropColumn(
                name: "VerificationSnapshotJson",
                schema: "catering",
                table: "CookingUnitMealStates");
        }
    }
}
