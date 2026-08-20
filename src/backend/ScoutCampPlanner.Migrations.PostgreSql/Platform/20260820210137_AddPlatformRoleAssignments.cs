using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class AddPlatformRoleAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformRoleAssignments",
                schema: "platform",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRoleAssignments", x => new { x.UserId, x.RoleIdentifier });
                    table.ForeignKey(
                        name: "FK_PlatformRoleAssignments_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "platform",
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO platform."PlatformRoleAssignments" ("UserId", "RoleIdentifier")
                SELECT "Id", 'PlatformAdmin'
                FROM platform."UserAccounts"
                WHERE (SELECT COUNT(*) FROM platform."UserAccounts") = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformRoleAssignments",
                schema: "platform");
        }
    }
}
