using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddCentralRecipeChangeSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CentralRecipeChangeSubmissions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CentralRecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCentralRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedLocalRecipeRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultingCentralRevisionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CentralRecipeChangeSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentralRecipeChangeSubmissions_RecipeRevisions_ResultingCen~",
                        column: x => x.ResultingCentralRevisionId,
                        principalSchema: "catering",
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CentralRecipeChangeSubmissions_RecipeRevisions_SourceCentra~",
                        column: x => x.SourceCentralRevisionId,
                        principalSchema: "catering",
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CentralRecipeChangeSubmissions_RecipeRevisions_SubmittedLoc~",
                        column: x => x.SubmittedLocalRecipeRevisionId,
                        principalSchema: "catering",
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CentralRecipeChangeSubmissions_Recipes_CentralRecipeId",
                        column: x => x.CentralRecipeId,
                        principalSchema: "catering",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CentralRecipeChangeSubmissions_CentralRecipeId",
                schema: "catering",
                table: "CentralRecipeChangeSubmissions",
                column: "CentralRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_CentralRecipeChangeSubmissions_ResultingCentralRevisionId",
                schema: "catering",
                table: "CentralRecipeChangeSubmissions",
                column: "ResultingCentralRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CentralRecipeChangeSubmissions_SourceCentralRevisionId",
                schema: "catering",
                table: "CentralRecipeChangeSubmissions",
                column: "SourceCentralRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CentralRecipeChangeSubmissions_Status_SubmittedAtUtc",
                schema: "catering",
                table: "CentralRecipeChangeSubmissions",
                columns: new[] { "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CentralRecipeChangeSubmissions_SubmittedLocalRecipeRevision~",
                schema: "catering",
                table: "CentralRecipeChangeSubmissions",
                column: "SubmittedLocalRecipeRevisionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CentralRecipeChangeSubmissions",
                schema: "catering");
        }
    }
}
