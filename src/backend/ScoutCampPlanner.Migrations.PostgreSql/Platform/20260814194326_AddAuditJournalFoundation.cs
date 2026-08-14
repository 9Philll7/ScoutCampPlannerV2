using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoutCampPlanner.Migrations.PostgreSql.Platform
{
    /// <inheritdoc />
    public partial class AddAuditJournalFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditSegments",
                schema: "platform",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstSequence = table.Column<long>(type: "bigint", nullable: false),
                    LastSequence = table.Column<long>(type: "bigint", nullable: true),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    KeyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FormatVersion = table.Column<int>(type: "integer", nullable: false),
                    FirstPredecessorHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    ClosingHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EventsDeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditSegments", x => new { x.InstanceId, x.SegmentId });
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "platform",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Result = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecurityVersion = table.Column<int>(type: "integer", nullable: true),
                    RoleDefinitionVersion = table.Column<int>(type: "integer", nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: false),
                    PreviousHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    Hmac = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    KeyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FormatVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => new { x.InstanceId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_AuditEvents_AuditSegments_InstanceId_SegmentId",
                        columns: x => new { x.InstanceId, x.SegmentId },
                        principalSchema: "platform",
                        principalTable: "AuditSegments",
                        principalColumns: new[] { "InstanceId", "SegmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditJournalHeads",
                schema: "platform",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Head = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    KeyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FormatVersion = table.Column<int>(type: "integer", nullable: false),
                    ActiveSegmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditJournalHeads", x => x.InstanceId);
                    table.ForeignKey(
                        name: "FK_AuditJournalHeads_AuditSegments_InstanceId_ActiveSegmentId",
                        columns: x => new { x.InstanceId, x.ActiveSegmentId },
                        principalSchema: "platform",
                        principalTable: "AuditSegments",
                        principalColumns: new[] { "InstanceId", "SegmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_InstanceId_EventId",
                schema: "platform",
                table: "AuditEvents",
                columns: new[] { "InstanceId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_InstanceId_SegmentId_Sequence",
                schema: "platform",
                table: "AuditEvents",
                columns: new[] { "InstanceId", "SegmentId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TimestampUtc",
                schema: "platform",
                table: "AuditEvents",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditJournalHeads_InstanceId_ActiveSegmentId",
                schema: "platform",
                table: "AuditJournalHeads",
                columns: new[] { "InstanceId", "ActiveSegmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditSegments_InstanceId_FirstSequence",
                schema: "platform",
                table: "AuditSegments",
                columns: new[] { "InstanceId", "FirstSequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AuditJournalHeads",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "AuditSegments",
                schema: "platform");
        }
    }
}
