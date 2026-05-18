-- Check SchoolId mapping for EKUZAMENI devices
SELECT 
    p2.Serial, 
    p2.SchoolId AS Phase2SchoolId, 
    p2.SchoolName AS Phase2SchoolName,
    d.SchoolId AS CoreSchoolId,
    d.SchoolName AS CoreSchoolName,
    s.SchoolId AS ActualSchoolId, 
    s.Name AS ActualSchoolName,
    p2.Stage AS Phase2Stage
FROM Phase2Devices p2
LEFT JOIN Devices d ON p2.Serial = d.SerialNumber
LEFT JOIN Schools s ON COALESCE(p2.SchoolId, d.SchoolId) = s.SchoolId
WHERE p2.Serial IN ('340YB0FGC2000Q', '340YBMMGCD1000Q')
ORDER BY p2.Serial;

-- Also check all devices to see school name patterns
SELECT 
    COUNT(*) AS TotalDevices,
    COUNT(p2.SchoolId) AS DevicesWithSchoolId,
    COUNT(p2.SchoolName) AS DevicesWithSchoolName
FROM Phase2Devices p2;

