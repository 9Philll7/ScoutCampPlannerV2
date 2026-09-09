using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddIngredientCentralContributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReplacedAtUtc",
                schema: "catering",
                table: "IngredientIdentities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacedBy",
                schema: "catering",
                table: "IngredientIdentities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacedByCentralIngredientId",
                schema: "catering",
                table: "IngredientIdentities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IngredientCentralContributions",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedLocalRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestedCentralIngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultingCentralIngredientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultingCentralRevisionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientCentralContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientCentralContributions_IngredientIdentities_Resulti~",
                        column: x => x.ResultingCentralIngredientId,
                        principalSchema: "catering",
                        principalTable: "IngredientIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientCentralContributions_IngredientIdentities_Suggest~",
                        column: x => x.SuggestedCentralIngredientId,
                        principalSchema: "catering",
                        principalTable: "IngredientIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientCentralContributions_IngredientRevisions_Resultin~",
                        column: x => x.ResultingCentralRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientCentralContributions_IngredientRevisions_Submitte~",
                        column: x => x.SubmittedLocalRevisionId,
                        principalSchema: "catering",
                        principalTable: "IngredientRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientIdentities_ReplacedByCentralIngredientId",
                schema: "catering",
                table: "IngredientIdentities",
                column: "ReplacedByCentralIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCentralContributions_ResultingCentralIngredientId",
                schema: "catering",
                table: "IngredientCentralContributions",
                column: "ResultingCentralIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCentralContributions_ResultingCentralRevisionId",
                schema: "catering",
                table: "IngredientCentralContributions",
                column: "ResultingCentralRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCentralContributions_Status",
                schema: "catering",
                table: "IngredientCentralContributions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCentralContributions_SubmittedLocalRevisionId",
                schema: "catering",
                table: "IngredientCentralContributions",
                column: "SubmittedLocalRevisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCentralContributions_SuggestedCentralIngredientId",
                schema: "catering",
                table: "IngredientCentralContributions",
                column: "SuggestedCentralIngredientId");

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientIdentities_IngredientIdentities_ReplacedByCentral~",
                schema: "catering",
                table: "IngredientIdentities",
                column: "ReplacedByCentralIngredientId",
                principalSchema: "catering",
                principalTable: "IngredientIdentities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IngredientIdentities_IngredientIdentities_ReplacedByCentral~",
                schema: "catering",
                table: "IngredientIdentities");

            migrationBuilder.DropTable(
                name: "IngredientCentralContributions",
                schema: "catering");

            migrationBuilder.DropIndex(
                name: "IX_IngredientIdentities_ReplacedByCentralIngredientId",
                schema: "catering",
                table: "IngredientIdentities");

            migrationBuilder.DropColumn(
                name: "ReplacedAtUtc",
                schema: "catering",
                table: "IngredientIdentities");

            migrationBuilder.DropColumn(
                name: "ReplacedBy",
                schema: "catering",
                table: "IngredientIdentities");

            migrationBuilder.DropColumn(
                name: "ReplacedByCentralIngredientId",
                schema: "catering",
                table: "IngredientIdentities");
        }
    }
}
