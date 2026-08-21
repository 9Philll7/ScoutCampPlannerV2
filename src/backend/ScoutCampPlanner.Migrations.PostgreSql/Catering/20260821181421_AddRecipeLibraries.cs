using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Catering
{
    /// <inheritdoc />
    public partial class AddRecipeLibraries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampRecipeEntries",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpstreamRecipeRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampRecipeEntries", x => x.Id);
                    table.CheckConstraint("CK_CampRecipeEntries_Source", "(\"UpstreamRecipeRevisionId\" IS NOT NULL AND \"CampRecipeId\" IS NULL) OR (\"UpstreamRecipeRevisionId\" IS NULL AND \"CampRecipeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CampRecipeEntries_RecipeRevisions_UpstreamRecipeRevisionId",
                        column: x => x.UpstreamRecipeRevisionId,
                        principalSchema: "catering",
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CampRecipeEntries_Recipes_CampRecipeId",
                        column: x => x.CampRecipeId,
                        principalSchema: "catering",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantRecipeEntries",
                schema: "catering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CentralRecipeRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantRecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRecipeEntries", x => x.Id);
                    table.CheckConstraint("CK_TenantRecipeEntries_Source", "(\"CentralRecipeRevisionId\" IS NOT NULL AND \"TenantRecipeId\" IS NULL) OR (\"CentralRecipeRevisionId\" IS NULL AND \"TenantRecipeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TenantRecipeEntries_RecipeRevisions_CentralRecipeRevisionId",
                        column: x => x.CentralRecipeRevisionId,
                        principalSchema: "catering",
                        principalTable: "RecipeRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantRecipeEntries_Recipes_TenantRecipeId",
                        column: x => x.TenantRecipeId,
                        principalSchema: "catering",
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampRecipeEntries_CampId_CampRecipeId",
                schema: "catering",
                table: "CampRecipeEntries",
                columns: new[] { "CampId", "CampRecipeId" },
                unique: true,
                filter: "\"CampRecipeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CampRecipeEntries_CampId_UpstreamRecipeRevisionId",
                schema: "catering",
                table: "CampRecipeEntries",
                columns: new[] { "CampId", "UpstreamRecipeRevisionId" },
                unique: true,
                filter: "\"UpstreamRecipeRevisionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CampRecipeEntries_CampRecipeId",
                schema: "catering",
                table: "CampRecipeEntries",
                column: "CampRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_CampRecipeEntries_UpstreamRecipeRevisionId",
                schema: "catering",
                table: "CampRecipeEntries",
                column: "UpstreamRecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRecipeEntries_CentralRecipeRevisionId",
                schema: "catering",
                table: "TenantRecipeEntries",
                column: "CentralRecipeRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRecipeEntries_TenantId_CentralRecipeRevisionId",
                schema: "catering",
                table: "TenantRecipeEntries",
                columns: new[] { "TenantId", "CentralRecipeRevisionId" },
                unique: true,
                filter: "\"CentralRecipeRevisionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRecipeEntries_TenantId_TenantRecipeId",
                schema: "catering",
                table: "TenantRecipeEntries",
                columns: new[] { "TenantId", "TenantRecipeId" },
                unique: true,
                filter: "\"TenantRecipeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRecipeEntries_TenantRecipeId",
                schema: "catering",
                table: "TenantRecipeEntries",
                column: "TenantRecipeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampRecipeEntries",
                schema: "catering");

            migrationBuilder.DropTable(
                name: "TenantRecipeEntries",
                schema: "catering");
        }
    }
}
