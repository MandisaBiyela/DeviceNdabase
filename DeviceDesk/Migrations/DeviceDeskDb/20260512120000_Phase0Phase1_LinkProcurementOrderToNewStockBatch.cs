using System;
using DeviceDesk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    /// <inheritdoc />
    [DbContext(typeof(DeviceDeskDbContext))]
    [Migration("20260512120000_Phase0Phase1_LinkProcurementOrderToNewStockBatch")]
    public partial class Phase0Phase1_LinkProcurementOrderToNewStockBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ProcurementOrders → supplier / expected delivery date / back-link to NewStockBatch
            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "ProcurementOrders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpectedDeliveryDate",
                table: "ProcurementOrders",
                type: "datetimeoffset(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NewStockBatchId",
                table: "ProcurementOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementOrders_NewStockBatchId",
                table: "ProcurementOrders",
                column: "NewStockBatchId");

            // ProcurementOrderItems → split Brand / Model / DeviceType from Description
            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "ProcurementOrderItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "ProcurementOrderItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "ProcurementOrderItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // NewStockBatches → link to ProcurementOrders and surface PO / Project / FY
            migrationBuilder.AddColumn<Guid>(
                name: "ProcurementOrderId",
                table: "NewStockBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoNumber",
                table: "NewStockBatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "NewStockBatches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinancialYear",
                table: "NewStockBatches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewStockBatches_ProcurementOrderId",
                table: "NewStockBatches",
                column: "ProcurementOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_NewStockBatches_PoNumber",
                table: "NewStockBatches",
                column: "PoNumber");

            // NewStockBatchItems → unit price + per-school breakdown JSON
            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "NewStockBatchItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SchoolBreakdownJson",
                table: "NewStockBatchItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SchoolBreakdownJson", table: "NewStockBatchItems");
            migrationBuilder.DropColumn(name: "UnitPrice", table: "NewStockBatchItems");

            migrationBuilder.DropIndex(name: "IX_NewStockBatches_PoNumber", table: "NewStockBatches");
            migrationBuilder.DropIndex(name: "IX_NewStockBatches_ProcurementOrderId", table: "NewStockBatches");
            migrationBuilder.DropColumn(name: "FinancialYear", table: "NewStockBatches");
            migrationBuilder.DropColumn(name: "ProjectName", table: "NewStockBatches");
            migrationBuilder.DropColumn(name: "PoNumber", table: "NewStockBatches");
            migrationBuilder.DropColumn(name: "ProcurementOrderId", table: "NewStockBatches");

            migrationBuilder.DropColumn(name: "DeviceType", table: "ProcurementOrderItems");
            migrationBuilder.DropColumn(name: "Model", table: "ProcurementOrderItems");
            migrationBuilder.DropColumn(name: "Brand", table: "ProcurementOrderItems");

            migrationBuilder.DropIndex(name: "IX_ProcurementOrders_NewStockBatchId", table: "ProcurementOrders");
            migrationBuilder.DropColumn(name: "NewStockBatchId", table: "ProcurementOrders");
            migrationBuilder.DropColumn(name: "ExpectedDeliveryDate", table: "ProcurementOrders");
            migrationBuilder.DropColumn(name: "SupplierName", table: "ProcurementOrders");
        }
    }
}
