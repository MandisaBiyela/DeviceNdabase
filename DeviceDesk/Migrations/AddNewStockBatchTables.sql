-- Migration: Add New Stock Batch Tables
-- Date: 2025-01-15
-- Description: Creates tables for Phase 0 → Phase 1 blind copy workflow

-- =============================================
-- 1. NewStockBatches Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NewStockBatches')
BEGIN
    CREATE TABLE NewStockBatches (
        BatchId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        BatchNumber NVARCHAR(50) NOT NULL,
        SupplierName NVARCHAR(200) NULL,
        InvoiceNumber NVARCHAR(100) NULL,
        ExpectedDeliveryDate DATETIME NULL,
        TotalQuantityExpected INT NOT NULL DEFAULT 0,
        TotalQuantityScanned INT NOT NULL DEFAULT 0,
        Status INT NOT NULL DEFAULT 0,
        CreatedBy NVARCHAR(100) NOT NULL,
        ConfirmedBy NVARCHAR(100) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
        ConfirmedAt DATETIME NULL,
        Notes NVARCHAR(MAX) NULL,
        GRVNumber NVARCHAR(50) NULL
    );

    -- Indexes
    CREATE UNIQUE INDEX IX_NewStockBatches_BatchNumber ON NewStockBatches(BatchNumber);
    CREATE INDEX IX_NewStockBatches_Status ON NewStockBatches(Status);
    CREATE INDEX IX_NewStockBatches_CreatedAt ON NewStockBatches(CreatedAt);

    PRINT 'Created table: NewStockBatches';
END
ELSE
BEGIN
    PRINT 'Table NewStockBatches already exists';
END
GO

-- =============================================
-- 2. NewStockBatchItems Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NewStockBatchItems')
BEGIN
    CREATE TABLE NewStockBatchItems (
        ItemId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        BatchId UNIQUEIDENTIFIER NOT NULL,
        Brand NVARCHAR(100) NULL,
        Model NVARCHAR(100) NULL,
        DeviceType NVARCHAR(50) NULL,
        Description NVARCHAR(500) NULL,
        QuantityExpected INT NOT NULL DEFAULT 0,
        QuantityScanned INT NOT NULL DEFAULT 0,
        Zone NVARCHAR(50) NOT NULL DEFAULT 'New Stock',
        
        CONSTRAINT FK_NewStockBatchItems_Batch 
            FOREIGN KEY (BatchId) REFERENCES NewStockBatches(BatchId) 
            ON DELETE CASCADE
    );

    -- Indexes
    CREATE INDEX IX_NewStockBatchItems_BatchId ON NewStockBatchItems(BatchId);

    PRINT 'Created table: NewStockBatchItems';
END
ELSE
BEGIN
    PRINT 'Table NewStockBatchItems already exists';
END
GO

-- =============================================
-- 3. NewStockScannedDevices Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NewStockScannedDevices')
BEGIN
    CREATE TABLE NewStockScannedDevices (
        ScanId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        BatchId UNIQUEIDENTIFIER NOT NULL,
        SerialNumber NVARCHAR(100) NOT NULL,
        IMEI NVARCHAR(50) NULL,
        Brand NVARCHAR(100) NULL,
        Model NVARCHAR(100) NULL,
        ScannedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
        ScannedBy NVARCHAR(100) NOT NULL,
        IsDuplicate BIT NOT NULL DEFAULT 0,
        Notes NVARCHAR(MAX) NULL,
        
        CONSTRAINT FK_NewStockScannedDevices_Batch 
            FOREIGN KEY (BatchId) REFERENCES NewStockBatches(BatchId) 
            ON DELETE CASCADE
    );

    -- Indexes
    CREATE INDEX IX_NewStockScannedDevices_BatchId ON NewStockScannedDevices(BatchId);
    CREATE UNIQUE INDEX IX_NewStockScannedDevices_SerialNumber ON NewStockScannedDevices(SerialNumber);
    CREATE INDEX IX_NewStockScannedDevices_BatchId_SerialNumber ON NewStockScannedDevices(BatchId, SerialNumber);

    PRINT 'Created table: NewStockScannedDevices';
END
ELSE
BEGIN
    PRINT 'Table NewStockScannedDevices already exists';
END
GO

PRINT 'Migration completed successfully!';
PRINT '';
PRINT 'Status Enum Values:';
PRINT '  0 = PendingScan';
PRINT '  1 = Scanning';
PRINT '  2 = ReadyToConfirm';
PRINT '  3 = Mismatch';
PRINT '  4 = Completed';
PRINT '  5 = Cancelled';
GO
