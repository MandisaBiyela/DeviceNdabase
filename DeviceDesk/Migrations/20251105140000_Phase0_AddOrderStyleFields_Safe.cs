using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceDesk.netcore.Migrations
{
    /// <summary>
    /// Safe Phase 0 migration: adds order-style fields to Devices and DeviceImportBatch,
    /// and conditionally renames legacy Batches table to DeviceImportBatch if present.
    /// Uses idempotent SQL so it works on fresh or existing databases.
    /// </summary>
    public partial class Phase0_AddOrderStyleFields_Safe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Ensure Devices has order-style columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'DeviceType')
BEGIN
    ALTER TABLE [dbo].[Devices] ADD [DeviceType] NVARCHAR(50) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'Description')
BEGIN
    ALTER TABLE [dbo].[Devices] ADD [Description] NVARCHAR(500) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'OrderNumber')
BEGIN
    ALTER TABLE [dbo].[Devices] ADD [OrderNumber] NVARCHAR(50) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Devices_OrderNumber' AND object_id = OBJECT_ID(N'[dbo].[Devices]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Devices_OrderNumber]
    ON [dbo].[Devices] ([OrderNumber])
    WHERE [OrderNumber] IS NOT NULL;
END

-- If legacy table exists, rename it to match EF mapping
IF OBJECT_ID(N'[dbo].[Batches]', 'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[DeviceImportBatch]', 'U') IS NULL
BEGIN
    EXEC sp_rename 'dbo.Batches', 'DeviceImportBatch';
END

-- Ensure DeviceImportBatch has OrderNumber and index
IF OBJECT_ID(N'[dbo].[DeviceImportBatch]', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DeviceImportBatch]') AND name = 'OrderNumber')
    BEGIN
        ALTER TABLE [dbo].[DeviceImportBatch] ADD [OrderNumber] NVARCHAR(50) NULL;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeviceImportBatch_OrderNumber' AND object_id = OBJECT_ID(N'[dbo].[DeviceImportBatch]'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_DeviceImportBatch_OrderNumber]
        ON [dbo].[DeviceImportBatch] ([OrderNumber])
        WHERE [OrderNumber] IS NOT NULL;
    END
END

-- No-op guards complete
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally leave Down empty for safety; removing these columns could break Phase 0 uploads.
        }
    }
}