-- Backfill SchoolName for Phase2Devices from core Devices table
-- Run this to populate school names for devices that don't have them

-- Update Phase2Devices with SchoolName from core Devices table
UPDATE p2
SET 
    p2.SchoolName = d.SchoolName,
    p2.SchoolId = CASE 
        WHEN p2.SchoolId IS NULL AND d.SchoolId IS NOT NULL 
        THEN CAST(d.SchoolId AS INT)
        ELSE p2.SchoolId 
    END
FROM [Phase2Devices] p2
INNER JOIN [Devices] d ON p2.Serial = d.SerialNumber
WHERE 
    (p2.SchoolName IS NULL OR p2.SchoolName = '')
    AND d.SchoolName IS NOT NULL
    AND d.SchoolName != '';

-- Show results
SELECT 
    COUNT(*) AS DevicesUpdated
FROM [Phase2Devices] p2
INNER JOIN [Devices] d ON p2.Serial = d.SerialNumber
WHERE p2.SchoolName IS NOT NULL AND p2.SchoolName != '';

-- Optional: Show devices that still don't have school names
SELECT 
    p2.Serial,
    p2.SchoolId,
    p2.SchoolName,
    d.SchoolId AS CoreSchoolId,
    d.SchoolName AS CoreSchoolName
FROM [Phase2Devices] p2
LEFT JOIN [Devices] d ON p2.Serial = d.SerialNumber
WHERE p2.SchoolName IS NULL OR p2.SchoolName = ''
ORDER BY p2.Serial;


