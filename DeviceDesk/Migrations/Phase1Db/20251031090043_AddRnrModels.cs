using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase1Db
{
    /// <inheritdoc />
    public partial class AddRnrModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceivingBatchScans",
                columns: table => new
                {
                    ReceivingBatchScanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScannedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DeviceInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SchoolMatch = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivingBatchScans", x => x.ReceivingBatchScanId);
                });

            migrationBuilder.CreateTable(
                name: "RnrExpectedItems",
                columns: table => new
                {
                    RnrExpectedItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RnrExpectedItems", x => x.RnrExpectedItemId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingBatchScans_BatchId_Serial",
                table: "ReceivingBatchScans",
                columns: new[] { "BatchId", "Serial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RnrExpectedItems_BatchId_Serial",
                table: "RnrExpectedItems",
                columns: new[] { "BatchId", "Serial" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceivingBatchScans");

            migrationBuilder.DropTable(
                name: "RnrExpectedItems");
        }
    }
}
