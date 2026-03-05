using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    /// <inheritdoc />
    public partial class Phase3_AddDispatchPods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DispatchPods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PodNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DeliveryNoteNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SchoolName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    District = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StockType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PodDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    DeliveryNoteDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    SignedPodDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    SignedPodUploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SignedPodUploadedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatchPods", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DispatchPods_PodNumber",
                table: "DispatchPods",
                column: "PodNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispatchPods");
        }
    }
}
