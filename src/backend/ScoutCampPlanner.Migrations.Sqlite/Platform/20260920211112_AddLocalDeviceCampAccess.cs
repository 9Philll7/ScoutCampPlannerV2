using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Platform
{
    /// <inheritdoc />
    public partial class AddLocalDeviceCampAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalDeviceIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalDeviceIdentities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalCampAccessGrants",
                columns: table => new
                {
                    DeviceIdentityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransferId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalCampAccessGrants", x => new { x.DeviceIdentityId, x.CampId });
                    table.ForeignKey(
                        name: "FK_LocalCampAccessGrants_LocalDeviceIdentities_DeviceIdentityId",
                        column: x => x.DeviceIdentityId,
                        principalTable: "LocalDeviceIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocalCampAccessGrants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocalCampAccessGrants_CampId",
                table: "LocalCampAccessGrants",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalCampAccessGrants_TenantId",
                table: "LocalCampAccessGrants",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalCampAccessGrants");

            migrationBuilder.DropTable(
                name: "LocalDeviceIdentities");
        }
    }
}
