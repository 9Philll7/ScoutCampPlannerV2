using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Platform
{
    /// <inheritdoc />
    public partial class AddTenantRoleAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantRoleAssignments",
                columns: table => new
                {
                    MembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleIdentifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRoleAssignments", x => new { x.MembershipId, x.RoleIdentifier });
                    table.ForeignKey(
                        name: "FK_TenantRoleAssignments_TenantMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "TenantMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantRoleAssignments_MembershipId",
                table: "TenantRoleAssignments",
                column: "MembershipId",
                unique: true,
                filter: "\"RoleIdentifier\" IN ('TenantOwner', 'TenantAdmin', 'TenantMember')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantRoleAssignments");
        }
    }
}
