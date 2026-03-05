using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.Phase1Db
{
    /// <inheritdoc />
    public partial class Phase1_InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollectionSlips",
                columns: table => new
                {
                    CollectionSlipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlipNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SchoolId = table.Column<long>(type: "bigint", nullable: false),
                    EmisCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SchoolName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    CollectionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CollectedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionSlips", x => x.CollectionSlipId);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OrderDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeliveryDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.OrderId);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                columns: table => new
                {
                    OrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    QuantityOrdered = table.Column<int>(type: "int", nullable: false),
                    QuantityReceived = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.OrderLineId);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivingBatches",
                columns: table => new
                {
                    ReceivingBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CollectionSlipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SchoolId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReceivedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ScanningOfficer = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReceivedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScanningStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScanningCompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpectedCount = table.Column<int>(type: "int", nullable: false),
                    ActualCount = table.Column<int>(type: "int", nullable: false),
                    VarianceCount = table.Column<int>(type: "int", nullable: false),
                    HasVariance = table.Column<bool>(type: "bit", nullable: false),
                    VarianceReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    VarianceResolution = table.Column<int>(type: "int", nullable: true),
                    SupervisorApprovedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SupervisorApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivingBatches", x => x.ReceivingBatchId);
                    table.ForeignKey(
                        name: "FK_ReceivingBatches_CollectionSlips_CollectionSlipId",
                        column: x => x.CollectionSlipId,
                        principalTable: "CollectionSlips",
                        principalColumn: "CollectionSlipId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivingBatches_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceivedNotes",
                columns: table => new
                {
                    GRVId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivingBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GRVNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GRVDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrderNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false),
                    ReceivedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    PdfData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceivedNotes", x => x.GRVId);
                    table.ForeignKey(
                        name: "FK_GoodsReceivedNotes_ReceivingBatches_ReceivingBatchId",
                        column: x => x.ReceivingBatchId,
                        principalTable: "ReceivingBatches",
                        principalColumn: "ReceivingBatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivingBatchItems",
                columns: table => new
                {
                    ReceivingBatchItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivingBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IMEI = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivingBatchItems", x => x.ReceivingBatchItemId);
                    table.ForeignKey(
                        name: "FK_ReceivingBatchItems_ReceivingBatches_ReceivingBatchId",
                        column: x => x.ReceivingBatchId,
                        principalTable: "ReceivingBatches",
                        principalColumn: "ReceivingBatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionSlips_SchoolId",
                table: "CollectionSlips",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionSlips_SlipNumber",
                table: "CollectionSlips",
                column: "SlipNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceivedNotes_GRVNumber",
                table: "GoodsReceivedNotes",
                column: "GRVNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceivedNotes_ReceivingBatchId",
                table: "GoodsReceivedNotes",
                column: "ReceivingBatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingBatches_CollectionSlipId",
                table: "ReceivingBatches",
                column: "CollectionSlipId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingBatches_OrderId",
                table: "ReceivingBatches",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingBatches_SchoolId",
                table: "ReceivingBatches",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingBatchItems_IMEI",
                table: "ReceivingBatchItems",
                column: "IMEI",
                filter: "[IMEI] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingBatchItems_ReceivingBatchId",
                table: "ReceivingBatchItems",
                column: "ReceivingBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivingBatchItems_SerialNumber",
                table: "ReceivingBatchItems",
                column: "SerialNumber",
                filter: "[SerialNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoodsReceivedNotes");

            migrationBuilder.DropTable(
                name: "OrderLines");

            migrationBuilder.DropTable(
                name: "ReceivingBatchItems");

            migrationBuilder.DropTable(
                name: "ReceivingBatches");

            migrationBuilder.DropTable(
                name: "CollectionSlips");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
