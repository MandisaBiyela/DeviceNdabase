using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase1Db
{
    /// <inheritdoc />
    public partial class AddNewStockBatchIdToReceivingBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DeviceName column already exists, skip it
            // migrationBuilder.AddColumn<string>(
            //     name: "DeviceName",
            //     table: "RnrExpectedItems",
            //     type: "nvarchar(max)",
            //     nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NewStockBatchId",
                table: "ReceivingBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingBatches_NewStockBatchId",
                table: "ReceivingBatches",
                column: "NewStockBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReceivingBatches_NewStockBatchId",
                table: "ReceivingBatches");

            // DeviceName column should not be dropped as it was added in a previous migration
            // migrationBuilder.DropColumn(
            //     name: "DeviceName",
            //     table: "RnrExpectedItems");

            migrationBuilder.DropColumn(
                name: "NewStockBatchId",
                table: "ReceivingBatches");
        }
    }
}
