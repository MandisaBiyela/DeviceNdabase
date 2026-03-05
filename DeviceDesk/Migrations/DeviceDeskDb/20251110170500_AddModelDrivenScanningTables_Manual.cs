using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    /// <inheritdoc />
    [Migration("20251110170500_AddModelDrivenScanningTables_Manual")]
    public partial class AddModelDrivenScanningTables_Manual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // OrderModelLists table
            migrationBuilder.CreateTable(
                name: "OrderModelLists",
                columns: table => new
                {
                    ModelID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpectedQty = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CountedQty = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderModelLists", x => x.ModelID);
                    table.ForeignKey(
                        name: "FK_OrderModelLists_NewStockBatches_OrderID",
                        column: x => x.OrderID,
                        principalTable: "NewStockBatches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Cascade);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_OrderModelLists_OrderID",
                table: "OrderModelLists",
                column: "OrderID"
            );

            // ScannedSerials table
            migrationBuilder.CreateTable(
                name: "ScannedSerials",
                columns: table => new
                {
                    SerialID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceSerial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannedSerials", x => x.SerialID);
                    table.ForeignKey(
                        name: "FK_ScannedSerials_NewStockBatches_OrderID",
                        column: x => x.OrderID,
                        principalTable: "NewStockBatches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScannedSerials_OrderModelLists_ModelID",
                        column: x => x.ModelID,
                        principalTable: "OrderModelLists",
                        principalColumn: "ModelID",
                        onDelete: ReferentialAction.Cascade);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ScannedSerials_OrderID",
                table: "ScannedSerials",
                column: "OrderID"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ScannedSerials_ModelID",
                table: "ScannedSerials",
                column: "ModelID"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ScannedSerials_DeviceSerial",
                table: "ScannedSerials",
                column: "DeviceSerial",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScannedSerials"
            );

            migrationBuilder.DropTable(
                name: "OrderModelLists"
            );
        }
    }
}