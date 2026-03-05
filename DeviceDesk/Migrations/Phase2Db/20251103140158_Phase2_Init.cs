using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <inheritdoc />
    public partial class Phase2_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Phase2AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: true),
                    DeviceSerial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Phase2Receipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GrvNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2Receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Phase2Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Serial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Zone = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    IctClerkId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReceivingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerificationStatus = table.Column<bool>(type: "bit", nullable: true),
                    PreAssessmentPassed = table.Column<bool>(type: "bit", nullable: true),
                    PreAssessmentInspectorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UnderWarranty = table.Column<bool>(type: "bit", nullable: true),
                    Repairable = table.Column<bool>(type: "bit", nullable: true),
                    TechnicianId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RepairCategory = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisposalRequested = table.Column<bool>(type: "bit", nullable: true),
                    QaPassed = table.Column<bool>(type: "bit", nullable: true),
                    QaInspectorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReworkCount = table.Column<int>(type: "int", nullable: false),
                    ReceiptId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase2Devices_Phase2Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Phase2Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Phase2Assessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    IsPreAssessment = table.Column<bool>(type: "bit", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DocumentRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2Assessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase2Assessments_Phase2Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Phase2Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Phase2Disposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ManagerSignature = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ManagerPinHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    DocumentPath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2Disposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase2Disposals_Phase2Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Phase2Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Phase2Quality",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2Quality", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase2Quality_Phase2Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Phase2Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phase2Assessments_DeviceId",
                table: "Phase2Assessments",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2Devices_ReceiptId",
                table: "Phase2Devices",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2Devices_Serial",
                table: "Phase2Devices",
                column: "Serial");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2Disposals_DeviceId",
                table: "Phase2Disposals",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2Quality_DeviceId",
                table: "Phase2Quality",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Phase2Assessments");

            migrationBuilder.DropTable(
                name: "Phase2AuditLogs");

            migrationBuilder.DropTable(
                name: "Phase2Disposals");

            migrationBuilder.DropTable(
                name: "Phase2Quality");

            migrationBuilder.DropTable(
                name: "Phase2Devices");

            migrationBuilder.DropTable(
                name: "Phase2Receipts");
        }
    }
}
