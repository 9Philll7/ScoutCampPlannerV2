using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class AddCampMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampMemberships",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampMemberships_TenantMemberships_TenantMembershipId",
                        column: x => x.TenantMembershipId,
                        principalSchema: "platform",
                        principalTable: "TenantMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampRoleAssignments",
                schema: "platform",
                columns: table => new
                {
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampRoleAssignments", x => new { x.MembershipId, x.RoleIdentifier });
                    table.ForeignKey(
                        name: "FK_CampRoleAssignments_CampMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalSchema: "platform",
                        principalTable: "CampMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampMemberships_CampId",
                schema: "platform",
                table: "CampMemberships",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_CampMemberships_TenantMembershipId",
                schema: "platform",
                table: "CampMemberships",
                column: "TenantMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_CampMemberships_TenantMembershipId_CampId",
                schema: "platform",
                table: "CampMemberships",
                columns: new[] { "TenantMembershipId", "CampId" },
                unique: true,
                filter: "\"State\" <> 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampRoleAssignments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "CampMemberships",
                schema: "platform");
        }
    }
}
