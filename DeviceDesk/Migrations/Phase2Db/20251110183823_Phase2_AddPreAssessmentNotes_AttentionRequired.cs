using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase2Db
{
    /// <inheritdoc />
    public partial class Phase2_AddPreAssessmentNotes_AttentionRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Phase2ReceiptDiscrepancies");

            migrationBuilder.AddColumn<int>(
                name: "AttentionRequired",
                table: "Phase2Devices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PreAssessmentNotes",
                table: "Phase2Devices",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttentionRequired",
                table: "Phase2Devices");

            migrationBuilder.DropColumn(
                name: "PreAssessmentNotes",
                table: "Phase2Devices");

            migrationBuilder.CreateTable(
                name: "Phase2ReceiptDiscrepancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptId = table.Column<int>(type: "int", nullable: true),
                    ActualCount = table.Column<int>(type: "int", nullable: false),
                    ClerkId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Difference = table.Column<int>(type: "int", nullable: false),
                    ExpectedCount = table.Column<int>(type: "int", nullable: false),
                    GrvNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
    }
}
