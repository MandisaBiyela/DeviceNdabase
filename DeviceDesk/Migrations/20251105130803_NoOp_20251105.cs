using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations
{
    /// <inheritdoc />
    public partial class NoOp_20251105 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safely rename legacy Batches table to DeviceImportBatch if present
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[Batches]', 'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[DeviceImportBatch]', 'U') IS NULL
BEGIN
    -- Drop legacy primary key if exists
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'PK_Batches' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
    BEGIN
        ALTER TABLE [dbo].[Batches] DROP CONSTRAINT [PK_Batches];
    END

    -- Rename table to match EF Core mapping
    EXEC sp_rename 'dbo.Batches', 'DeviceImportBatch';

    -- Rename index to new naming convention if present on the renamed table
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Batches_Source_SchoolId_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[DeviceImportBatch]'))
    BEGIN
        EXEC sp_rename N'[dbo].[DeviceImportBatch].[IX_Batches_Source_SchoolId_CreatedAt]', N'IX_DeviceImportBatch_Source_SchoolId_CreatedAt', N'INDEX';
    END

    -- Recreate primary key on DeviceImportBatch
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'PK_DeviceImportBatch' AND object_id = OBJECT_ID(N'[dbo].[DeviceImportBatch]'))
    BEGIN
        ALTER TABLE [dbo].[DeviceImportBatch] ADD CONSTRAINT [PK_DeviceImportBatch] PRIMARY KEY ([BatchId]);
    END
END

-- Ensure Devices has required order-style columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'Description')
BEGIN
    ALTER TABLE [dbo].[Devices] ADD [Description] NVARCHAR(MAX) NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'DeviceType')
BEGIN
    ALTER TABLE [dbo].[Devices] ADD [DeviceType] NVARCHAR(MAX) NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'OrderNumber')
BEGIN
    ALTER TABLE [dbo].[Devices] ADD [OrderNumber] NVARCHAR(MAX) NULL;
END

-- Ensure DeviceImportBatch has OrderNumber column
IF OBJECT_ID(N'[dbo].[DeviceImportBatch]', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DeviceImportBatch]') AND name = 'OrderNumber')
BEGIN
    ALTER TABLE [dbo].[DeviceImportBatch] ADD [OrderNumber] NVARCHAR(MAX) NULL;
END
");

            migrationBuilder.CreateTable(
                name: "NewStockBatches",
                columns: table => new
                {
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalQuantityExpected = table.Column<int>(type: "int", nullable: false),
                    TotalQuantityScanned = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GRVNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewStockBatches", x => x.BatchId);
                });

            migrationBuilder.CreateTable(
                name: "NewStockBatchItems",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QuantityExpected = table.Column<int>(type: "int", nullable: false),
                    QuantityScanned = table.Column<int>(type: "int", nullable: false),
                    Zone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewStockBatchItems", x => x.ItemId);
                    table.ForeignKey(
                        name: "FK_NewStockBatchItems_NewStockBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "NewStockBatches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewStockScannedDevices",
                columns: table => new
                {
                    ScanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IMEI = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScannedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewStockScannedDevices", x => x.ScanId);
                    table.ForeignKey(
                        name: "FK_NewStockScannedDevices_NewStockBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "NewStockBatches",
                        principalColumn: "BatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewStockBatches_BatchNumber",
                table: "NewStockBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewStockBatches_CreatedAt",
                table: "NewStockBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NewStockBatches_Status",
                table: "NewStockBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NewStockBatchItems_BatchId",
                table: "NewStockBatchItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_NewStockScannedDevices_BatchId",
                table: "NewStockScannedDevices",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_NewStockScannedDevices_BatchId_SerialNumber",
                table: "NewStockScannedDevices",
                columns: new[] { "BatchId", "SerialNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_NewStockScannedDevices_SerialNumber",
                table: "NewStockScannedDevices",
                column: "SerialNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewStockBatchItems");

            migrationBuilder.DropTable(
                name: "NewStockScannedDevices");

            migrationBuilder.DropTable(
                name: "NewStockBatches");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DeviceImportBatch",
                table: "DeviceImportBatch");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "DeviceImportBatch");

            migrationBuilder.RenameTable(
                name: "DeviceImportBatch",
                newName: "Batches");

            migrationBuilder.RenameIndex(
                name: "IX_DeviceImportBatch_Source_SchoolId_CreatedAt",
                table: "Batches",
                newName: "IX_Batches_Source_SchoolId_CreatedAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Batches",
                table: "Batches",
                column: "BatchId");
        }
    }
}
