-- Migration: Add Order-Style Fields to Support New Stock Uploads (v2)
-- Date: 2025-11-04
-- Description: Adds OrderNumber field to DeviceImportBatch table

-- Add OrderNumber column to DeviceImportBatch table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DeviceImportBatch]') AND name = 'OrderNumber')
BEGIN
    ALTER TABLE [dbo].[DeviceImportBatch]
    ADD [OrderNumber] NVARCHAR(50) NULL;
    PRINT 'Added OrderNumber column to DeviceImportBatch table';
END
ELSE
BEGIN
    PRINT 'OrderNumber column already exists in DeviceImportBatch table';
END
GO

-- Create index on OrderNumber for better query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeviceImportBatch_OrderNumber' AND object_id = OBJECT_ID(N'[dbo].[DeviceImportBatch]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DeviceImportBatch_OrderNumber]
    ON [dbo].[DeviceImportBatch] ([OrderNumber])
    WHERE [OrderNumber] IS NOT NULL;
    PRINT 'Created index IX_DeviceImportBatch_OrderNumber';
END
ELSE
BEGIN
    PRINT 'Index IX_DeviceImportBatch_OrderNumber already exists';
END
GO

PRINT 'Migration v2 completed successfully!';
GO
