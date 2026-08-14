using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Platform
{
    /// <inheritdoc />
    public partial class AddCampMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantMembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampMemberships_TenantMemberships_TenantMembershipId",
                        column: x => x.TenantMembershipId,
                        principalTable: "TenantMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampRoleAssignments",
                columns: table => new
                {
                    MembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleIdentifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampRoleAssignments", x => new { x.MembershipId, x.RoleIdentifier });
                    table.ForeignKey(
                        name: "FK_CampRoleAssignments_CampMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "CampMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampMemberships_CampId",
                table: "CampMemberships",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_CampMemberships_TenantMembershipId",
                table: "CampMemberships",
                column: "TenantMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_CampMemberships_TenantMembershipId_CampId",
                table: "CampMemberships",
                columns: new[] { "TenantMembershipId", "CampId" },
                unique: true,
                filter: "\"State\" <> 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampRoleAssignments");

            migrationBuilder.DropTable(
                name: "CampMemberships");
        }
    }
}
