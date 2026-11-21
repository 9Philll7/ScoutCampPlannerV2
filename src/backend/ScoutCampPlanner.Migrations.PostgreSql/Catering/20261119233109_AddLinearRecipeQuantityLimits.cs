using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddLinearRecipeQuantityLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaximumQuantity",
                schema: "catering",
                table: "RecipeIngredientPositions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumQuantity",
                schema: "catering",
                table: "RecipeIngredientPositions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaximumQuantity",
                schema: "catering",
                table: "RecipeIngredientPositions");

            migrationBuilder.DropColumn(
                name: "MinimumQuantity",
                schema: "catering",
                table: "RecipeIngredientPositions");
        }
    }
}
