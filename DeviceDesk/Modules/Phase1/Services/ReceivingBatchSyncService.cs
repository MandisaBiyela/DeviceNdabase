using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase0.Models;
using DeviceDesk.Modules.Phase1.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Services
{
    /// <summary>
    /// Service to sync ScannedSerials from DeviceDeskDbContext to ReceivingBatchItems in Phase1DbContext
    /// </summary>
    public class ReceivingBatchSyncService
    {
        private readonly DeviceDeskDbContext _mainDb;
        private readonly Phase1DbContext _phase1Db;
        private readonly ILogger<ReceivingBatchSyncService> _logger;

        public ReceivingBatchSyncService(
            DeviceDeskDbContext mainDb,
            Phase1DbContext phase1Db,
            ILogger<ReceivingBatchSyncService> logger)
        {
            _mainDb = mainDb;
            _phase1Db = phase1Db;
            _logger = logger;
        }

        /// <summary>
        /// Sync all ScannedSerials to ReceivingBatchItems
        /// </summary>
        public async Task<SyncResultDto> SyncScannedSerialsToReceivingBatchItemsAsync(CancellationToken ct = default)
        {
            var result = new SyncResultDto
            {
                TotalProcessed = 0,
                Created = 0,
                Skipped = 0,
                Errors = 0
            };

            _logger.LogInformation("[Sync] Starting sync of ScannedSerials to ReceivingBatchItems");

            // Get all ScannedSerials
            var scannedSerials = await _mainDb.ScannedSerials
                .Include(s => s.Model)
                .ToListAsync(ct);

            result.TotalProcessed = scannedSerials.Count;

            foreach (var scannedSerial in scannedSerials)
            {
                try
                {
                    // Find ReceivingBatch where NewStockBatchId or OrderId matches the ScannedSerial's OrderID
                    // For NewStock batches, use NewStockBatchId; for legacy Orders, use OrderId
                    var receivingBatch = await _phase1Db.ReceivingBatches
                        .FirstOrDefaultAsync(b => b.NewStockBatchId == scannedSerial.OrderID || b.OrderId == scannedSerial.OrderID, ct);

                    if (receivingBatch == null)
                    {
                        _logger.LogWarning("[Sync] ReceivingBatch not found for OrderID {OrderID}. Skipping serial {Serial}",
                            scannedSerial.OrderID, scannedSerial.DeviceSerial);
                        result.Skipped++;
                        continue;
                    }

                    // Check if ReceivingBatchItem already exists
                    var existingItem = await _phase1Db.ReceivingBatchItems
                        .AnyAsync(i => i.ReceivingBatchId == receivingBatch.ReceivingBatchId &&
                                      i.SerialNumber == scannedSerial.DeviceSerial, ct);

                    if (existingItem)
                    {
                        result.Skipped++;
                        continue;
                    }

                    // Get OrderModelList to parse ModelName
                    var orderModel = scannedSerial.Model;
                    if (orderModel == null)
                    {
                        // Try to load it if not included
                        orderModel = await _mainDb.OrderModelLists
                            .FirstOrDefaultAsync(m => m.ModelID == scannedSerial.ModelID, ct);
                    }

                    string? brand = null;
                    string? model = null;

                    if (orderModel != null && !string.IsNullOrWhiteSpace(orderModel.ModelName))
                    {
                        ParseModelName(orderModel.ModelName, out brand, out model);
                    }

                    // Create ReceivingBatchItem
                    var receivingBatchItem = new ReceivingBatchItem
                    {
                        ReceivingBatchId = receivingBatch.ReceivingBatchId,
                        SerialNumber = scannedSerial.DeviceSerial,
                        IMEI = null,
                        Brand = brand,
                        Model = model,
                        Notes = null,
                        CreatedAt = scannedSerial.Timestamp
                    };

                    _phase1Db.ReceivingBatchItems.Add(receivingBatchItem);
                    result.Created++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Sync] Error processing ScannedSerial {SerialID} (Serial: {Serial})",
                        scannedSerial.SerialID, scannedSerial.DeviceSerial);
                    result.Errors++;
                }
            }

            // Save all changes
            if (result.Created > 0)
            {
                await _phase1Db.SaveChangesAsync(ct);
                _logger.LogInformation("[Sync] Saved {Count} new ReceivingBatchItems", result.Created);
            }

            _logger.LogInformation("[Sync] Sync completed. Processed: {Total}, Created: {Created}, Skipped: {Skipped}, Errors: {Errors}",
                result.TotalProcessed, result.Created, result.Skipped, result.Errors);

            return result;
        }

        /// <summary>
        /// Parse ModelName string to extract Brand and Model
        /// ModelName format: "Brand Model DeviceType" (e.g., "HP EliteBook 840 Laptop")
        /// </summary>
        private void ParseModelName(string modelName, out string? brand, out string? model)
        {
            brand = null;
            model = null;

            if (string.IsNullOrWhiteSpace(modelName))
            {
                return;
            }

            var parts = modelName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
            {
                return;
            }
            else if (parts.Length == 1)
            {
                // Single word: treat as Brand only
                brand = parts[0];
                model = null;
            }
            else if (parts.Length == 2)
            {
                // Two words: first is Brand, second is Model
                brand = parts[0];
                model = parts[1];
            }
            else
            {
                // Three or more words: first is Brand, last is DeviceType, middle is Model
                brand = parts[0];
                // Join all parts except first and last as Model
                model = string.Join(" ", parts.Skip(1).Take(parts.Length - 2));
            }
        }
    }

    /// <summary>
    /// Result DTO for sync operation
    /// </summary>
    public class SyncResultDto
    {
        public int TotalProcessed { get; set; }
        public int Created { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
    }
}


