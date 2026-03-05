-- Add DeviceName column to RnrExpectedItems table
-- Run this script in SQL Server Management Studio on DeviceDeskDB2 database

USE DeviceDeskDB2;
GO

-- Check if column already exists
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('RnrExpectedItems') 
    AND name = 'DeviceName'
)
BEGIN
    -- Add DeviceName column
    ALTER TABLE RnrExpectedItems 
    ADD DeviceName NVARCHAR(255) NULL;
    
    PRINT 'DeviceName column added successfully to RnrExpectedItems table.';
END
ELSE
BEGIN
    PRINT 'DeviceName column already exists in RnrExpectedItems table.';
END
GO

-- Verify the column was added
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'RnrExpectedItems'
AND COLUMN_NAME = 'DeviceName';
GO

