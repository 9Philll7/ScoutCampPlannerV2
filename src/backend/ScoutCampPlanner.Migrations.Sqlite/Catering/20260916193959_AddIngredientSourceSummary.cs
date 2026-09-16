using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddIngredientSourceSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceSummary",
                table: "IngredientRevisions",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "IngredientRevisions"
                SET "SourceSummary" = COALESCE(
                    (SELECT "SourceReference" FROM "IngredientRevisionNutritionProfiles"
                     WHERE "IngredientRevisionId" = "IngredientRevisions"."Id"),
                    (SELECT "SourceReference" FROM "IngredientRevisionSubstanceContents"
                     WHERE "IngredientRevisionId" = "IngredientRevisions"."Id" LIMIT 1),
                    '')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceSummary",
                table: "IngredientRevisions");
        }
    }
}
