using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Catering
{
    /// <inheritdoc />
    public partial class AddCampParticipantFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampParticipantFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampParticipantFilters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampParticipantFilters_CampId",
                table: "CampParticipantFilters",
                column: "CampId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampParticipantFilters");
        }
    }
}
