using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase0.Models;
using DeviceDesk.Middleware;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase0.Services
{
    /// <summary>
    /// Service for managing new stock batches in Phase 0
    /// Handles batch creation, item management, and status tracking
    /// </summary>
    public class NewStockBatchService
    {
        private readonly DeviceDeskDbContext _db;
        private readonly ILogger<NewStockBatchService> _logger;

        public NewStockBatchService(DeviceDeskDbContext db, ILogger<NewStockBatchService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Create a new stock batch with items
        /// </summary>
        public async Task<NewStockBatch> CreateBatchAsync(
            string supplierName,
            string? invoiceNumber,
            DateTime? expectedDeliveryDate,
            List<NewStockBatchItemDto> items,
            string createdBy,
            CancellationToken ct = default)
        {
            _logger.LogInformation("[Phase 0 Batch] Creating new stock batch by {User}", createdBy);

            // Validate
            if (items == null || items.Count == 0)
                throw new ValidationException("items", "At least one item is required");

            // Generate batch number
            var batchNumber = await GenerateBatchNumberAsync(ct);

            // Calculate total quantity
            var totalQuantity = items.Sum(i => i.QuantityExpected);

            // Create batch
            var batch = new NewStockBatch
            {
                BatchNumber = batchNumber,
                SupplierName = supplierName,
                InvoiceNumber = invoiceNumber,
                ExpectedDeliveryDate = expectedDeliveryDate,
                TotalQuantityExpected = totalQuantity,
                TotalQuantityScanned = 0,
                Status = NewStockBatchStatus.PendingScan,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            // Add items
            foreach (var itemDto in items)
            {
                batch.Items.Add(new NewStockBatchItem
                {
                    Brand = itemDto.Brand,
                    Model = itemDto.Model,
                    DeviceType = itemDto.DeviceType,
                    Description = itemDto.Description,
                    QuantityExpected = itemDto.QuantityExpected,
                    QuantityScanned = 0,
                    Zone = "New Stock"
                });
            }

            _db.NewStockBatches.Add(batch);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[Phase 0 Batch] Created batch {BatchNumber} with {ItemCount} items, total quantity {TotalQuantity}",
                batchNumber, items.Count, totalQuantity);

            return batch;
        }

        /// <summary>
        /// Get all batches with optional status filter
        /// </summary>
        public async Task<List<NewStockBatchSummaryDto>> GetBatchesAsync(
            NewStockBatchStatus? status = null,
            CancellationToken ct = default)
        {
            var query = _db.NewStockBatches.AsQueryable();

            if (status.HasValue)
                query = query.Where(b => b.Status == status.Value);

            var batches = await query
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new NewStockBatchSummaryDto
                {
                    BatchId = b.BatchId,
                    BatchNumber = b.BatchNumber,
                    SupplierName = b.SupplierName,
                    InvoiceNumber = b.InvoiceNumber,
                    TotalQuantityExpected = b.TotalQuantityExpected,
                    TotalQuantityScanned = b.TotalQuantityScanned,
                    Status = b.Status,
                    StatusText = b.Status.ToString(),
                    CreatedBy = b.CreatedBy,
                    CreatedAt = b.CreatedAt,
                    ConfirmedBy = b.ConfirmedBy,
                    ConfirmedAt = b.ConfirmedAt,
                    GRVNumber = b.GRVNumber
                })
                .ToListAsync(ct);

            _logger.LogInformation("[Phase 0 Batch] Retrieved {Count} batches", batches.Count);
            return batches;
        }

        /// <summary>
        /// Get batch details with items
        /// </summary>
        public async Task<NewStockBatchDetailsDto?> GetBatchDetailsAsync(
            Guid batchId,
            CancellationToken ct = default)
        {
            var batch = await _db.NewStockBatches
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.BatchId == batchId, ct);

            if (batch == null)
                return null;

            return new NewStockBatchDetailsDto
            {
                BatchId = batch.BatchId,
                BatchNumber = batch.BatchNumber,
                SupplierName = batch.SupplierName,
                InvoiceNumber = batch.InvoiceNumber,
                ExpectedDeliveryDate = batch.ExpectedDeliveryDate,
                TotalQuantityExpected = batch.TotalQuantityExpected,
                TotalQuantityScanned = batch.TotalQuantityScanned,
                Status = batch.Status,
                StatusText = batch.Status.ToString(),
                CreatedBy = batch.CreatedBy,
                CreatedAt = batch.CreatedAt,
                ConfirmedBy = batch.ConfirmedBy,
                ConfirmedAt = batch.ConfirmedAt,
                GRVNumber = batch.GRVNumber,
                Notes = batch.Notes,
                Items = batch.Items.Select(i => new NewStockBatchItemDetailsDto
                {
                    ItemId = i.ItemId,
                    Brand = i.Brand,
                    Model = i.Model,
                    DeviceType = i.DeviceType,
                    Description = i.Description,
                    QuantityExpected = i.QuantityExpected,
                    QuantityScanned = i.QuantityScanned,
                    Zone = i.Zone
                }).ToList()
            };
        }

        /// <summary>
        /// Generate unique batch number (NB-YYYY-NNNNN)
        /// </summary>
        private async Task<string> GenerateBatchNumberAsync(CancellationToken ct)
        {
            var year = DateTime.Now.Year;
            var prefix = $"NB-{year}-";

            var lastBatch = await _db.NewStockBatches
                .Where(b => b.BatchNumber.StartsWith(prefix))
                .OrderByDescending(b => b.BatchNumber)
                .FirstOrDefaultAsync(ct);

            int nextNumber = 1;
            if (lastBatch != null)
            {
                var lastNumberStr = lastBatch.BatchNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D5}";
        }
    }

    // DTOs
    public class NewStockBatchItemDto
    {
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? DeviceType { get; set; }
        public string? Description { get; set; }
        public int QuantityExpected { get; set; }
    }

    public class NewStockBatchSummaryDto
    {
        public Guid BatchId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string? SupplierName { get; set; }
        public string? InvoiceNumber { get; set; }
        public int TotalQuantityExpected { get; set; }
        public int TotalQuantityScanned { get; set; }
        public NewStockBatchStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ConfirmedBy { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? GRVNumber { get; set; }
    }

    public class NewStockBatchDetailsDto
    {
        public Guid BatchId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string? SupplierName { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public int TotalQuantityExpected { get; set; }
        public int TotalQuantityScanned { get; set; }
        public NewStockBatchStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ConfirmedBy { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? GRVNumber { get; set; }
        public string? Notes { get; set; }
        public List<NewStockBatchItemDetailsDto> Items { get; set; } = new();
    }

    public class NewStockBatchItemDetailsDto
    {
        public Guid ItemId { get; set; }
        public string? Brand { get; set; }
        public string? Model { get; set; }
        public string? DeviceType { get; set; }
        public string? Description { get; set; }
        public int QuantityExpected { get; set; }
        public int QuantityScanned { get; set; }
        public string Zone { get; set; } = string.Empty;
    }
}
