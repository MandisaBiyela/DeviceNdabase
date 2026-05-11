using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    /// <inheritdoc />
    public partial class Phase0_AddProcurementOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementOrders",
                columns: table => new
                {
                    ProcurementOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PoNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalOrderValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalInvoicedToDepartment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPaidByDepartment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPaidToSuppliers = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementOrders", x => x.ProcurementOrderId);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementOrderSchools",
                columns: table => new
                {
                    ProcurementOrderSchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SchoolSubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementOrderSchools", x => x.ProcurementOrderSchoolId);
                    table.ForeignKey(
                        name: "FK_ProcurementOrderSchools_ProcurementOrders_ProcurementOrderId",
                        column: x => x.ProcurementOrderId,
                        principalTable: "ProcurementOrders",
                        principalColumn: "ProcurementOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementOrderItems",
                columns: table => new
                {
                    ProcurementOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcurementOrderSchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QtyOrdered = table.Column<int>(type: "int", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeliveryStatus = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementOrderItems", x => x.ProcurementOrderItemId);
                    table.ForeignKey(
                        name: "FK_ProcurementOrderItems_ProcurementOrderSchools_ProcurementOrderSchoolId",
                        column: x => x.ProcurementOrderSchoolId,
                        principalTable: "ProcurementOrderSchools",
                        principalColumn: "ProcurementOrderSchoolId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementOrderItems_ProcurementOrderSchoolId",
                table: "ProcurementOrderItems",
                column: "ProcurementOrderSchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementOrders_PoNumber",
                table: "ProcurementOrders",
                column: "PoNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementOrderSchools_ProcurementOrderId",
                table: "ProcurementOrderSchools",
                column: "ProcurementOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementOrderItems");

            migrationBuilder.DropTable(
                name: "ProcurementOrderSchools");

            migrationBuilder.DropTable(
                name: "ProcurementOrders");
        }
    }
}
