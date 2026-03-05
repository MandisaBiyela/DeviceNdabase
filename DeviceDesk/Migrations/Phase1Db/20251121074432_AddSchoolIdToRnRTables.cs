using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase1Db
{
    /// <inheritdoc />
    public partial class AddSchoolIdToRnRTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Orders");

            migrationBuilder.AddColumn<long>(
                name: "SchoolId",
                table: "RnrExpectedItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SchoolId",
                table: "ReceivingBatchScans",
                type: "bigint",
                nullable: true);

            // Create indexes for better performance
            migrationBuilder.CreateIndex(
                name: "IX_RnrExpectedItems_SchoolId",
                table: "RnrExpectedItems",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingBatchScans_SchoolId",
                table: "ReceivingBatchScans",
                column: "SchoolId");

            // Backfill existing data with school information from batches
            migrationBuilder.Sql(@"
                UPDATE ei 
                SET ei.SchoolId = rb.SchoolId
                FROM RnrExpectedItems ei
                INNER JOIN ReceivingBatches rb ON ei.BatchId = rb.ReceivingBatchId
                WHERE ei.SchoolId IS NULL AND rb.SchoolId IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE s 
                SET s.SchoolId = rb.SchoolId
                FROM ReceivingBatchScans s
                INNER JOIN ReceivingBatches rb ON s.BatchId = rb.ReceivingBatchId
                WHERE s.SchoolId IS NULL AND rb.SchoolId IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_RnrExpectedItems_SchoolId",
                table: "RnrExpectedItems");

            migrationBuilder.DropIndex(
                name: "IX_ReceivingBatchScans_SchoolId",
                table: "ReceivingBatchScans");

            // Drop columns
            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "RnrExpectedItems");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "ReceivingBatchScans");

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
