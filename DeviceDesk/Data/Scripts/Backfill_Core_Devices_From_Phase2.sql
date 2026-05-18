-- ============================================================================
-- Backfill Core Device Records from Phase 2 Devices
-- ============================================================================
-- Purpose: Create Core Device records for Phase 2 devices that don't have them
-- This ensures all Phase 2 devices show up in Student/Teacher Allocation
-- Run this ONCE after deploying the ReceiptingService fix
-- ============================================================================

-- Insert missing Core Device records from Phase2Devices
INSERT INTO Devices (
    SerialNumber,
    AllocationType,
    SchoolId,
    SchoolName,
    Source,
    ImportedAt
)
SELECT DISTINCT
    p2.Serial AS SerialNumber,
    0 AS AllocationType,  -- 0 = None (ready for allocation)
    p2.SchoolId,
    p2.SchoolName,
    'RNR' AS Source,
    GETUTCDATE() AS ImportedAt
FROM Phase2Devices p2
LEFT JOIN Devices d ON d.SerialNumber = p2.Serial
WHERE d.SerialNumber IS NULL  -- Only insert if Core Device doesn't exist
  AND p2.Serial IS NOT NULL
  AND p2.Serial != '';

-- Show results
SELECT 
    COUNT(*) AS DevicesBackfilled,
    MIN(ImportedAt) AS FirstImported,
    MAX(ImportedAt) AS LastImported
FROM Devices
WHERE ImportedAt >= DATEADD(SECOND, -10, GETUTCDATE());

PRINT 'Backfill complete! Core Device records created for existing Phase 2 devices.';

