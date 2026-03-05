using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase0.Models;
using DeviceDesk.Modules.Phase1.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Services
{
    /// <summary>
    /// Service for model-driven scanning workflow
    /// </summary>
    public class ModelDrivenScanningService
    {
        private readonly DeviceDeskDbContext _db;
        private readonly Phase1DbContext _phase1Db;
        private readonly ILogger<ModelDrivenScanningService> _logger;

        public ModelDrivenScanningService(DeviceDeskDbContext db, Phase1DbContext phase1Db, ILogger<ModelDrivenScanningService> logger)
        {
            _db = db;
            _phase1Db = phase1Db;
            _logger = logger;
        }

        /// <summary>
        /// Get all available orders (batches) for selection
        /// </summary>
        public async Task<List<OrderSummaryDto>> GetAvailableOrdersAsync(CancellationToken ct = default)
        {
            var orders = await _db.NewStockBatches
                .Where(b => b.Status == NewStockBatchStatus.PendingScan || 
                           b.Status == NewStockBatchStatus.Scanning)
                .Select(b => new OrderSummaryDto
                {
                    OrderID = b.BatchId,
                    BatchNumber = b.BatchNumber,
                    SupplierName = b.SupplierName ?? "",
                    InvoiceNumber = b.InvoiceNumber ?? "",
                    TotalExpected = b.TotalQuantityExpected,
                    TotalScanned = b.TotalQuantityScanned,
                    Status = b.Status.ToString()
                })
                .ToListAsync(ct);

            return orders;
        }

        /// <summary>
        /// Get all models for a specific order
        /// If no models exist, automatically populate them from NewStockBatchItems
        /// </summary>
        public async Task<List<ModelDto>> GetModelsForOrderAsync(Guid orderID, CancellationToken ct = default)
        {
            // Check if models already exist
            var existingModels = await _db.OrderModelLists
                .Where(m => m.OrderID == orderID)
                .ToListAsync(ct);

            // If no models exist, create them from NewStockBatchItems
            if (existingModels.Count == 0)
            {
                _logger.LogInformation("[ModelScanning] No OrderModelList entries found for order {OrderID}, populating from NewStockBatchItems", orderID);
                
                var batch = await _db.NewStockBatches
                    .Include(b => b.Items)
                    .FirstOrDefaultAsync(b => b.BatchId == orderID, ct);

                if (batch != null && batch.Items != null && batch.Items.Any())
                {
                    // Group items by Brand, Model, DeviceType to create models
                    var itemGroups = batch.Items
                        .GroupBy(item => new { item.Brand, item.Model, item.DeviceType })
                        .ToList();

                    foreach (var group in itemGroups)
                    {
                        var modelName = $"{group.Key.Brand} {group.Key.Model} {group.Key.DeviceType}".Trim();
                        var expectedQty = group.Sum(item => item.QuantityExpected);

                        var orderModel = new OrderModelList
                        {
                            ModelID = Guid.NewGuid(),
                            OrderID = orderID,
                            ModelName = modelName,
                            ExpectedQty = expectedQty,
                            CountedQty = 0,
                            Status = "Open"
                        };

                        _db.OrderModelLists.Add(orderModel);
                    }

                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("[ModelScanning] Created {Count} OrderModelList entries for order {OrderID}", itemGroups.Count, orderID);

                    // Re-fetch the newly created models
                    existingModels = await _db.OrderModelLists
                        .Where(m => m.OrderID == orderID)
                        .ToListAsync(ct);
                }
                else
                {
                    // Try to create from Devices if batch items don't exist
                    var devices = await _db.Devices
                        .Where(d => d.BatchId == orderID)
                        .ToListAsync(ct);

                    if (devices.Any())
                    {
                        var deviceGroups = devices
                            .GroupBy(d => new { d.Brand, d.Model, d.DeviceType })
                            .ToList();

                        foreach (var group in deviceGroups)
                        {
                            var modelName = $"{group.Key.Brand} {group.Key.Model} {group.Key.DeviceType}".Trim();
                            var expectedQty = group.Count();

                            var orderModel = new OrderModelList
                            {
                                ModelID = Guid.NewGuid(),
                                OrderID = orderID,
                                ModelName = modelName,
                                ExpectedQty = expectedQty,
                                CountedQty = 0,
                                Status = "Open"
                            };

                            _db.OrderModelLists.Add(orderModel);
                        }

                        await _db.SaveChangesAsync(ct);
                        _logger.LogInformation("[ModelScanning] Created {Count} OrderModelList entries from Devices for order {OrderID}", deviceGroups.Count, orderID);

                        // Re-fetch the newly created models
                        existingModels = await _db.OrderModelLists
                            .Where(m => m.OrderID == orderID)
                            .ToListAsync(ct);
                    }
                }
            }

            // Return models as DTOs
            return existingModels.Select(m => new ModelDto
            {
                ModelID = m.ModelID,
                ModelName = m.ModelName,
                ExpectedQty = m.ExpectedQty,
                CountedQty = m.CountedQty,
                Status = m.Status,
                Variance = m.ExpectedQty - m.CountedQty
            }).ToList();
        }

        /// <summary>
        /// Record a scanned serial for the active model
        /// </summary>
        public async Task<ModelScanResultDto> ScanSerialAsync(Guid orderID, Guid modelID, string serial, CancellationToken ct = default)
        {
            // Validate model exists and is open
            var model = await _db.OrderModelLists
                .FirstOrDefaultAsync(m => m.ModelID == modelID && m.OrderID == orderID, ct);

            if (model == null)
            {
                return new ModelScanResultDto
                {
                    Success = false,
                    Message = "Model not found or does not belong to this order"
                };
            }

            if (model.Status != "Open")
            {
                return new ModelScanResultDto
                {
                    Success = false,
                    Message = $"Model is {model.Status}. Cannot scan closed models."
                };
            }

            // Check if serial already exists
            var existingSerial = await _db.ScannedSerials
                .AnyAsync(s => s.DeviceSerial == serial, ct);

            if (existingSerial)
            {
                return new ModelScanResultDto
                {
                    Success = false,
                    Message = "Serial number already scanned"
                };
            }

            // Check if we've already reached expected quantity
            if (model.CountedQty >= model.ExpectedQty)
            {
                return new ModelScanResultDto
                {
                    Success = false,
                    Message = $"Model already has {model.CountedQty} of {model.ExpectedQty} expected. Cannot scan more."
                };
            }

            // Insert into ScannedSerials
            var scannedSerial = new ScannedSerial
            {
                OrderID = orderID,
                ModelID = modelID,
                DeviceSerial = serial,
                Timestamp = DateTime.UtcNow
            };

            _db.ScannedSerials.Add(scannedSerial);

            // Increment CountedQty
            model.CountedQty++;

            await _db.SaveChangesAsync(ct);

            // Dual-write: Also write to ReceivingBatchItems for Phase 1 integration
            try
            {
                // For NewStock batches, use NewStockBatchId; for legacy Orders, use OrderId
                var receivingBatch = await _phase1Db.ReceivingBatches
                    .FirstOrDefaultAsync(b => b.NewStockBatchId == orderID || b.OrderId == orderID, ct);

                if (receivingBatch != null)
                {
                    // Check if ReceivingBatchItem already exists (avoid duplicates)
                    var existingItem = await _phase1Db.ReceivingBatchItems
                        .AnyAsync(i => i.ReceivingBatchId == receivingBatch.ReceivingBatchId && 
                                      i.SerialNumber == serial, ct);

                    if (!existingItem)
                    {
                        // Parse ModelName to extract Brand and Model
                        ParseModelName(model.ModelName, out string? brand, out string? parsedModel);

                        var receivingBatchItem = new ReceivingBatchItem
                        {
                            ReceivingBatchId = receivingBatch.ReceivingBatchId,
                            SerialNumber = serial,
                            IMEI = null,
                            Brand = brand ?? string.Empty,
                            Model = parsedModel ?? string.Empty,
                            Notes = null
                        };

                        _phase1Db.ReceivingBatchItems.Add(receivingBatchItem);
                        await _phase1Db.SaveChangesAsync(ct);

                        _logger.LogInformation("[ModelScan] Also synced to ReceivingBatchItems: {Serial} for batch {BatchId}",
                            serial, receivingBatch.ReceivingBatchId);
                    }
                }
                else
                {
                    _logger.LogWarning("[ModelScan] ReceivingBatch not found for OrderID {OrderID}. Skipping ReceivingBatchItems sync.",
                        orderID);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the scan operation
                _logger.LogError(ex, "[ModelScan] Error syncing to ReceivingBatchItems for serial {Serial}", serial);
            }

            _logger.LogInformation("[ModelScan] Scanned serial {Serial} for model {ModelID} ({CountedQty}/{ExpectedQty})",
                serial, modelID, model.CountedQty, model.ExpectedQty);

            return new ModelScanResultDto
            {
                Success = true,
                Message = "Serial scanned successfully",
                CountedQty = model.CountedQty,
                ExpectedQty = model.ExpectedQty,
                Remaining = model.ExpectedQty - model.CountedQty
            };
        }

        /// <summary>
        /// Close a model after scanning is complete
        /// </summary>
        public async Task<bool> CloseModelAsync(Guid modelID, CancellationToken ct = default)
        {
            var model = await _db.OrderModelLists.FindAsync(new object[] { modelID }, ct);

            if (model == null) return false;

            model.Status = "Closed";
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("[ModelScan] Model {ModelID} closed. Counted: {CountedQty}/{ExpectedQty}",
                modelID, model.CountedQty, model.ExpectedQty);

            return true;
        }

        /// <summary>
        /// Calculate variance for all models in an order
        /// </summary>
        public async Task<VarianceResultDto> CalculateVarianceAsync(Guid orderID, CancellationToken ct = default)
        {
            var models = await _db.OrderModelLists
                .Where(m => m.OrderID == orderID)
                .ToListAsync(ct);

            var modelVariances = models.Select(m => new ModelVarianceDto
            {
                ModelID = m.ModelID,
                ModelName = m.ModelName,
                ExpectedQty = m.ExpectedQty,
                CountedQty = m.CountedQty,
                Variance = m.ExpectedQty - m.CountedQty,
                Status = m.Status
            }).ToList();

            var allClosed = models.All(m => m.Status == "Closed");
            var allMatch = models.All(m => m.ExpectedQty == m.CountedQty);
            var hasShortages = models.Any(m => m.CountedQty < m.ExpectedQty);

            return new VarianceResultDto
            {
                OrderID = orderID,
                AllModelsClosed = allClosed,
                AllQuantitiesMatch = allMatch,
                HasShortages = hasShortages,
                CanGenerateGRV = allClosed && allMatch,
                Models = modelVariances
            };
        }

        /// <summary>
        /// Get all scanned serials for a specific model
        /// </summary>
        public async Task<List<ScannedSerialDto>> GetScannedSerialsAsync(Guid orderID, Guid modelID, CancellationToken ct = default)
        {
            var serials = await _db.ScannedSerials
                .Where(s => s.OrderID == orderID && s.ModelID == modelID)
                .OrderBy(s => s.Timestamp)
                .Select(s => new ScannedSerialDto
                {
                    SerialID = s.SerialID,
                    DeviceSerial = s.DeviceSerial,
                    Timestamp = s.Timestamp
                })
                .ToListAsync(ct);

            return serials;
        }

        public async Task<NewStockBatch?> ConfirmBatchAsync(Guid orderID, string grvNumber, CancellationToken ct = default)
        {
            var batch = await _db.NewStockBatches.FindAsync(new object[] { orderID }, ct);
            if (batch == null) return null;

            batch.GRVNumber = grvNumber;
            batch.Status = NewStockBatchStatus.Completed;
            batch.ConfirmedAt = DateTime.UtcNow;
            batch.ConfirmedBy = "System"; // TODO: Get actual user from context

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("[ModelScan] Batch {OrderID} confirmed with GRV {GRVNumber}", orderID, grvNumber);

            return batch;
        }

        /// <summary>
        /// Get GRV document data for a completed batch
        /// </summary>
        public async Task<GRVDataDto?> GetGRVDataAsync(Guid orderID, CancellationToken ct = default)
        {
            var batch = await _db.NewStockBatches
                .Where(b => b.BatchId == orderID)
                .FirstOrDefaultAsync(ct);

            if (batch == null || string.IsNullOrEmpty(batch.GRVNumber))
                return null;

            var models = await _db.OrderModelLists
                .Where(m => m.OrderID == orderID)
                .OrderBy(m => m.ModelName)
                .ToListAsync(ct);

            var serials = await _db.ScannedSerials
                .Where(s => s.OrderID == orderID)
                .OrderBy(s => s.Timestamp)
                .ToListAsync(ct);

            var serialsByModel = serials.GroupBy(s => s.ModelID)
                .ToDictionary(g => g.Key, g => g.Select(s => s.DeviceSerial).ToList());

            return new GRVDataDto
            {
                GRVNumber = batch.GRVNumber,
                BatchNumber = batch.BatchNumber,
                SupplierName = batch.SupplierName ?? "N/A",
                InvoiceNumber = batch.InvoiceNumber ?? "N/A",
                CreatedDate = batch.CreatedAt,
                ConfirmedDate = batch.ConfirmedAt,
                ConfirmedBy = batch.ConfirmedBy ?? "System",
                TotalQuantity = batch.TotalQuantityScanned,
                Models = models.Select(m => new GRVModelDto
                {
                    ModelID = m.ModelID,
                    ModelName = m.ModelName,
                    ExpectedQty = m.ExpectedQty,
                    CountedQty = m.CountedQty,
                    Variance = m.ExpectedQty - m.CountedQty,
                    Serials = serialsByModel.ContainsKey(m.ModelID) ? serialsByModel[m.ModelID] : new List<string>()
                }).ToList()
            };
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

    // DTOs
    public class OrderSummaryDto
    {
        public Guid OrderID { get; set; }
        public string BatchNumber { get; set; } = string.Empty;

        public string SupplierName { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public int TotalExpected { get; set; }
        public int TotalScanned { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ModelDto
    {
        public Guid ModelID { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public int ExpectedQty { get; set; }
        public int CountedQty { get; set; }
        public string Status { get; set; } = string.Empty;
        public int Variance { get; set; }
    }

    public class ModelScanResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CountedQty { get; set; }
        public int ExpectedQty { get; set; }
        public int Remaining { get; set; }
    }

    public class VarianceResultDto
    {
        public Guid OrderID { get; set; }
        public bool AllModelsClosed { get; set; }
        public bool AllQuantitiesMatch { get; set; }
        public bool HasShortages { get; set; }
        public bool CanGenerateGRV { get; set; }
        public List<ModelVarianceDto> Models { get; set; } = new List<ModelVarianceDto>();
    }

    public class ModelVarianceDto
    {
        public Guid ModelID { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public int ExpectedQty { get; set; }
        public int CountedQty { get; set; }
        public int Variance { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ScannedSerialDto
    {
        public Guid SerialID { get; set; }
        public string DeviceSerial { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
    }

    public class GRVDataDto
    {
        public string GRVNumber { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? ConfirmedDate { get; set; }
        public string ConfirmedBy { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public List<GRVModelDto> Models { get; set; } = new List<GRVModelDto>();
    }

    public class GRVModelDto
    {
        public Guid ModelID { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public int ExpectedQty { get; set; }
        public int CountedQty { get; set; }
        public int Variance { get; set; }
        public List<string> Serials { get; set; } = new List<string>();
    }
}
