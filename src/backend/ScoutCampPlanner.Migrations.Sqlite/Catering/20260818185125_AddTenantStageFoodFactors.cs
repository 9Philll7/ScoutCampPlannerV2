using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddTenantStageFoodFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantStageFoodFactors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StageName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedStageName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Factor = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantStageFoodFactors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantStageFoodFactors_TenantId_NormalizedStageName",
                table: "TenantStageFoodFactors",
                columns: new[] { "TenantId", "NormalizedStageName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantStageFoodFactors");
        }
    }
}
