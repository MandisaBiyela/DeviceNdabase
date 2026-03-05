using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations.DeviceDeskDb
{
    /// <inheritdoc />
    public partial class EnsureOrderModelListsExists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safety migration: re-create Phase 0 model-driven scanning tables
            // if they were dropped or never created on this database.
            // This uses raw SQL with IF NOT EXISTS guards so it is idempotent.

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OrderModelLists]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderModelLists](
        [ModelID] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_OrderModelLists] PRIMARY KEY,
        [OrderID] UNIQUEIDENTIFIER NOT NULL,
        [ModelName] NVARCHAR(200) NOT NULL,
        [ExpectedQty] INT NOT NULL CONSTRAINT [DF_OrderModelLists_ExpectedQty] DEFAULT(0),
        [CountedQty] INT NOT NULL CONSTRAINT [DF_OrderModelLists_CountedQty] DEFAULT(0),
        [Status] NVARCHAR(20) NOT NULL
    );

    ALTER TABLE [dbo].[OrderModelLists]  WITH CHECK ADD  CONSTRAINT [FK_OrderModelLists_NewStockBatches_OrderID]
        FOREIGN KEY([OrderID]) REFERENCES [dbo].[NewStockBatches] ([BatchId]) ON DELETE CASCADE;

    CREATE INDEX [IX_OrderModelLists_OrderID] ON [dbo].[OrderModelLists]([OrderID]);
END
");

            // Also ensure ScannedSerials exists, since it is tightly coupled to OrderModelLists.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[ScannedSerials]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ScannedSerials](
        [SerialID] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_ScannedSerials] PRIMARY KEY,
        [OrderID] UNIQUEIDENTIFIER NOT NULL,
        [ModelID] UNIQUEIDENTIFIER NOT NULL,
        [DeviceSerial] NVARCHAR(200) NOT NULL,
        [Timestamp] DATETIME NOT NULL CONSTRAINT [DF_ScannedSerials_Timestamp] DEFAULT(GETUTCDATE())
    );

    ALTER TABLE [dbo].[ScannedSerials]  WITH CHECK ADD  CONSTRAINT [FK_ScannedSerials_NewStockBatches_OrderID]
        FOREIGN KEY([OrderID]) REFERENCES [dbo].[NewStockBatches] ([BatchId]) ON DELETE CASCADE;

    ALTER TABLE [dbo].[ScannedSerials]  WITH CHECK ADD  CONSTRAINT [FK_ScannedSerials_OrderModelLists_ModelID]
        FOREIGN KEY([ModelID]) REFERENCES [dbo].[OrderModelLists] ([ModelID]);

    CREATE INDEX [IX_ScannedSerials_OrderID] ON [dbo].[ScannedSerials]([OrderID]);
    CREATE INDEX [IX_ScannedSerials_ModelID] ON [dbo].[ScannedSerials]([ModelID]);
    CREATE UNIQUE INDEX [IX_ScannedSerials_DeviceSerial] ON [dbo].[ScannedSerials]([DeviceSerial]);
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op on Down() to avoid deleting data; this migration is a safety net.
        }
    }
}
