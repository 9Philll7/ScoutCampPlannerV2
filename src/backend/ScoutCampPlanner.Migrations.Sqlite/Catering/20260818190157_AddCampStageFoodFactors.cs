using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddCampStageFoodFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampStageFoodFactors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampStageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StageName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Factor = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampStageFoodFactors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampStageFoodFactors_CampId",
                table: "CampStageFoodFactors",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_CampStageFoodFactors_CampId_CampStageId",
                table: "CampStageFoodFactors",
                columns: new[] { "CampId", "CampStageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampStageFoodFactors");
        }
    }
}
