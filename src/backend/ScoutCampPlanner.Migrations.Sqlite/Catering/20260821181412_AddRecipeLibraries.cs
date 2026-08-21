using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddRecipeLibraries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampRecipeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpstreamRecipeRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CampRecipeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampRecipeEntries", x => x.Id);
                    table.CheckConstraint("CK_CampRecipeEntries_Source", "(\"UpstreamRecipeRevisionId\" IS NOT NULL AND \"CampRecipeId\" IS NULL) OR (\"UpstreamRecipeRevisionId\" IS NULL AND \"CampRecipeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CampRecipeEntries_RecipeRevisions_UpstreamRecipeRevisionId",
                        column: x => x.UpstreamRecipeRevisionId,
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampRecipeEntries_Recipes_CampRecipeId",
                        column: x => x.CampRecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantRecipeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CentralRecipeRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantRecipeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRecipeEntries", x => x.Id);
                    table.CheckConstraint("CK_TenantRecipeEntries_Source", "(\"CentralRecipeRevisionId\" IS NOT NULL AND \"TenantRecipeId\" IS NULL) OR (\"CentralRecipeRevisionId\" IS NULL AND \"TenantRecipeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TenantRecipeEntries_RecipeRevisions_CentralRecipeRevisionId",
                        column: x => x.CentralRecipeRevisionId,
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantRecipeEntries_Recipes_TenantRecipeId",
                        column: x => x.TenantRecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampRecipeEntries_CampId_CampRecipeId",
                table: "CampRecipeEntries",
                columns: new[] { "CampId", "CampRecipeId" },
                unique: true,
                filter: "\"CampRecipeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CampRecipeEntries_CampId_UpstreamRecipeRevisionId",
                table: "CampRecipeEntries",
                columns: new[] { "CampId", "UpstreamRecipeRevisionId" },
                unique: true,
                filter: "\"UpstreamRecipeRevisionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CampRecipeEntries_CampRecipeId",
                table: "CampRecipeEntries",
                column: "CampRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampRecipeEntries_UpstreamRecipeRevisionId",
                table: "CampRecipeEntries",
                column: "UpstreamRecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRecipeEntries_CentralRecipeRevisionId",
                table: "TenantRecipeEntries",
                column: "CentralRecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRecipeEntries_TenantId_CentralRecipeRevisionId",
                table: "TenantRecipeEntries",
                columns: new[] { "TenantId", "CentralRecipeRevisionId" },
                unique: true,
                filter: "\"CentralRecipeRevisionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRecipeEntries_TenantId_TenantRecipeId",
                table: "TenantRecipeEntries",
                columns: new[] { "TenantId", "TenantRecipeId" },
                unique: true,
                filter: "\"TenantRecipeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRecipeEntries_TenantRecipeId",
                table: "TenantRecipeEntries",
                column: "TenantRecipeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampRecipeEntries");

            migrationBuilder.DropTable(
                name: "TenantRecipeEntries");
        }
    }
}
