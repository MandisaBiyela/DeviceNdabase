using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <inheritdoc />
    public partial class Phase2_AddReceiptDiscrepancies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Phase2ReceiptDiscrepancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptId = table.Column<int>(type: "int", nullable: true),
                    GrvNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ClerkId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpectedCount = table.Column<int>(type: "int", nullable: false),
                    ActualCount = table.Column<int>(type: "int", nullable: false),
                    Difference = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phase1ClerkEmail = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase2ReceiptDiscrepancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase2ReceiptDiscrepancies_Phase2Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Phase2Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phase2ReceiptDiscrepancies_ReceiptId",
                table: "Phase2ReceiptDiscrepancies",
                column: "ReceiptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Phase2ReceiptDiscrepancies");
        }
    }
}
