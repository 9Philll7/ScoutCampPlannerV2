using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddLinearRecipeQuantityLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaximumQuantity",
                table: "RecipeIngredientPositions",
                type: "TEXT",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumQuantity",
                table: "RecipeIngredientPositions",
                type: "TEXT",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximumQuantity",
                table: "RecipeIngredientPositions");

            migrationBuilder.DropColumn(
                name: "MinimumQuantity",
                table: "RecipeIngredientPositions");
        }
    }
}
