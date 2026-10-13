using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class OperationalMealSupply : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TogetherPreference",
                table: "CookingUnits",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ManualSupplySolutionsJson",
                table: "CookingUnitMealStates",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<long>(
                name: "SupplyVersion",
                table: "CookingUnitMealStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "VerificationFingerprint",
                table: "CookingUnitMealStates",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationSnapshotJson",
                table: "CookingUnitMealStates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TogetherPreference",
                table: "CookingUnits");

            migrationBuilder.DropColumn(
                name: "ManualSupplySolutionsJson",
                table: "CookingUnitMealStates");

            migrationBuilder.DropColumn(
                name: "SupplyVersion",
                table: "CookingUnitMealStates");

            migrationBuilder.DropColumn(
                name: "VerificationFingerprint",
                table: "CookingUnitMealStates");

            migrationBuilder.DropColumn(
                name: "VerificationSnapshotJson",
                table: "CookingUnitMealStates");
        }
    }
}
