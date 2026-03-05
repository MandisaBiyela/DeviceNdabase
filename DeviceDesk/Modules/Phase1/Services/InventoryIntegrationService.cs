using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Services
{
    /// <summary>
    /// Integrates Phase 1 receiving with Phase 0 inventory
    /// </summary>
    public class InventoryIntegrationService
    {
        private readonly Phase1DbContext _phase1Db;
        private readonly DeviceDeskDbContext _phase0Db;

        public InventoryIntegrationService(Phase1DbContext phase1Db, DeviceDeskDbContext phase0Db)
        {
            _phase1Db = phase1Db;
            _phase0Db = phase0Db;
        }

        /// <summary>
        /// Transfer verified devices from Phase 1 to Phase 0 inventory
        /// Called after GRV is issued
        /// </summary>
        public async Task<int> TransferToInventoryAsync(Guid receivingBatchId, CancellationToken ct = default)
        {
            var batch = await _phase1Db.ReceivingBatches
                .Include(b => b.Items)
                .Include(b => b.Order)
                .Include(b => b.CollectionSlip)
                .Include(b => b.GRV)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == receivingBatchId, ct);

            if (batch == null)
                throw new InvalidOperationException("Receiving batch not found.");

            if (batch.Status != ReceivingBatchStatus.GRVIssued)
                throw new InvalidOperationException("Batch must have GRV issued before transferring to inventory.");

            if (!batch.IsLocked)
                throw new InvalidOperationException("Batch must be locked before transferring to inventory.");

            // Determine source type for Phase 0
            string phase0Source = batch.SourceType switch
            {
                ReceivingSourceType.NewStock => "NEW",
                ReceivingSourceType.RnrNormal => "RNR",
                ReceivingSourceType.RnrEmergency => "RNR",
                _ => "UNKNOWN"
            };

            // Get school ID and name (for RnR) - CRITICAL: prefer CollectionSlip over batch
            // CollectionSlip is the source of truth for school information
            long? schoolId = batch.CollectionSlip?.SchoolId ?? batch.SchoolId;
            string? schoolName = batch.CollectionSlip?.SchoolName;

            // If we have SchoolId but SchoolName is null/empty, look it up from Schools table
            if (schoolId.HasValue && string.IsNullOrEmpty(schoolName))
            {
                var school = await _phase0Db.Schools
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SchoolId == schoolId.Value, ct);
                schoolName = school?.Name;
            }

            int transferredCount = 0;

            // Handle two scenarios:
            // 1. NewStock batches: use ReceivingBatchItems
            // 2. RnR batches: use ReceivingBatchScans (Items may be empty)
            bool hasItems = batch.Items != null && batch.Items.Count > 0;
            bool isRnR = batch.SourceType == ReceivingSourceType.RnrNormal || batch.SourceType == ReceivingSourceType.RnrEmergency;

            if (hasItems && batch.Items != null)
            {
                // Process from ReceivingBatchItems (NewStock flow)
                foreach (var item in batch.Items)
                {
                    // Check if device already exists in Phase 0 (shouldn't happen due to duplicate checks)
                    bool exists = await _phase0Db.Devices.AnyAsync(d =>
                        (d.SerialNumber != null && d.SerialNumber == item.SerialNumber) ||
                        (d.IMEI != null && d.IMEI == item.IMEI), ct);

                    if (exists)
                    {
                        // Skip duplicates (log warning in production)
                        continue;
                    }

                    // Create Phase 0 device record
                    var phase0Device = new Device
                    {
                        Id = Guid.NewGuid(),
                        SerialNumber = item.SerialNumber,
                        IMEI = item.IMEI,
                        Brand = item.Brand,
                        Model = item.Model,
                        Source = phase0Source,
                        SchoolId = schoolId ?? batch.SchoolId ?? batch.CollectionSlip?.SchoolId,
                        SchoolName = schoolName ?? batch.CollectionSlip?.SchoolName,
                        ImportedAt = DateTimeOffset.UtcNow,
                        BatchId = null // Phase 0 batch is different from Phase 1 batch
                    };

                    _phase0Db.Devices.Add(phase0Device);
                    transferredCount++;
                }
            }
            else if (isRnR)
            {
                // Process from ReceivingBatchScans (RnR flow - Items may be empty)
                var scans = await _phase1Db.ReceivingBatchScans
                    .Where(s => s.BatchId == receivingBatchId && s.Status == RnrScanStatus.Matched)
                    .ToListAsync(ct);

                // Get expected items to get model info if available
                var expectedItems = await _phase1Db.RnrExpectedItems
                    .Where(e => e.BatchId == receivingBatchId)
                    .ToDictionaryAsync(e => e.Serial, e => e, StringComparer.OrdinalIgnoreCase, ct);

                foreach (var scan in scans)
                {
                    var serial = scan.Serial?.Trim();
                    if (string.IsNullOrEmpty(serial))
                        continue;

                    // Check if device already exists in Phase 0
                    bool exists = await _phase0Db.Devices.AnyAsync(d =>
                        d.SerialNumber != null && d.SerialNumber == serial, ct);

                    if (exists)
                    {
                        // Skip duplicates
                        continue;
                    }

                    // Try to get model from expected items
                    string? model = null;
                    if (expectedItems.TryGetValue(serial, out var expected))
                    {
                        model = expected.Model;
                    }

                    // Create Phase 0 device record from scan
                    var phase0Device = new Device
                    {
                        Id = Guid.NewGuid(),
                        SerialNumber = serial,
                        IMEI = null, // RnR scans don't typically have IMEI
                        Brand = null, // Not available from scans
                        Model = model,
                        Source = phase0Source,
                        SchoolId = schoolId ?? batch.SchoolId ?? batch.CollectionSlip?.SchoolId,
                        SchoolName = schoolName ?? batch.CollectionSlip?.SchoolName,
                        ImportedAt = DateTimeOffset.UtcNow,
                        BatchId = null
                    };

                    _phase0Db.Devices.Add(phase0Device);
                    transferredCount++;
                }
            }

            // Update batch status to Completed
            batch.Status = ReceivingBatchStatus.Completed;
            batch.UpdatedAt = DateTimeOffset.UtcNow;

            await _phase0Db.SaveChangesAsync(ct);
            await _phase1Db.SaveChangesAsync(ct);

            return transferredCount;
        }

        /// <summary>
        /// Check if devices from a batch already exist in Phase 0 inventory
        /// </summary>
        public async Task<List<string>> CheckDuplicatesInInventoryAsync(Guid receivingBatchId, CancellationToken ct = default)
        {
            var batch = await _phase1Db.ReceivingBatches
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == receivingBatchId, ct);

            if (batch == null)
                return new List<string>();

            var duplicates = new List<string>();

            foreach (var item in batch.Items)
            {
                var identifier = item.SerialNumber ?? item.IMEI;
                if (string.IsNullOrEmpty(identifier))
                    continue;

                bool exists = await _phase0Db.Devices.AnyAsync(d =>
                    (d.SerialNumber != null && d.SerialNumber == identifier) ||
                    (d.IMEI != null && d.IMEI == identifier), ct);

                if (exists)
                {
                    duplicates.Add(identifier);
                }
            }

            return duplicates;
        }

        /// <summary>
        /// Get inventory statistics
        /// </summary>
        public async Task<InventoryStatsDto> GetInventoryStatsAsync(CancellationToken ct = default)
        {
            var totalDevices = await _phase0Db.Devices.CountAsync(ct);
            var newStockCount = await _phase0Db.Devices.CountAsync(d => d.Source == "NEW", ct);
            var rnrCount = await _phase0Db.Devices.CountAsync(d => d.Source == "RNR", ct);

            var phase1PendingCount = await _phase1Db.ReceivingBatches
                .CountAsync(b => b.Status != ReceivingBatchStatus.Completed && b.Status != ReceivingBatchStatus.Cancelled, ct);

            var phase1CompletedCount = await _phase1Db.ReceivingBatches
                .CountAsync(b => b.Status == ReceivingBatchStatus.Completed, ct);

            return new InventoryStatsDto(
                totalDevices,
                newStockCount,
                rnrCount,
                phase1PendingCount,
                phase1CompletedCount
            );
        }
    }

    public record InventoryStatsDto(
        int TotalDevices,
        int NewStockCount,
        int RnrCount,
        int Phase1PendingBatches,
        int Phase1CompletedBatches
    );
}
