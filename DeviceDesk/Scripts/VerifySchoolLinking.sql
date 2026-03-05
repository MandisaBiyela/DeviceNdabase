-- Verification query to check if school information is properly linked
-- Run this after applying the migration and backfill

-- Check RnrExpectedItems
SELECT 
    'RnrExpectedItems' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(SchoolId) as RecordsWithSchoolId,
    COUNT(*) - COUNT(SchoolId) as RecordsWithoutSchoolId,
    CAST(COUNT(SchoolId) * 100.0 / NULLIF(COUNT(*), 0) AS DECIMAL(5,2)) as PercentageWithSchool
FROM RnrExpectedItems;

-- Check ReceivingBatchScans
SELECT 
    'ReceivingBatchScans' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(SchoolId) as RecordsWithSchoolId,
    COUNT(*) - COUNT(SchoolId) as RecordsWithoutSchoolId,
    CAST(COUNT(SchoolId) * 100.0 / NULLIF(COUNT(*), 0) AS DECIMAL(5,2)) as PercentageWithSchool
FROM ReceivingBatchScans;

-- Detailed view: Check if school info flows correctly from batch to items/scans
SELECT 
    rb.ReceivingBatchId,
    cs.SchoolName as BatchSchool,
    cs.SchoolId as BatchSchoolId,
    COUNT(DISTINCT ei.RnrExpectedItemId) as ExpectedItems,
    COUNT(DISTINCT CASE WHEN ei.SchoolId IS NOT NULL THEN ei.RnrExpectedItemId END) as ItemsWithSchool,
    COUNT(DISTINCT s.ReceivingBatchScanId) as ScannedItems,
    COUNT(DISTINCT CASE WHEN s.SchoolId IS NOT NULL THEN s.ReceivingBatchScanId END) as ScansWithSchool
FROM ReceivingBatches rb
LEFT JOIN CollectionSlips cs ON rb.CollectionSlipId = cs.CollectionSlipId
LEFT JOIN RnrExpectedItems ei ON rb.ReceivingBatchId = ei.BatchId
LEFT JOIN ReceivingBatchScans s ON rb.ReceivingBatchId = s.BatchId
WHERE rb.SourceType IN (2, 3) -- RnrNormal, RnrEmergency
GROUP BY rb.ReceivingBatchId, cs.SchoolName, cs.SchoolId
ORDER BY rb.ReceivingBatchId DESC;

-- Sample data: Show a specific batch's school linking
SELECT TOP 10
    ei.Serial as ExpectedSerial,
    ei.SchoolId as ExpectedSchoolId,
    s.Serial as ScannedSerial, 
    s.SchoolId as ScanSchoolId,
    cs.SchoolName as BatchSchool,
    s.ScannedAt
FROM ReceivingBatches rb
JOIN CollectionSlips cs ON rb.CollectionSlipId = cs.CollectionSlipId
LEFT JOIN RnrExpectedItems ei ON rb.ReceivingBatchId = ei.BatchId
LEFT JOIN ReceivingBatchScans s ON rb.ReceivingBatchId = s.BatchId AND ei.Serial = s.Serial
WHERE rb.SourceType IN (2, 3) -- RnrNormal, RnrEmergency
ORDER BY rb.ReceivingBatchId DESC, ei.Serial;

