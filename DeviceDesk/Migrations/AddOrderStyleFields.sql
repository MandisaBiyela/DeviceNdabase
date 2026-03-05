-- Migration: Add Order-Style Fields to Support New Stock Uploads
-- Date: 2025-11-04
-- Description: Adds DeviceType, Description, and OrderNumber fields to Devices and Batches tables

-- Add new columns to Devices table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'DeviceType')
BEGIN
    ALTER TABLE [dbo].[Devices]
    ADD [DeviceType] NVARCHAR(50) NULL;
    PRINT 'Added DeviceType column to Devices table';
END
ELSE
BEGIN
    PRINT 'DeviceType column already exists in Devices table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'Description')
BEGIN
    ALTER TABLE [dbo].[Devices]
    ADD [Description] NVARCHAR(500) NULL;
    PRINT 'Added Description column to Devices table';
END
ELSE
BEGIN
    PRINT 'Description column already exists in Devices table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Devices]') AND name = 'OrderNumber')
BEGIN
    ALTER TABLE [dbo].[Devices]
    ADD [OrderNumber] NVARCHAR(50) NULL;
    PRINT 'Added OrderNumber column to Devices table';
END
ELSE
BEGIN
    PRINT 'OrderNumber column already exists in Devices table';
END
GO

-- Add OrderNumber column to Batches table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Batches]') AND name = 'OrderNumber')
BEGIN
    ALTER TABLE [dbo].[Batches]
    ADD [OrderNumber] NVARCHAR(50) NULL;
    PRINT 'Added OrderNumber column to Batches table';
END
ELSE
BEGIN
    PRINT 'OrderNumber column already exists in Batches table';
END
GO

-- Create index on OrderNumber for better query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Devices_OrderNumber' AND object_id = OBJECT_ID(N'[dbo].[Devices]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Devices_OrderNumber]
    ON [dbo].[Devices] ([OrderNumber])
    WHERE [OrderNumber] IS NOT NULL;
    PRINT 'Created index IX_Devices_OrderNumber';
END
ELSE
BEGIN
    PRINT 'Index IX_Devices_OrderNumber already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Batches_OrderNumber' AND object_id = OBJECT_ID(N'[dbo].[Batches]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Batches_OrderNumber]
    ON [dbo].[Batches] ([OrderNumber])
    WHERE [OrderNumber] IS NOT NULL;
    PRINT 'Created index IX_Batches_OrderNumber';
END
ELSE
BEGIN
    PRINT 'Index IX_Batches_OrderNumber already exists';
END
GO

PRINT 'Migration completed successfully!';
GO
