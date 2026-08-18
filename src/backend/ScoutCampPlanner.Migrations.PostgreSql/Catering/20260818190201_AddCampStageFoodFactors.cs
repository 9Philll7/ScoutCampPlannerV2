using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddCampStageFoodFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampStageFoodFactors",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Factor = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampStageFoodFactors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampStageFoodFactors_CampId",
                schema: "catering",
                table: "CampStageFoodFactors",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_CampStageFoodFactors_CampId_CampStageId",
                schema: "catering",
                table: "CampStageFoodFactors",
                columns: new[] { "CampId", "CampStageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampStageFoodFactors",
                schema: "catering");
        }
    }
}
