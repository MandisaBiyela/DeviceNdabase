-- Backfill SchoolId for existing RnrExpectedItems and ReceivingBatchScans records
-- This script populates SchoolId from the parent ReceivingBatch for records that don't have it

-- Backfill RnrExpectedItems
UPDATE ei 
SET ei.SchoolId = rb.SchoolId
FROM RnrExpectedItems ei
INNER JOIN ReceivingBatches rb ON ei.BatchId = rb.ReceivingBatchId
WHERE ei.SchoolId IS NULL AND rb.SchoolId IS NOT NULL;

-- Also try to get from CollectionSlip if batch doesn't have SchoolId
UPDATE ei 
SET ei.SchoolId = cs.SchoolId
FROM RnrExpectedItems ei
INNER JOIN ReceivingBatches rb ON ei.BatchId = rb.ReceivingBatchId
INNER JOIN CollectionSlips cs ON rb.CollectionSlipId = cs.CollectionSlipId
WHERE ei.SchoolId IS NULL AND cs.SchoolId IS NOT NULL;

-- Backfill ReceivingBatchScans
UPDATE s 
SET s.SchoolId = rb.SchoolId
FROM ReceivingBatchScans s
INNER JOIN ReceivingBatches rb ON s.BatchId = rb.ReceivingBatchId
WHERE s.SchoolId IS NULL AND rb.SchoolId IS NOT NULL;

-- Also try to get from CollectionSlip if batch doesn't have SchoolId
UPDATE s 
SET s.SchoolId = cs.SchoolId
FROM ReceivingBatchScans s
INNER JOIN ReceivingBatches rb ON s.BatchId = rb.ReceivingBatchId
INNER JOIN CollectionSlips cs ON rb.CollectionSlipId = cs.CollectionSlipId
WHERE s.SchoolId IS NULL AND cs.SchoolId IS NOT NULL;

-- Verification query
SELECT 
    'RnrExpectedItems' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(SchoolId) as RecordsWithSchoolId,
    COUNT(*) - COUNT(SchoolId) as RecordsWithoutSchoolId
FROM RnrExpectedItems
UNION ALL
SELECT 
    'ReceivingBatchScans' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(SchoolId) as RecordsWithSchoolId,
    COUNT(*) - COUNT(SchoolId) as RecordsWithoutSchoolId
FROM ReceivingBatchScans;

