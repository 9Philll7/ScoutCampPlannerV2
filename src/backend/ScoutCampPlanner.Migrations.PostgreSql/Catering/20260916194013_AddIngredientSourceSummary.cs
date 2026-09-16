using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddIngredientSourceSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceSummary",
                schema: "catering",
                table: "IngredientRevisions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE catering."IngredientRevisions" AS revision
                SET "SourceSummary" = COALESCE(
                    (SELECT "SourceReference" FROM catering."IngredientRevisionNutritionProfiles"
                     WHERE "IngredientRevisionId" = revision."Id"),
                    (SELECT "SourceReference" FROM catering."IngredientRevisionSubstanceContents"
                     WHERE "IngredientRevisionId" = revision."Id" LIMIT 1),
                    '')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceSummary",
                schema: "catering",
                table: "IngredientRevisions");
        }
    }
}
