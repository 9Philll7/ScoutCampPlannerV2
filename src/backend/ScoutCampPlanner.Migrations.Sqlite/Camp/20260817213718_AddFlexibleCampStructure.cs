using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Camp
{
    /// <inheritdoc />
    public partial class AddFlexibleCampStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CookingUnits");

            migrationBuilder.AddColumn<int>(
                name: "StructureMode",
                table: "Camps",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StructureNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StructureNodes_Camps_CampId",
                        column: x => x.CampId,
                        principalTable: "Camps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StructureNodes_StructureNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "StructureNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_CampId",
                table: "StructureNodes",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_CampId_NormalizedName",
                table: "StructureNodes",
                columns: new[] { "CampId", "NormalizedName" },
                unique: true,
                filter: "\"ParentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_CampId_ParentId_NormalizedName",
                table: "StructureNodes",
                columns: new[] { "CampId", "ParentId", "NormalizedName" },
                unique: true,
                filter: "\"ParentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_ParentId",
                table: "StructureNodes",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructureNodes");

            migrationBuilder.DropColumn(
                name: "StructureMode",
                table: "Camps");

            migrationBuilder.CreateTable(
                name: "CookingUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CookingUnits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CookingUnits_CampId",
                table: "CookingUnits",
                column: "CampId");
        }
    }
}
