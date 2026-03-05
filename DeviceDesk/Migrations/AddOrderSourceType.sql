-- Migration: Add SourceType to Orders Table
-- Date: 2025-11-05
-- Description: Adds SourceType column to Orders table to distinguish Phase 0 NEW stock orders from RnR orders

-- Add SourceType column to Orders table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Orders]') AND name = 'SourceType')
BEGIN
    ALTER TABLE [dbo].[Orders]
    ADD [SourceType] INT NOT NULL DEFAULT 3; -- Default to 'Other' (3)
    PRINT 'Added SourceType column to Orders table';
END
ELSE
BEGIN
    PRINT 'SourceType column already exists in Orders table';
END
GO

-- Create index on SourceType for better query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Orders_SourceType' AND object_id = OBJECT_ID(N'[dbo].[Orders]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Orders_SourceType]
    ON [dbo].[Orders] ([SourceType]);
    PRINT 'Created index IX_Orders_SourceType';
END
ELSE
BEGIN
    PRINT 'Index IX_Orders_SourceType already exists';
END
GO

PRINT 'Migration completed successfully!';
PRINT 'SourceType values: 1=NewStock (Phase 0 Upload), 2=RnR, 3=Other';
GO
