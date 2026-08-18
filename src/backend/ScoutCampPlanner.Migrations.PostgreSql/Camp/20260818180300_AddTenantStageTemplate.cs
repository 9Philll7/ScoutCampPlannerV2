using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Camp
{
    /// <inheritdoc />
    public partial class AddTenantStageTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantStageTemplateEntries",
                schema: "camp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantStageTemplateEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantStageTemplateEntries_TenantId_NormalizedName",
                schema: "camp",
                table: "TenantStageTemplateEntries",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantStageTemplateEntries_TenantId_SortOrder",
                schema: "camp",
                table: "TenantStageTemplateEntries",
                columns: new[] { "TenantId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantStageTemplateEntries",
                schema: "camp");
        }
    }
}
