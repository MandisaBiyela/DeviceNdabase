-- Backfill SchoolName in Phase2Devices from Schools table via SchoolId
-- This is needed because the core Devices table also has NULL school names

-- Step 1: Update Phase2Devices with school names from Schools table
UPDATE p2
SET p2.SchoolName = s.Name
FROM [Phase2Devices] p2
INNER JOIN [Schools] s ON p2.SchoolId = s.SchoolId
WHERE (p2.SchoolName IS NULL OR p2.SchoolName = '')
  AND s.Name IS NOT NULL
  AND s.Name != '';

-- Step 2: Show how many devices were updated
SELECT COUNT(*) AS DevicesUpdated
FROM [Phase2Devices]
WHERE SchoolName IS NOT NULL AND SchoolName != '';

-- Step 3: Show devices that still don't have school names
SELECT TOP 20
    p2.Serial,
    p2.SchoolId,
    p2.SchoolName,
    s.Name AS SchoolNameFromSchools,
    s.EmisCode
FROM [Phase2Devices] p2
LEFT JOIN [Schools] s ON p2.SchoolId = s.SchoolId
WHERE p2.SchoolName IS NULL OR p2.SchoolName = ''
ORDER BY p2.Serial;


