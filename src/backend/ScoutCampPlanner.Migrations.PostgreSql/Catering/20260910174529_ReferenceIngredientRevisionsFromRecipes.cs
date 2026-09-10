using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class ReferenceIngredientRevisionsFromRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredientPositions_BaseIngredients_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredientReplacements_BaseIngredients_ReplacementBas~",
                schema: "catering",
                table: "RecipeIngredientReplacements");

            migrationBuilder.RenameColumn(
                name: "ReplacementBaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientReplacements",
                newName: "ReplacementIngredientRevisionId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientReplacements_ReplacementBaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientReplacements",
                newName: "IX_RecipeIngredientReplacements_ReplacementIngredientRevisionId");

            migrationBuilder.RenameColumn(
                name: "BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                newName: "IngredientRevisionId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_Ungrouped_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_Ungrouped_IngredientRevisionId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_RecipeId_GroupId_IngredientRevisi~");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_IngredientRevisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredientPositions_IngredientRevisions_IngredientRev~",
                schema: "catering",
                table: "RecipeIngredientPositions",
                column: "IngredientRevisionId",
                principalSchema: "catering",
                principalTable: "IngredientRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredientReplacements_IngredientRevisions_Replacemen~",
                schema: "catering",
                table: "RecipeIngredientReplacements",
                column: "ReplacementIngredientRevisionId",
                principalSchema: "catering",
                principalTable: "IngredientRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredientPositions_IngredientRevisions_IngredientRev~",
                schema: "catering",
                table: "RecipeIngredientPositions");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredientReplacements_IngredientRevisions_Replacemen~",
                schema: "catering",
                table: "RecipeIngredientReplacements");

            migrationBuilder.RenameColumn(
                name: "ReplacementIngredientRevisionId",
                schema: "catering",
                table: "RecipeIngredientReplacements",
                newName: "ReplacementBaseIngredientId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientReplacements_ReplacementIngredientRevisionId",
                schema: "catering",
                table: "RecipeIngredientReplacements",
                newName: "IX_RecipeIngredientReplacements_ReplacementBaseIngredientId");

            migrationBuilder.RenameColumn(
                name: "IngredientRevisionId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                newName: "BaseIngredientId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_Ungrouped_IngredientRevisionId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_Ungrouped_BaseIngredientId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_RecipeId_GroupId_IngredientRevisi~",
                schema: "catering",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_RecipeId_GroupId_BaseIngredientId");

            migrationBuilder.RenameIndex(
                name: "IX_RecipeIngredientPositions_IngredientRevisionId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                newName: "IX_RecipeIngredientPositions_BaseIngredientId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredientPositions_BaseIngredients_BaseIngredientId",
                schema: "catering",
                table: "RecipeIngredientPositions",
                column: "BaseIngredientId",
                principalSchema: "catering",
                principalTable: "BaseIngredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredientReplacements_BaseIngredients_ReplacementBas~",
                schema: "catering",
                table: "RecipeIngredientReplacements",
                column: "ReplacementBaseIngredientId",
                principalSchema: "catering",
                principalTable: "BaseIngredients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
