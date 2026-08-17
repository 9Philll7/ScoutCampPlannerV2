using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Camp
{
    /// <inheritdoc />
    public partial class AddFlexibleCampStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CookingUnits",
                schema: "camp");

            migrationBuilder.AddColumn<int>(
                name: "StructureMode",
                schema: "camp",
                table: "Camps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StructureNodes",
                schema: "camp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StructureNodes_Camps_CampId",
                        column: x => x.CampId,
                        principalSchema: "camp",
                        principalTable: "Camps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StructureNodes_StructureNodes_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "camp",
                        principalTable: "StructureNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_CampId",
                schema: "camp",
                table: "StructureNodes",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_CampId_NormalizedName",
                schema: "camp",
                table: "StructureNodes",
                columns: new[] { "CampId", "NormalizedName" },
                unique: true,
                filter: "\"ParentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_CampId_ParentId_NormalizedName",
                schema: "camp",
                table: "StructureNodes",
                columns: new[] { "CampId", "ParentId", "NormalizedName" },
                unique: true,
                filter: "\"ParentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_ParentId",
                schema: "camp",
                table: "StructureNodes",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructureNodes",
                schema: "camp");

            migrationBuilder.DropColumn(
                name: "StructureMode",
                schema: "camp",
                table: "Camps");

            migrationBuilder.CreateTable(
                name: "CookingUnits",
                schema: "camp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnits_CampId",
                schema: "camp",
                table: "CookingUnits",
                column: "CampId");
        }
    }
}
