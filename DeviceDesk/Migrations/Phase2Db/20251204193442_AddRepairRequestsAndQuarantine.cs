using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <inheritdoc />
    public partial class AddRepairRequestsAndQuarantine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Phase2Devices",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ScannedOutAt",
                table: "Phase2Devices",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ReceivingDate",
                table: "Phase2Devices",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "InspectionDate",
                table: "Phase2Devices",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Phase2Devices",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<bool>(
                name: "IsQuarantined",
                table: "Phase2Devices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "QuarantineReason",
                table: "Phase2Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QuarantinedAtUtc",
                table: "Phase2Devices",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                table: "Phase2Assessments",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.CreateTable(
                name: "Phase2RepairRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    DeviceSerial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WarrantyRoute = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsUnderWarranty = table.Column<bool>(type: "bit", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    SymptomDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicianFindings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HardwareChecklistSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecommendedAction = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EstimatedLabourHours = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2RepairRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase2RepairRequests_Phase2Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Phase2Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Phase2RepairParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepairRequestId = table.Column<int>(type: "int", nullable: false),
                    PartName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Supplier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2RepairParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase2RepairParts_Phase2RepairRequests_RepairRequestId",
                        column: x => x.RepairRequestId,
                        principalTable: "Phase2RepairRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phase2RepairParts_RepairRequestId",
                table: "Phase2RepairParts",
                column: "RepairRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2RepairRequests_CreatedAtUtc",
                table: "Phase2RepairRequests",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2RepairRequests_DeviceId",
                table: "Phase2RepairRequests",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2RepairRequests_DeviceSerial",
                table: "Phase2RepairRequests",
                column: "DeviceSerial");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2RepairRequests_Status",
                table: "Phase2RepairRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Phase2RepairParts");

            migrationBuilder.DropTable(
                name: "Phase2RepairRequests");

            migrationBuilder.DropColumn(
                name: "IsQuarantined",
                table: "Phase2Devices");

            migrationBuilder.DropColumn(
                name: "QuarantineReason",
                table: "Phase2Devices");

            migrationBuilder.DropColumn(
                name: "QuarantinedAtUtc",
                table: "Phase2Devices");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Phase2Devices",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ScannedOutAt",
                table: "Phase2Devices",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ReceivingDate",
                table: "Phase2Devices",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "InspectionDate",
                table: "Phase2Devices",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Phase2Devices",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "Phase2Assessments",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");
        }
    }
}
