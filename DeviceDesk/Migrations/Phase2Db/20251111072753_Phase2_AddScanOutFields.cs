using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <inheritdoc />
    public partial class Phase2_AddScanOutFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScannedOutAt",
                table: "Phase2Devices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScannedOutByUserId",
                table: "Phase2Devices",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScannedOutAt",
                table: "Phase2Devices");

            migrationBuilder.DropColumn(
                name: "ScannedOutByUserId",
                table: "Phase2Devices");
        }
    }
}
