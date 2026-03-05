using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <inheritdoc />
    public partial class Phase2_BulkAllocationSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BulkSessionId",
                table: "Phase2DeviceStorageLocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Phase2BulkAllocationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<long>(type: "bigint", nullable: false),
                    SchoolName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DeviceCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2BulkAllocationSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Phase2DeviceScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceSerial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ScanTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScannedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2DeviceScans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phase2DeviceStorageLocations_BulkSessionId",
                table: "Phase2DeviceStorageLocations",
                column: "BulkSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2BulkAllocationSessions_CreatedAt",
                table: "Phase2BulkAllocationSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2BulkAllocationSessions_SchoolId",
                table: "Phase2BulkAllocationSessions",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2DeviceScans_DeviceSerial",
                table: "Phase2DeviceScans",
                column: "DeviceSerial");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2DeviceScans_ScanTime",
                table: "Phase2DeviceScans",
                column: "ScanTime");

            migrationBuilder.AddForeignKey(
                name: "FK_Phase2DeviceStorageLocations_Phase2BulkAllocationSessions_BulkSessionId",
                table: "Phase2DeviceStorageLocations",
                column: "BulkSessionId",
                principalTable: "Phase2BulkAllocationSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Phase2DeviceStorageLocations_Phase2BulkAllocationSessions_BulkSessionId",
                table: "Phase2DeviceStorageLocations");

            migrationBuilder.DropTable(
                name: "Phase2BulkAllocationSessions");

            migrationBuilder.DropTable(
                name: "Phase2DeviceScans");

            migrationBuilder.DropIndex(
                name: "IX_Phase2DeviceStorageLocations_BulkSessionId",
                table: "Phase2DeviceStorageLocations");

            migrationBuilder.DropColumn(
                name: "BulkSessionId",
                table: "Phase2DeviceStorageLocations");
        }
    }
}
