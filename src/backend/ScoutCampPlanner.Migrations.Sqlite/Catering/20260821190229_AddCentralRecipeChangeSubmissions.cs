using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddCentralRecipeChangeSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CentralRecipeChangeSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CentralRecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceCentralRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmittedLocalRecipeRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ResultingCentralRevisionId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CentralRecipeChangeSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CentralRecipeChangeSubmissions_RecipeRevisions_ResultingCentralRevisionId",
                        column: x => x.ResultingCentralRevisionId,
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CentralRecipeChangeSubmissions_RecipeRevisions_SourceCentralRevisionId",
                        column: x => x.SourceCentralRevisionId,
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CentralRecipeChangeSubmissions_RecipeRevisions_SubmittedLocalRecipeRevisionId",
                        column: x => x.SubmittedLocalRecipeRevisionId,
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CentralRecipeChangeSubmissions_Recipes_CentralRecipeId",
                        column: x => x.CentralRecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CentralRecipeChangeSubmissions_CentralRecipeId",
                table: "CentralRecipeChangeSubmissions",
                column: "CentralRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_CentralRecipeChangeSubmissions_ResultingCentralRevisionId",
                table: "CentralRecipeChangeSubmissions",
                column: "ResultingCentralRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CentralRecipeChangeSubmissions_SourceCentralRevisionId",
                table: "CentralRecipeChangeSubmissions",
                column: "SourceCentralRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CentralRecipeChangeSubmissions_Status_SubmittedAtUtc",
                table: "CentralRecipeChangeSubmissions",
                columns: new[] { "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CentralRecipeChangeSubmissions_SubmittedLocalRecipeRevisionId",
                table: "CentralRecipeChangeSubmissions",
                column: "SubmittedLocalRecipeRevisionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CentralRecipeChangeSubmissions");
        }
    }
}
