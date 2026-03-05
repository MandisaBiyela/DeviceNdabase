using DeviceDesk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeviceDesk.Modules.Phase0.Services
{
    public class RnrBatchService
    {
        private readonly DeviceDeskDbContext _db;
        private readonly ILogger<RnrBatchService> _logger;

        public RnrBatchService(DeviceDeskDbContext db, ILogger<RnrBatchService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Get R&R batches available for Phase 1 processing
        /// </summary>
        public async Task<List<RnrBatchDto>> GetBatchesAsync(RnrBatchStatus? status = null, CancellationToken ct = default)
        {
            try
            {
                var query = _db.RnrBatches.AsQueryable();

                if (status.HasValue)
                {
                    query = query.Where(b => b.Status == status.Value);
                }

                var batches = await query
                    .OrderByDescending(b => b.CreatedAt)
                    .Select(b => new RnrBatchDto(
                        b.BatchId,
                        b.BatchNumber,
                        b.CollectionSlipNumber,
                        b.SchoolName,
                        b.TotalQuantityExpected,
                        b.TotalQuantityScanned,
                        b.Status,
                        b.CreatedAt
                    ))
                    .ToListAsync(ct);

                _logger.LogInformation("[R&R Batch] Retrieved {Count} batches", batches.Count);
                return batches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[R&R Batch] Error retrieving batches");
                throw;
            }
        }

        /// <summary>
        /// Get specific R&R batch details with items
        /// </summary>
        public async Task<RnrBatchDetailsDto?> GetBatchDetailsAsync(Guid batchId, CancellationToken ct = default)
        {
            try
            {
                var batch = await _db.RnrBatches
                    .Where(b => b.BatchId == batchId)
                    .Select(b => new
                    {
                        b.BatchId,
                        b.BatchNumber,
                        b.CollectionSlipNumber,
                        b.SchoolId,
                        b.SchoolName,
                        b.TotalQuantityExpected,
                        b.TotalQuantityScanned,
                        b.Status,
                        b.CreatedBy,
                        b.CreatedAt,
                        b.ConfirmedBy,
                        b.ConfirmedAt,
                        b.GRVNumber
                    })
                    .FirstOrDefaultAsync(ct);

                if (batch == null)
                    return null;

                var items = await _db.RnrBatchItems
                    .Where(i => i.BatchId == batchId)
                    .Select(i => new RnrBatchItemDto(
                        i.ItemId,
                        i.Brand,
                        i.Model,
                        i.DeviceType,
                        i.Description,
                        i.QuantityExpected,
                        i.QuantityScanned
                    ))
                    .ToListAsync(ct);

                return new RnrBatchDetailsDto(
                    batch.BatchId,
                    batch.BatchNumber,
                    batch.CollectionSlipNumber,
                    batch.SchoolName,
                    batch.TotalQuantityExpected,
                    batch.TotalQuantityScanned,
                    batch.Status,
                    batch.CreatedAt,
                    batch.GRVNumber,
                    items
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[R&R Batch] Error retrieving batch details for {BatchId}", batchId);
                throw;
            }
        }

        /// <summary>
        /// Create R&R batch from uploaded devices
        /// </summary>
        public async Task<Guid> CreateBatchFromImportAsync(
            Guid importBatchId,
            string collectionSlipNumber,
            long? schoolId,
            string? schoolName,
            List<(string? Brand, string? Model, string? DeviceType, string? Description, int Quantity)> items,
            CancellationToken ct = default)
        {
            try
            {
                // Generate batch number
                var today = DateTime.UtcNow.ToString("yyyyMMdd");
                var dailyCount = await _db.RnrBatches
                    .Where(b => b.BatchNumber.StartsWith($"RNR-{today}"))
                    .CountAsync(ct);
                
                var batchNumber = $"RNR-{today}-{(dailyCount + 1):D4}";

                var batch = new RnrBatch
                {
                    BatchId = Guid.NewGuid(),
                    BatchNumber = batchNumber,
                    CollectionSlipNumber = collectionSlipNumber,
                    SchoolId = schoolId,
                    SchoolName = schoolName,
                    TotalQuantityExpected = items.Sum(i => i.Quantity),
                    TotalQuantityScanned = 0,
                    Status = RnrBatchStatus.PendingScan,
                    CreatedBy = "system", // TODO: Get from authentication
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _db.RnrBatches.Add(batch);

                // Add items
                foreach (var item in items)
                {
                    var batchItem = new RnrBatchItem
                    {
                        ItemId = Guid.NewGuid(),
                        BatchId = batch.BatchId,
                        Brand = item.Brand,
                        Model = item.Model,
                        DeviceType = item.DeviceType,
                        Description = item.Description,
                        QuantityExpected = item.Quantity,
                        QuantityScanned = 0
                    };
                    _db.RnrBatchItems.Add(batchItem);
                }

                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("[R&R Batch] Created batch {BatchNumber} with {ItemCount} items, total quantity: {TotalQty}",
                    batchNumber, items.Count, batch.TotalQuantityExpected);

                return batch.BatchId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[R&R Batch] Error creating batch from import");
                throw;
            }
        }

        /// <summary>
        /// Increment scanned count for a specific R&R batch item
        /// </summary>
        public async Task<RnrBatchDetailsDto?> IncrementItemScanAsync(
            Guid batchId,
            string? brand,
            string? model,
            string? deviceType,
            CancellationToken ct = default)
        {
            try
            {
                // Find the batch item
                var item = await _db.RnrBatchItems
                    .FirstOrDefaultAsync(i => 
                        i.BatchId == batchId &&
                        i.Brand == brand &&
                        i.Model == model &&
                        i.DeviceType == deviceType, 
                        ct);

                if (item == null)
                {
                    _logger.LogWarning("[R&R Batch] Item not found in batch {BatchId}: {Brand} {Model} {DeviceType}", 
                        batchId, brand, model, deviceType);
                    return null;
                }

                // Check if already at capacity
                if (item.QuantityScanned >= item.QuantityExpected)
                {
                    _logger.LogWarning("[R&R Batch] Item already fully scanned: {Brand} {Model} {DeviceType}", 
                        brand, model, deviceType);
                    return null;
                }

                // Increment item scan count
                item.QuantityScanned++;

                // Update batch total and status
                var batch = await _db.RnrBatches.FindAsync(new object[] { batchId }, ct);
                if (batch != null)
                {
                    batch.TotalQuantityScanned++;
                    
                    // Set status to ScanningInProgress on first scan
                    if (batch.Status == RnrBatchStatus.PendingScan)
                    {
                        batch.Status = RnrBatchStatus.ScanningInProgress;
                    }
                }

                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("[R&R Batch] Incremented scan for {Brand} {Model} {DeviceType} in batch {BatchId}. Now: {Scanned}/{Expected}",
                    brand, model, deviceType, batchId, item.QuantityScanned, item.QuantityExpected);

                // Return updated batch details
                return await GetBatchDetailsAsync(batchId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[R&R Batch] Error incrementing scan for batch {BatchId}", batchId);
                throw;
            }
        }

        /// <summary>
        /// Update R&R batch status
        /// </summary>
        public async Task<bool> UpdateBatchStatusAsync(
            Guid batchId,
            RnrBatchStatus status,
            string? grvNumber = null,
            string? confirmedBy = null,
            CancellationToken ct = default)
        {
            try
            {
                var batch = await _db.RnrBatches.FindAsync(new object[] { batchId }, ct);
                if (batch == null)
                {
                    _logger.LogWarning("[R&R Batch] Batch not found: {BatchId}", batchId);
                    return false;
                }

                batch.Status = status;

                if (!string.IsNullOrWhiteSpace(grvNumber))
                {
                    batch.GRVNumber = grvNumber;
                }

                if (!string.IsNullOrWhiteSpace(confirmedBy))
                {
                    batch.ConfirmedBy = confirmedBy;
                    batch.ConfirmedAt = DateTimeOffset.UtcNow;
                }

                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("[R&R Batch] Updated batch {BatchId} status to {Status}", batchId, status);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[R&R Batch] Error updating batch status for {BatchId}", batchId);
                throw;
            }
        }
    }

    // DTOs
    public record RnrBatchDto(
        Guid BatchId,
        string BatchNumber,
        string CollectionSlipNumber,
        string? SchoolName,
        int TotalQuantityExpected,
        int TotalQuantityScanned,
        RnrBatchStatus Status,
        DateTimeOffset CreatedAt
    );

    public record RnrBatchDetailsDto(
        Guid BatchId,
        string BatchNumber,
        string CollectionSlipNumber,
        string? SchoolName,
        int TotalQuantityExpected,
        int TotalQuantityScanned,
        RnrBatchStatus Status,
        DateTimeOffset CreatedAt,
        string? GRVNumber,
        List<RnrBatchItemDto> Items
    );

    public record RnrBatchItemDto(
        Guid ItemId,
        string? Brand,
        string? Model,
        string? DeviceType,
        string? Description,
        int QuantityExpected,
        int QuantityScanned
    );
}
