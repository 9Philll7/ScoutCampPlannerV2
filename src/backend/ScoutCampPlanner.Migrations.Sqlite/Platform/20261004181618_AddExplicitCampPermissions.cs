using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Platform
{
    /// <inheritdoc />
    public partial class AddExplicitCampPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampPermissionGrants",
                columns: table => new
                {
                    MembershipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampPermissionGrants", x => new { x.MembershipId, x.Permission });
                    table.ForeignKey(
                        name: "FK_CampPermissionGrants_CampMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "CampMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocalCampPermissionGrants",
                columns: table => new
                {
                    DeviceIdentityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransferId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalCampPermissionGrants", x => new { x.DeviceIdentityId, x.CampId, x.TransferId, x.Permission });
                    table.ForeignKey(
                        name: "FK_LocalCampPermissionGrants_LocalCampAccessGrants_DeviceIdentityId_CampId",
                        columns: x => new { x.DeviceIdentityId, x.CampId },
                        principalTable: "LocalCampAccessGrants",
                        principalColumns: new[] { "DeviceIdentityId", "CampId" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampPermissionGrants");

            migrationBuilder.DropTable(
                name: "LocalCampPermissionGrants");
        }
    }
}
