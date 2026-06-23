using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    /// <inheritdoc />
    public partial class Phase0_ProcurementOrderManagementFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ManagementFeeAmount",
                table: "ProcurementOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ManagementFeePercentage",
                table: "ProcurementOrders",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SupplierFee",
                table: "ProcurementOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                "UPDATE ProcurementOrders SET SupplierFee = TotalOrderValue WHERE SupplierFee = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagementFeeAmount",
                table: "ProcurementOrders");

            migrationBuilder.DropColumn(
                name: "ManagementFeePercentage",
                table: "ProcurementOrders");

            migrationBuilder.DropColumn(
                name: "SupplierFee",
                table: "ProcurementOrders");
        }
    }
}
