using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class AddLocalDeviceCampAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalDeviceIdentities",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalDeviceIdentities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalCampAccessGrants",
                schema: "platform",
                columns: table => new
                {
                    DeviceIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalCampAccessGrants", x => new { x.DeviceIdentityId, x.CampId });
                    table.ForeignKey(
                        name: "FK_LocalCampAccessGrants_LocalDeviceIdentities_DeviceIdentityId",
                        column: x => x.DeviceIdentityId,
                        principalSchema: "platform",
                        principalTable: "LocalDeviceIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocalCampAccessGrants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocalCampAccessGrants_CampId",
                schema: "platform",
                table: "LocalCampAccessGrants",
                column: "CampId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalCampAccessGrants_TenantId",
                schema: "platform",
                table: "LocalCampAccessGrants",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalCampAccessGrants",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "LocalDeviceIdentities",
                schema: "platform");
        }
    }
}
