using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class EnforceUngroupedRecipePositionUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_RecipeRevisionId",
                schema: "catering",
                table: "RecipeSubrecipePositions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_SortOrder",
                schema: "catering",
                table: "RecipeSubrecipePositions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_SortOrder",
                schema: "catering",
                table: "RecipeIngredientPositions");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_RecipeRevisionId",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                columns: new[] { "RecipeId", "GroupId", "RecipeRevisionId" },
                unique: true,
                filter: "\"GroupId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_SortOrder",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                columns: new[] { "RecipeId", "GroupId", "SortOrder" },
                unique: true,
                filter: "\"GroupId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_Ungrouped_RecipeRevisionId",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                columns: new[] { "RecipeId", "RecipeRevisionId" },
                unique: true,
                filter: "\"GroupId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_Ungrouped_SortOrder",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                columns: new[] { "RecipeId", "SortOrder" },
                unique: true,
                filter: "\"GroupId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                columns: new[] { "RecipeId", "GroupId", "BaseIngredientId" },
                unique: true,
                filter: "\"GroupId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_SortOrder",
                schema: "catering",
                table: "RecipeIngredientPositions",
                columns: new[] { "RecipeId", "GroupId", "SortOrder" },
                unique: true,
                filter: "\"GroupId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_Ungrouped_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                columns: new[] { "RecipeId", "BaseIngredientId" },
                unique: true,
                filter: "\"GroupId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_Ungrouped_SortOrder",
                schema: "catering",
                table: "RecipeIngredientPositions",
                columns: new[] { "RecipeId", "SortOrder" },
                unique: true,
                filter: "\"GroupId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_RecipeRevisionId",
                schema: "catering",
                table: "RecipeSubrecipePositions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_SortOrder",
                schema: "catering",
                table: "RecipeSubrecipePositions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeSubrecipePositions_Ungrouped_RecipeRevisionId",
                schema: "catering",
                table: "RecipeSubrecipePositions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeSubrecipePositions_Ungrouped_SortOrder",
                schema: "catering",
                table: "RecipeSubrecipePositions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_SortOrder",
                schema: "catering",
                table: "RecipeIngredientPositions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeIngredientPositions_Ungrouped_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions");

            migrationBuilder.DropIndex(
                name: "IX_RecipeIngredientPositions_Ungrouped_SortOrder",
                schema: "catering",
                table: "RecipeIngredientPositions");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_RecipeRevisionId",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                columns: new[] { "RecipeId", "GroupId", "RecipeRevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSubrecipePositions_RecipeId_GroupId_SortOrder",
                schema: "catering",
                table: "RecipeSubrecipePositions",
                columns: new[] { "RecipeId", "GroupId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                columns: new[] { "RecipeId", "GroupId", "BaseIngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_SortOrder",
                schema: "catering",
                table: "RecipeIngredientPositions",
                columns: new[] { "RecipeId", "GroupId", "SortOrder" },
                unique: true);
        }
    }
}
