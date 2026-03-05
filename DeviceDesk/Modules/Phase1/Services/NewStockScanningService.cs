using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase0.Models;
using DeviceDesk.Middleware;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Services
{
    /// <summary>
    /// Service for Phase 1 blind copy scanning of new stock batches
    /// Handles device scanning, validation, and batch confirmation
    /// </summary>
    public class NewStockScanningService
    {
        private readonly DeviceDeskDbContext _db;
        private readonly ILogger<NewStockScanningService> _logger;

        public NewStockScanningService(DeviceDeskDbContext db, ILogger<NewStockScanningService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Get all batches pending scan from Phase 0
        /// </summary>
        public async Task<List<PendingBatchDto>> GetPendingBatchesAsync(CancellationToken ct = default)
        {
            var batches = await _db.NewStockBatches
                .Where(b => b.Status == NewStockBatchStatus.PendingScan || 
                           b.Status == NewStockBatchStatus.Scanning)
                .OrderBy(b => b.CreatedAt)
                .Select(b => new PendingBatchDto
                {
                    BatchId = b.BatchId,
                    BatchNumber = b.BatchNumber,
                    SupplierName = b.SupplierName,
                    InvoiceNumber = b.InvoiceNumber,
                    TotalQuantityExpected = b.TotalQuantityExpected,
                    TotalQuantityScanned = b.TotalQuantityScanned,
                    Status = b.Status,
                    CreatedBy = b.CreatedBy,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync(ct);

            _logger.LogInformation("[Phase 1 Scanning] Found {Count} pending batches", batches.Count);
            return batches;
        }

        /// <summary>
        /// Get batch details for scanning (blind copy - no serials shown)
        /// </summary>
        public async Task<BatchScanningDto?> GetBatchForScanningAsync(
            Guid batchId,
            CancellationToken ct = default)
        {
            var batch = await _db.NewStockBatches
                .Include(b => b.Items)
                .Include(b => b.ScannedDevices)
                .FirstOrDefaultAsync(b => b.BatchId == batchId, ct);

            if (batch == null)
                return null;

            return new BatchScanningDto
            {
                BatchId = batch.BatchId,
                BatchNumber = batch.BatchNumber,
                SupplierName = batch.SupplierName,
                InvoiceNumber = batch.InvoiceNumber,
                TotalQuantityExpected = batch.TotalQuantityExpected,
                TotalQuantityScanned = batch.TotalQuantityScanned,
                Status = batch.Status,
                Items = batch.Items.Select(i => new BatchItemScanDto
                {
                    ItemId = i.ItemId,
                    Brand = i.Brand,
                    Model = i.Model,
                    DeviceType = i.DeviceType,
                    Description = i.Description,
                    QuantityExpected = i.QuantityExpected,
                    QuantityScanned = i.QuantityScanned
                }).ToList(),
                ScannedDevices = batch.ScannedDevices
                    .OrderByDescending(d => d.ScannedAt)
                    .Select(d => new BatchScannedDeviceDto
                    {
                        ScanId = d.ScanId,
                        SerialNumber = d.SerialNumber,
                        IMEI = d.IMEI,
                        Brand = d.Brand,
                        Model = d.Model,
                        ScannedAt = d.ScannedAt,
                        ScannedBy = d.ScannedBy,
                        IsDuplicate = d.IsDuplicate
                    }).ToList()
            };
        }

        /// <summary>
        /// Scan a device serial number
        /// </summary>
        public async Task<ScanResultDto> ScanDeviceAsync(
            Guid batchId,
            string serialNumber,
            string? imei,
            string? brand,
            string? model,
            string scannedBy,
            CancellationToken ct = default)
        {
            _logger.LogInformation("[Phase 1 Scanning] Scanning device {Serial} for batch {BatchId}", 
                serialNumber, batchId);

            // Get batch
            var batch = await _db.NewStockBatches
                .Include(b => b.ScannedDevices)
                .FirstOrDefaultAsync(b => b.BatchId == batchId, ct);

            if (batch == null)
                throw new NotFoundException("Batch", batchId);

            // Check batch status
            if (batch.Status == NewStockBatchStatus.Completed)
                throw new BusinessRuleException("This batch has already been completed");

            if (batch.Status == NewStockBatchStatus.Cancelled)
                throw new BusinessRuleException("This batch has been cancelled");

            // Update status to Scanning if it's PendingScan
            if (batch.Status == NewStockBatchStatus.PendingScan)
            {
                batch.Status = NewStockBatchStatus.Scanning;
            }

            // Check for duplicate within batch
            var duplicateInBatch = batch.ScannedDevices
                .Any(d => d.SerialNumber.Equals(serialNumber, StringComparison.OrdinalIgnoreCase));

            // Check for duplicate in other batches
            var duplicateInOtherBatch = await _db.NewStockScannedDevices
                .AnyAsync(d => d.BatchId != batchId && 
                              d.SerialNumber.Equals(serialNumber, StringComparison.OrdinalIgnoreCase), ct);

            var isDuplicate = duplicateInBatch || duplicateInOtherBatch;

            if (isDuplicate)
            {
                _logger.LogWarning("[Phase 1 Scanning] Duplicate serial {Serial} detected", serialNumber);
                return new ScanResultDto
                {
                    Success = false,
                    Message = $"Duplicate serial number: {serialNumber}",
                    IsDuplicate = true
                };
            }

            // Add scanned device
            var scannedDevice = new NewStockScannedDevice
            {
                BatchId = batchId,
                SerialNumber = serialNumber,
                IMEI = imei,
                Brand = brand,
                Model = model,
                ScannedAt = DateTime.UtcNow,
                ScannedBy = scannedBy,
                IsDuplicate = false
            };

            _db.NewStockScannedDevices.Add(scannedDevice);

            // Update batch counts
            batch.TotalQuantityScanned = batch.ScannedDevices.Count + 1;

            // Update status based on quantity match
            if (batch.TotalQuantityScanned == batch.TotalQuantityExpected)
            {
                batch.Status = NewStockBatchStatus.ReadyToConfirm;
            }
            else if (batch.TotalQuantityScanned > batch.TotalQuantityExpected)
            {
                batch.Status = NewStockBatchStatus.Mismatch;
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[Phase 1 Scanning] Scanned device {Serial}, batch progress: {Scanned}/{Expected}",
                serialNumber, batch.TotalQuantityScanned, batch.TotalQuantityExpected);

            return new ScanResultDto
            {
                Success = true,
                Message = $"Device scanned successfully ({batch.TotalQuantityScanned}/{batch.TotalQuantityExpected})",
                IsDuplicate = false,
                TotalScanned = batch.TotalQuantityScanned,
                TotalExpected = batch.TotalQuantityExpected,
                Status = batch.Status
            };
        }

        /// <summary>
        /// Confirm batch and generate GRV
        /// </summary>
        public async Task<ConfirmBatchResultDto> ConfirmBatchAsync(
            Guid batchId,
            string confirmedBy,
            string? notes,
            CancellationToken ct = default)
        {
            _logger.LogInformation("[Phase 1 Scanning] Confirming batch {BatchId} by {User}", 
                batchId, confirmedBy);

            var batch = await _db.NewStockBatches
                .Include(b => b.ScannedDevices)
                .FirstOrDefaultAsync(b => b.BatchId == batchId, ct);

            if (batch == null)
                throw new NotFoundException("Batch", batchId);

            // Validate status
            if (batch.Status != NewStockBatchStatus.ReadyToConfirm && 
                batch.Status != NewStockBatchStatus.Mismatch)
            {
                throw new BusinessRuleException(
                    $"Batch cannot be confirmed in status: {batch.Status}. " +
                    "Complete scanning first.");
            }

            // Generate GRV number
            var grvNumber = await GenerateGRVNumberAsync(ct);

            // Update batch
            batch.Status = NewStockBatchStatus.Completed;
            batch.ConfirmedBy = confirmedBy;
            batch.ConfirmedAt = DateTime.UtcNow;
            batch.GRVNumber = grvNumber;
            batch.Notes = notes;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[Phase 1 Scanning] Batch {BatchNumber} confirmed, GRV {GRVNumber} generated",
                batch.BatchNumber, grvNumber);

            return new ConfirmBatchResultDto
            {
                Success = true,
                BatchNumber = batch.BatchNumber,
                GRVNumber = grvNumber,
                TotalDevices = batch.TotalQuantityScanned,
                Message = $"Batch confirmed successfully. GRV {grvNumber} generated."
            };
        }

        /// <summary>
        /// Delete a scanned device (undo scan)
        /// </summary>
        public async Task<bool> DeleteScannedDeviceAsync(
            Guid scanId,
            CancellationToken ct = default)
        {
            var scannedDevice = await _db.NewStockScannedDevices
                .Include(d => d.Batch)
                .FirstOrDefaultAsync(d => d.ScanId == scanId, ct);

            if (scannedDevice == null)
                return false;

            var batch = scannedDevice.Batch;

            // Don't allow deletion if batch is completed
            if (batch.Status == NewStockBatchStatus.Completed)
                throw new BusinessRuleException("Cannot delete scans from a completed batch");

            _db.NewStockScannedDevices.Remove(scannedDevice);

            // Update batch counts
            batch.TotalQuantityScanned = Math.Max(0, batch.TotalQuantityScanned - 1);

            // Update status
            if (batch.TotalQuantityScanned < batch.TotalQuantityExpected)
            {
                batch.Status = NewStockBatchStatus.Scanning;
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("[Phase 1 Scanning] Deleted scan {ScanId} from batch {BatchId}", 
                scanId, batch.BatchId);

            return true;
        }

        /// <summary>
        /// Generate GRV number
        /// </summary>
        private async Task<string> GenerateGRVNumberAsync(CancellationToken ct)
        {
            var year = DateTime.Now.Year;
            var prefix = $"GRV-{year}-";

            var lastGRV = await _db.NewStockBatches
                .Where(b => b.GRVNumber != null && b.GRVNumber.StartsWith(prefix))
                .OrderByDescending(b => b.GRVNumber)
                .FirstOrDefaultAsync(ct);

            int nextNumber = 1;
            if (lastGRV != null && lastGRV.GRVNumber != null)
            {
                var lastNumberStr = lastGRV.GRVNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D5}";
        }
    }

    // DTOs
    public class PendingBatchDto
    {
        public Guid BatchId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string? SupplierName { get; set; }
        public string? InvoiceNumber { get; set; }
        public int TotalQuantityExpected { get; set; }
        public int TotalQuantityScanned { get; set; }
        public NewStockBatchStatus Status { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class BatchScanningDto
    {
        public Guid BatchId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string? SupplierName { get; set; }
        public string? InvoiceNumber { get; set; }
        public int TotalQuantityExpected { get; set; }
        public int TotalQuantityScanned { get; set; }
        public NewStockBatchStatus Status { get; set; }
        public List<BatchItemScanDto> Items { get; set; } = new();
        public List<BatchScannedDeviceDto> ScannedDevices { get; set; } = new();
    }

    public class BatchItemScanDto
    {
        public Guid ItemId { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? DeviceType { get; set; }
        public string? Description { get; set; }
        public int QuantityExpected { get; set; }
        public int QuantityScanned { get; set; }
    }

    public class BatchScannedDeviceDto
    {
        public Guid ScanId { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string? IMEI { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public DateTime ScannedAt { get; set; }
        public string ScannedBy { get; set; } = string.Empty;
        public bool IsDuplicate { get; set; }
    }

    public class ScanResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsDuplicate { get; set; }
        public int TotalScanned { get; set; }
        public int TotalExpected { get; set; }
        public NewStockBatchStatus Status { get; set; }
    }

    public class ConfirmBatchResultDto
    {
        public bool Success { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string GRVNumber { get; set; } = string.Empty;
        public int TotalDevices { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
