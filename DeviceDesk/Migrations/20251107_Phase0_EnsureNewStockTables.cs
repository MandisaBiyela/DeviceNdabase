using Microsoft.EntityFrameworkCore.Migrations;
using DeviceDesk.Infrastructure.Data;

namespace DeviceDesk.netcore.Migrations
{
     using Microsoft.EntityFrameworkCore;
    [Migration("20251107120000_Phase0_EnsureNewStockTables")]
    public partial class Phase0_EnsureNewStockTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) NewStockBatches
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NewStockBatches')
BEGIN
    CREATE TABLE [dbo].[NewStockBatches] (
        [BatchId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [BatchNumber] NVARCHAR(50) NOT NULL,
        [SupplierName] NVARCHAR(200) NULL,
        [InvoiceNumber] NVARCHAR(100) NULL,
        [ExpectedDeliveryDate] DATETIME NULL,
        [TotalQuantityExpected] INT NOT NULL DEFAULT 0,
        [TotalQuantityScanned] INT NOT NULL DEFAULT 0,
        [Status] INT NOT NULL DEFAULT 0,
        [CreatedBy] NVARCHAR(100) NOT NULL,
        [ConfirmedBy] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME NOT NULL DEFAULT GETUTCDATE(),
        [ConfirmedAt] DATETIME NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [GRVNumber] NVARCHAR(50) NULL
    );

    CREATE UNIQUE INDEX [IX_NewStockBatches_BatchNumber] ON [dbo].[NewStockBatches]([BatchNumber]);
    CREATE INDEX [IX_NewStockBatches_Status] ON [dbo].[NewStockBatches]([Status]);
    CREATE INDEX [IX_NewStockBatches_CreatedAt] ON [dbo].[NewStockBatches]([CreatedAt]);
END





            ");

            // 2) NewStockBatchItems
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NewStockBatchItems')
BEGIN
    CREATE TABLE [dbo].[NewStockBatchItems] (
        [ItemId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [BatchId] UNIQUEIDENTIFIER NOT NULL,
        [Brand] NVARCHAR(100) NULL,
        [Model] NVARCHAR(100) NULL,
        [DeviceType] NVARCHAR(50) NULL,
        [Description] NVARCHAR(500) NULL,
        [QuantityExpected] INT NOT NULL DEFAULT 0,
        [QuantityScanned] INT NOT NULL DEFAULT 0,
        [Zone] NVARCHAR(50) NOT NULL DEFAULT 'New Stock',
        CONSTRAINT [FK_NewStockBatchItems_Batch] FOREIGN KEY ([BatchId]) REFERENCES [dbo].[NewStockBatches]([BatchId]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_NewStockBatchItems_BatchId] ON [dbo].[NewStockBatchItems]([BatchId]);
END




            ");

            // 3) NewStockScannedDevices
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NewStockScannedDevices')
BEGIN
    CREATE TABLE [dbo].[NewStockScannedDevices] (
        [ScanId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [BatchId] UNIQUEIDENTIFIER NOT NULL,
        [SerialNumber] NVARCHAR(100) NOT NULL,
        [IMEI] NVARCHAR(50) NULL,
        [Brand] NVARCHAR(100) NULL,
        [Model] NVARCHAR(100) NULL,
        [ScannedAt] DATETIME NOT NULL,
        [ScannedBy] NVARCHAR(100) NOT NULL,
        [IsDuplicate] BIT NOT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        CONSTRAINT [FK_NewStockScannedDevices_Batch] FOREIGN KEY ([BatchId]) REFERENCES [dbo].[NewStockBatches]([BatchId]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_NewStockScannedDevices_BatchId] ON [dbo].[NewStockScannedDevices]([BatchId]);
    CREATE UNIQUE INDEX [IX_NewStockScannedDevices_SerialNumber] ON [dbo].[NewStockScannedDevices]([SerialNumber]);
    CREATE INDEX [IX_NewStockScannedDevices_BatchId_SerialNumber] ON [dbo].[NewStockScannedDevices]([BatchId], [SerialNumber]);
END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.NewStockScannedDevices','U') IS NOT NULL
    DROP TABLE [dbo].[NewStockScannedDevices];
IF OBJECT_ID('dbo.NewStockBatchItems','U') IS NOT NULL
    DROP TABLE [dbo].[NewStockBatchItems];
IF OBJECT_ID('dbo.NewStockBatches','U') IS NOT NULL
    DROP TABLE [dbo].[NewStockBatches];
            ");
        }
    }
}