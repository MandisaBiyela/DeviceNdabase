using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <inheritdoc />
    public partial class Phase2_AddStorageTemplatesAndOccupancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Phase2SchoolStorageTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Building = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Room = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RackPattern = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ShelfPattern = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BinPattern = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaxRacks = table.Column<int>(type: "int", nullable: false),
                    MaxShelvesPerRack = table.Column<int>(type: "int", nullable: false),
                    MaxBinsPerShelf = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2SchoolStorageTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Phase2StorageSlotOccupancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Building = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Room = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Rack = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Shelf = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Bin = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Phase2DeviceId = table.Column<int>(type: "int", nullable: false),
                    IsOccupied = table.Column<bool>(type: "bit", nullable: false),
                    OccupiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VacatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2StorageSlotOccupancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase2StorageSlotOccupancies_Phase2Devices_Phase2DeviceId",
                        column: x => x.Phase2DeviceId,
                        principalTable: "Phase2Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phase2SchoolStorageTemplates_SchoolId_Category",
                table: "Phase2SchoolStorageTemplates",
                columns: new[] { "SchoolId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Phase2StorageSlotOccupancies_Phase2DeviceId",
                table: "Phase2StorageSlotOccupancies",
                column: "Phase2DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase2StorageSlotOccupancies_SchoolId_Category_Building_Room_Rack_Shelf_Bin",
                table: "Phase2StorageSlotOccupancies",
                columns: new[] { "SchoolId", "Category", "Building", "Room", "Rack", "Shelf", "Bin" });

            migrationBuilder.CreateIndex(
                name: "IX_Phase2StorageSlotOccupancies_SchoolId_Category_IsOccupied",
                table: "Phase2StorageSlotOccupancies",
                columns: new[] { "SchoolId", "Category", "IsOccupied" },
                filter: "[IsOccupied] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Phase2SchoolStorageTemplates");

            migrationBuilder.DropTable(
                name: "Phase2StorageSlotOccupancies");
        }
    }
}
