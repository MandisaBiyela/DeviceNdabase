using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <inheritdoc />
    public partial class Phase2_DeviceStorageLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Phase2DeviceStorageLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Phase2DeviceId = table.Column<int>(type: "int", nullable: false),
                    StorageLocationId = table.Column<int>(type: "int", nullable: true),
                    Building = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Room = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Rack = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Shelf = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Bin = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2DeviceStorageLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase2DeviceStorageLocations_Phase2Devices_Phase2DeviceId",
                        column: x => x.Phase2DeviceId,
                        principalTable: "Phase2Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phase2DeviceStorageLocations_Phase2DeviceId",
                table: "Phase2DeviceStorageLocations",
                column: "Phase2DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2DeviceStorageLocations_Phase2DeviceId_Status",
                table: "Phase2DeviceStorageLocations",
                columns: new[] { "Phase2DeviceId", "Status" },
                unique: true,
                filter: "[Status] = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Phase2DeviceStorageLocations");
        }
    }
}
