using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddTenantStageFoodFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantStageFoodFactors",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedStageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Factor = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantStageFoodFactors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantStageFoodFactors_TenantId_NormalizedStageName",
                schema: "catering",
                table: "TenantStageFoodFactors",
                columns: new[] { "TenantId", "NormalizedStageName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantStageFoodFactors",
                schema: "catering");
        }
    }
}
