using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.Sqlite.Platform
{
    /// <inheritdoc />
    public partial class AddAuditJournalFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditSegments",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SegmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FirstSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSequence = table.Column<long>(type: "INTEGER", nullable: true),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    KeyId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FormatVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstPredecessorHash = table.Column<byte[]>(type: "BLOB", maxLength: 32, nullable: false),
                    ClosingHash = table.Column<byte[]>(type: "BLOB", maxLength: 32, nullable: true),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EventsDeletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditSegments", x => new { x.InstanceId, x.SegmentId });
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SegmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CampId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SecurityVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    RoleDefinitionVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousHash = table.Column<byte[]>(type: "BLOB", maxLength: 32, nullable: false),
                    Hmac = table.Column<byte[]>(type: "BLOB", maxLength: 32, nullable: false),
                    KeyId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FormatVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => new { x.InstanceId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_AuditEvents_AuditSegments_InstanceId_SegmentId",
                        columns: x => new { x.InstanceId, x.SegmentId },
                        principalTable: "AuditSegments",
                        principalColumns: new[] { "InstanceId", "SegmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditJournalHeads",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Head = table.Column<byte[]>(type: "BLOB", maxLength: 32, nullable: false),
                    KeyId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FormatVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveSegmentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditJournalHeads", x => x.InstanceId);
                    table.ForeignKey(
                        name: "FK_AuditJournalHeads_AuditSegments_InstanceId_ActiveSegmentId",
                        columns: x => new { x.InstanceId, x.ActiveSegmentId },
                        principalTable: "AuditSegments",
                        principalColumns: new[] { "InstanceId", "SegmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_InstanceId_EventId",
                table: "AuditEvents",
                columns: new[] { "InstanceId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_InstanceId_SegmentId_Sequence",
                table: "AuditEvents",
                columns: new[] { "InstanceId", "SegmentId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TimestampUtc",
                table: "AuditEvents",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditJournalHeads_InstanceId_ActiveSegmentId",
                table: "AuditJournalHeads",
                columns: new[] { "InstanceId", "ActiveSegmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditSegments_InstanceId_FirstSequence",
                table: "AuditSegments",
                columns: new[] { "InstanceId", "FirstSequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "AuditJournalHeads");

            migrationBuilder.DropTable(
                name: "AuditSegments");
        }
    }
}
