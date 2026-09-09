using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddIngredientCentralContributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReplacedAtUtc",
                table: "IngredientIdentities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacedBy",
                table: "IngredientIdentities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacedByCentralIngredientId",
                table: "IngredientIdentities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IngredientCentralContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmittedLocalRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SuggestedCentralIngredientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResultingCentralIngredientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResultingCentralRevisionId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientCentralContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientCentralContributions_IngredientIdentities_ResultingCentralIngredientId",
                        column: x => x.ResultingCentralIngredientId,
                        principalTable: "IngredientIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientCentralContributions_IngredientIdentities_SuggestedCentralIngredientId",
                        column: x => x.SuggestedCentralIngredientId,
                        principalTable: "IngredientIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientCentralContributions_IngredientRevisions_ResultingCentralRevisionId",
                        column: x => x.ResultingCentralRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientCentralContributions_IngredientRevisions_SubmittedLocalRevisionId",
                        column: x => x.SubmittedLocalRevisionId,
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIdentities_ReplacedByCentralIngredientId",
                table: "IngredientIdentities",
                column: "ReplacedByCentralIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCentralContributions_ResultingCentralIngredientId",
                table: "IngredientCentralContributions",
                column: "ResultingCentralIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCentralContributions_ResultingCentralRevisionId",
                table: "IngredientCentralContributions",
                column: "ResultingCentralRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCentralContributions_Status",
                table: "IngredientCentralContributions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCentralContributions_SubmittedLocalRevisionId",
                table: "IngredientCentralContributions",
                column: "SubmittedLocalRevisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCentralContributions_SuggestedCentralIngredientId",
                table: "IngredientCentralContributions",
                column: "SuggestedCentralIngredientId");

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientIdentities_IngredientIdentities_ReplacedByCentralIngredientId",
                table: "IngredientIdentities",
                column: "ReplacedByCentralIngredientId",
                principalTable: "IngredientIdentities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IngredientIdentities_IngredientIdentities_ReplacedByCentralIngredientId",
                table: "IngredientIdentities");

            migrationBuilder.DropTable(
                name: "IngredientCentralContributions");

            migrationBuilder.DropIndex(
                name: "IX_IngredientIdentities_ReplacedByCentralIngredientId",
                table: "IngredientIdentities");

            migrationBuilder.DropColumn(
                name: "ReplacedAtUtc",
                table: "IngredientIdentities");

            migrationBuilder.DropColumn(
                name: "ReplacedBy",
                table: "IngredientIdentities");

            migrationBuilder.DropColumn(
                name: "ReplacedByCentralIngredientId",
                table: "IngredientIdentities");
        }
    }
}
