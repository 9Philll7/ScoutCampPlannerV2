using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class ReferenceIngredientRevisionsFromRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredientPositions_BaseIngredients_BaseIngredientId",
                table: "RecipeIngredientPositions");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredientReplacements_BaseIngredients_ReplacementBaseIngredientId",
                table: "RecipeIngredientReplacements");

            migrationBuilder.RenameColumn(
                name: "ReplacementBaseIngredientId",
                table: "RecipeIngredientReplacements",
                newName: "ReplacementIngredientRevisionId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientReplacements_ReplacementBaseIngredientId",
                table: "RecipeIngredientReplacements",
                newName: "IX_RecipeIngredientReplacements_ReplacementIngredientRevisionId");

            migrationBuilder.RenameColumn(
                name: "BaseIngredientId",
                table: "RecipeIngredientPositions",
                newName: "IngredientRevisionId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_Ungrouped_BaseIngredientId",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_Ungrouped_IngredientRevisionId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_BaseIngredientId",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_RecipeId_GroupId_IngredientRevisionId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_BaseIngredientId",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_IngredientRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredientPositions_IngredientRevisions_IngredientRevisionId",
                table: "RecipeIngredientPositions",
                column: "IngredientRevisionId",
                principalTable: "IngredientRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredientReplacements_IngredientRevisions_ReplacementIngredientRevisionId",
                table: "RecipeIngredientReplacements",
                column: "ReplacementIngredientRevisionId",
                principalTable: "IngredientRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredientPositions_IngredientRevisions_IngredientRevisionId",
                table: "RecipeIngredientPositions");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredientReplacements_IngredientRevisions_ReplacementIngredientRevisionId",
                table: "RecipeIngredientReplacements");

            migrationBuilder.RenameColumn(
                name: "ReplacementIngredientRevisionId",
                table: "RecipeIngredientReplacements",
                newName: "ReplacementBaseIngredientId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientReplacements_ReplacementIngredientRevisionId",
                table: "RecipeIngredientReplacements",
                newName: "IX_RecipeIngredientReplacements_ReplacementBaseIngredientId");

            migrationBuilder.RenameColumn(
                name: "IngredientRevisionId",
                table: "RecipeIngredientPositions",
                newName: "BaseIngredientId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_Ungrouped_IngredientRevisionId",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_Ungrouped_BaseIngredientId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_IngredientRevisionId",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_RecipeId_GroupId_BaseIngredientId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_IngredientRevisionId",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_BaseIngredientId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredientPositions_BaseIngredients_BaseIngredientId",
                table: "RecipeIngredientPositions",
                column: "BaseIngredientId",
                principalTable: "BaseIngredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredientReplacements_BaseIngredients_ReplacementBaseIngredientId",
                table: "RecipeIngredientReplacements",
                column: "ReplacementBaseIngredientId",
                principalTable: "BaseIngredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
