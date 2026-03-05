using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    /// <inheritdoc />
    public partial class AddStorageLocationsAndDeviceLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Devices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StorageLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<long>(type: "bigint", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LocationCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsDispatchReadyZone = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageLocations_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId");
                });

            migrationBuilder.CreateTable(
                name: "DeviceLocationHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromLocationId = table.Column<int>(type: "int", nullable: true),
                    ToLocationId = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MovedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLocationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceLocationHistory_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceLocationHistory_StorageLocations_FromLocationId",
                        column: x => x.FromLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceLocationHistory_StorageLocations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeviceLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageLocationId = table.Column<int>(type: "int", nullable: false),
                    MovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    MovedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceLocations_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceLocations_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLocationHistory_DeviceId",
                table: "DeviceLocationHistory",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLocationHistory_FromLocationId",
                table: "DeviceLocationHistory",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLocationHistory_ToLocationId",
                table: "DeviceLocationHistory",
                column: "ToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLocations_DeviceId_IsCurrent",
                table: "DeviceLocations",
                columns: new[] { "DeviceId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLocations_StorageLocationId",
                table: "DeviceLocations",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_LocationCode",
                table: "StorageLocations",
                column: "LocationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_SchoolId_Category_Area",
                table: "StorageLocations",
                columns: new[] { "SchoolId", "Category", "Area" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceLocationHistory");

            migrationBuilder.DropTable(
                name: "DeviceLocations");

            migrationBuilder.DropTable(
                name: "StorageLocations");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Devices");
        }
    }
}
