-- =============================================
-- Backfill SchoolId and SchoolName in Phase2Devices
-- =============================================
-- This script updates Phase2Devices table with school information
-- from the Devices table and Schools table.

-- Step 1: Backfill SchoolId and SchoolName from Devices table
-- Phase2Devices.SchoolId is INT, Devices.SchoolId is BIGINT, so we cast
PRINT 'Step 1: Backfilling SchoolId and SchoolName from Devices table...';

UPDATE p2
SET p2.SchoolId = CAST(d.SchoolId AS int),
    p2.SchoolName = d.SchoolName
FROM Phase2Devices p2
INNER JOIN Devices d ON p2.Serial = d.SerialNumber
WHERE p2.SchoolId IS NULL
  AND d.SchoolId IS NOT NULL;

PRINT 'Step 1 complete.';
GO

-- Step 2: Backfill SchoolName from Schools table where SchoolId exists but SchoolName is missing
PRINT 'Step 2: Backfilling SchoolName from Schools table...';

UPDATE p2
SET p2.SchoolName = s.Name
FROM Phase2Devices p2
INNER JOIN Schools s ON p2.SchoolId = s.SchoolId
WHERE p2.SchoolId IS NOT NULL
  AND (p2.SchoolName IS NULL OR p2.SchoolName = '');

PRINT 'Step 2 complete.';
GO

-- Step 3: Report results
PRINT 'Backfill Summary:';
SELECT 
    COUNT(*) as TotalDevices,
    COUNT(SchoolId) as DevicesWithSchoolId,
    COUNT(SchoolName) as DevicesWithSchoolName,
    COUNT(*) - COUNT(SchoolId) as DevicesWithoutSchoolId
FROM Phase2Devices;

-- Step 4: Verify EKUZAMENI devices specifically
PRINT 'EKUZAMENI Devices Verification:';
SELECT Serial, SchoolId, SchoolName, Stage
FROM Phase2Devices  
WHERE Serial IN ('340YB0FGC2000Q', '340YBMMGCD1000Q')
ORDER BY Serial;
GO

