using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase3Db
{
    /// <inheritdoc />
    public partial class Phase3_AddDispatchBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ──────────────────────────────────────────────
            // Phase3_DispatchBatches
            // ──────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Phase3_DispatchBatches",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),

                    // Batch Status
                    Status = table.Column<int>(type: "int", nullable: false),

                    // School details
                    SchoolName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    District = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EmisCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),

                    // Source details
                    StockType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),

                    // Generated documents
                    PODNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeliveryNoteNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),

                    // Trip details
                    TripReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DriverName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DriverUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    VehicleReg = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),

                    // Document references
                    DeliveryNoteDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    PODDocumentId = table.Column<long>(type: "bigint", nullable: true),

                    // Audit
                    AuditPassed = table.Column<bool>(type: "bit", nullable: false),
                    AuditCompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditCompletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),

                    // Transport / en-route
                    EnRoute = table.Column<bool>(type: "bit", nullable: false),
                    EnRouteAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EnRouteByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ArrivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArrivedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),

                    // Debrief / school sign-off
                    SchoolSigned = table.Column<bool>(type: "bit", nullable: false),
                    SchoolSignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SchoolSignatoryName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SignedPODDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    DebriefCompleted = table.Column<bool>(type: "bit", nullable: false),
                    DebriefCompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DebriefCompletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DebriefNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasExceptions = table.Column<bool>(type: "bit", nullable: false),
                    ExceptionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExceptionPhotos = table.Column<string>(type: "nvarchar(max)", nullable: true),

                    // Audit / lifecycle meta
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LockedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase3_DispatchBatches", x => x.BatchId);
                });

            // Indexes for Phase3_DispatchBatches (as per OnModelCreating)
            migrationBuilder.CreateIndex(
                name: "IX_Phase3_DispatchBatches_PODNumber",
                table: "Phase3_DispatchBatches",
                column: "PODNumber",
                unique: true,
                filter: "[PODNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Phase3_DispatchBatches_Status",
                table: "Phase3_DispatchBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Phase3_DispatchBatches_CreatedAt",
                table: "Phase3_DispatchBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Phase3_DispatchBatches_SchoolName",
                table: "Phase3_DispatchBatches",
                column: "SchoolName");

            // ──────────────────────────────────────────────
            // Phase3_BatchDevices
            // ──────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Phase3_BatchDevices",
                columns: table => new
                {
                    BatchDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),

                    // FKs
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: false),

                    // Device info
                    Serial = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),

                    // Audit status
                    ScannedInAudit = table.Column<bool>(type: "bit", nullable: false),
                    ScannedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScannedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),

                    // Timestamps
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AddedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase3_BatchDevices", x => x.BatchDeviceId);
                    table.ForeignKey(
                        name: "FK_Phase3_BatchDevices_Phase3_DispatchBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Phase3_DispatchBatches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phase3_BatchDevices_BatchId",
                table: "Phase3_BatchDevices",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase3_BatchDevices_DeviceId",
                table: "Phase3_BatchDevices",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase3_BatchDevices_Serial",
                table: "Phase3_BatchDevices",
                column: "Serial");

            // ──────────────────────────────────────────────
            // Phase3_LoadingAuditScans
            // ──────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Phase3_LoadingAuditScans",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),

                    // FK
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),

                    // Scan details
                    ScannedSerials = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedCount = table.Column<int>(type: "int", nullable: false),
                    ScannedCount = table.Column<int>(type: "int", nullable: false),

                    // Mismatch
                    AuditPassed = table.Column<bool>(type: "bit", nullable: false),
                    MismatchDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),

                    // Timestamps / user
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AuditedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase3_LoadingAuditScans", x => x.AuditId);
                    table.ForeignKey(
                        name: "FK_Phase3_LoadingAuditScans_Phase3_DispatchBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Phase3_DispatchBatches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phase3_LoadingAuditScans_BatchId",
                table: "Phase3_LoadingAuditScans",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase3_LoadingAuditScans_StartedAt",
                table: "Phase3_LoadingAuditScans",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Phase3_LoadingAuditScans");

            migrationBuilder.DropTable(
                name: "Phase3_BatchDevices");

            migrationBuilder.DropTable(
                name: "Phase3_DispatchBatches");
        }
    }
}
