using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Modules.Phase0.Services;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Services
{
    public class ReceivingService
    {
        private readonly Phase1DbContext _db;
        private readonly NewStockBatchService _newStockBatchService;

        public ReceivingService(Phase1DbContext db, NewStockBatchService newStockBatchService)
        {
            _db = db;
            _newStockBatchService = newStockBatchService;
        }

        public async Task<ReceivingBatch> CreateReceivingBatchAsync(CreateReceivingBatchRequest request, CancellationToken ct = default)
        {
            bool isNewStockBatch = false;
            
            // Validate source-specific requirements
            if (request.SourceType == ReceivingSourceType.NewStock)
            {
                if (!request.OrderId.HasValue)
                    throw new InvalidOperationException("OrderId is required for New Stock receiving.");

                // Check if it's a NewStockBatch from Phase 0 or old Order from Phase 1
                var newStockBatch = await _newStockBatchService.GetBatchDetailsAsync(request.OrderId.Value, ct);
                if (newStockBatch != null)
                {
                    // Valid NewStockBatch from Phase 0
                    isNewStockBatch = true;
                    
                    // Check for existing batch using NewStockBatchId
                    var existingBatch = await _db.ReceivingBatches
                        .FirstOrDefaultAsync(rb => rb.NewStockBatchId == request.OrderId.Value, ct);
                    
                    if (existingBatch != null)
                    {
                        // Return existing batch instead of creating duplicate
                        return existingBatch;
                    }
                }
                else
                {
                    // Fall back to checking Phase1 Orders (legacy support)
                    var order = await _db.Orders.FindAsync(new object[] { request.OrderId.Value }, ct);
                    if (order == null)
                        throw new InvalidOperationException($"Order or Batch {request.OrderId} not found in Phase 0 or Phase 1.");
                    
                    // For legacy Orders, check using OrderId
                    var existingBatch = await _db.ReceivingBatches
                        .FirstOrDefaultAsync(rb => rb.OrderId == request.OrderId.Value, ct);
                    
                    if (existingBatch != null)
                    {
                        // Return existing batch instead of creating duplicate
                        return existingBatch;
                    }
                }
            }
            else if (request.SourceType == ReceivingSourceType.RnrNormal || request.SourceType == ReceivingSourceType.RnrEmergency)
            {
                if (!request.CollectionSlipId.HasValue)
                    throw new InvalidOperationException("CollectionSlipId is required for RnR receiving.");

                // Check if a ReceivingBatch already exists for this CollectionSlipId
                var existingBatch = await _db.ReceivingBatches
                    .FirstOrDefaultAsync(rb => rb.CollectionSlipId == request.CollectionSlipId.Value, ct);
                
                if (existingBatch != null)
                {
                    // Return existing batch instead of creating duplicate
                    return existingBatch;
                }

                var slip = await _db.CollectionSlips.FindAsync(new object[] { request.CollectionSlipId.Value }, ct);
                if (slip == null)
                    throw new InvalidOperationException($"Collection Slip {request.CollectionSlipId} not found.");
            }

            var batch = new ReceivingBatch
            {
                SourceType = request.SourceType,
                // For NewStock: use NewStockBatchId, set OrderId to null to avoid FK constraint
                // For legacy Orders: use OrderId
                NewStockBatchId = isNewStockBatch ? request.OrderId : null,
                OrderId = isNewStockBatch
                    ? null  // Don't set OrderId for NewStock to avoid FK constraint violation
                    : request.OrderId,  // For legacy Orders, keep OrderId
                CollectionSlipId = request.CollectionSlipId,
                SchoolId = request.SchoolId,
                ReceivedBy = request.ReceivedBy,
                Notes = request.Notes,
                Status = ReceivingBatchStatus.Draft
            };

            _db.ReceivingBatches.Add(batch);
            await _db.SaveChangesAsync(ct);

            return batch;
        }

        public async Task<List<OrderDto>> GetAvailableOrdersAsync(CancellationToken ct = default)
        {
            var orders = await _db.Orders
                .Include(o => o.Lines)
                .Where(o => o.Status == OrderStatus.Approved || o.Status == OrderStatus.PartiallyReceived)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync(ct);

            return orders.Select(o => new OrderDto(
                o.OrderId,
                o.OrderNumber,
                o.InvoiceNumber,
                o.SupplierName,
                o.OrderDate,
                o.Status,
                o.Status.ToString(),
                o.Lines.Sum(l => l.QuantityOrdered),
                o.Lines.Sum(l => l.QuantityReceived),
                o.Lines.Count
            )).ToList();
        }

        public async Task<List<CollectionSlipDto>> GetAvailableCollectionSlipsAsync(ReceivingSourceType? sourceType = null, CancellationToken ct = default)
        {
            var query = _db.CollectionSlips.AsQueryable();

            if (sourceType.HasValue)
                query = query.Where(c => c.SourceType == sourceType.Value);

            var slips = await query
                .OrderByDescending(c => c.CollectionDate)
                .ToListAsync(ct);

            return slips.Select(c => new CollectionSlipDto(
                c.CollectionSlipId,
                c.SlipNumber,
                c.EmisCode,
                c.SchoolName,
                c.SourceType,
                c.SourceType.ToString(),
                c.CollectionDate,
                c.CollectedBy
            )).ToList();
        }

        public async Task<List<object>> GetAllReceivingBatchesAsync(CancellationToken ct = default)
        {
            var batches = await _db.ReceivingBatches
                .Include(b => b.Order)
                .Include(b => b.CollectionSlip)
                .Include(b => b.Items)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync(ct);

            // Fetch NewStockBatch details for batches that have NewStockBatchId
            var newStockBatchIds = batches
                .Where(b => b.SourceType == ReceivingSourceType.NewStock && b.NewStockBatchId.HasValue)
                .Select(b => b.NewStockBatchId!.Value)
                .Distinct()
                .ToList();

            var newStockBatches = new Dictionary<Guid, Modules.Phase0.Services.NewStockBatchDetailsDto>();
            foreach (var batchId in newStockBatchIds)
            {
                var details = await _newStockBatchService.GetBatchDetailsAsync(batchId, ct);
                if (details != null)
                {
                    newStockBatches[batchId] = details;
                }
            }

            return batches.Select(b =>
            {
                // For NewStock: try Order first (legacy), then NewStockBatch
                if (b.SourceType == ReceivingSourceType.NewStock)
                {
                    if (b.Order != null)
                    {
                        // Legacy Order
                        return new
                        {
                            batchId = b.ReceivingBatchId.ToString(),
                            sourceType = (int)b.SourceType,
                            sourceTypeName = b.SourceType.ToString(),
                            documentInfo = new
                            {
                                type = "Invoice",
                                number = b.Order.InvoiceNumber ?? "",
                                supplier = b.Order.SupplierName ?? "",
                                school = "",
                                emisCode = "",
                                uploadedAt = b.CreatedAt
                            },
                            schoolSupplier = b.Order.SupplierName ?? "",
                            status = b.Status.ToString(),
                            deviceCount = b.ExpectedCount,
                            actualCount = b.ActualCount,
                            createdAt = b.CreatedAt,
                            lastUpdated = b.UpdatedAt,
                            createdBy = b.ReceivedBy ?? "unknown",
                            notes = b.Notes ?? ""
                        };
                    }
                    else if (b.NewStockBatchId.HasValue && newStockBatches.TryGetValue(b.NewStockBatchId.Value, out var newStockBatch))
                    {
                        // NewStockBatch from Phase 0
                        return new
                        {
                            batchId = b.ReceivingBatchId.ToString(),
                            sourceType = (int)b.SourceType,
                            sourceTypeName = b.SourceType.ToString(),
                            documentInfo = new
                            {
                                type = "New Stock Batch",
                                number = newStockBatch.BatchNumber,
                                supplier = newStockBatch.SupplierName ?? "",
                                school = "",
                                emisCode = "",
                                uploadedAt = b.CreatedAt
                            },
                            schoolSupplier = newStockBatch.SupplierName ?? "",
                            status = b.Status.ToString(),
                            deviceCount = b.ExpectedCount,
                            actualCount = b.ActualCount,
                            createdAt = b.CreatedAt,
                            lastUpdated = b.UpdatedAt,
                            createdBy = b.ReceivedBy ?? "unknown",
                            notes = b.Notes ?? ""
                        };
                    }
                }

                // For RnR or unknown NewStock
                if (b.CollectionSlip != null)
                {
                    return new
                    {
                        batchId = b.ReceivingBatchId.ToString(),
                        sourceType = (int)b.SourceType,
                        sourceTypeName = b.SourceType.ToString(),
                        documentInfo = new
                        {
                            type = b.SourceType == ReceivingSourceType.RnrEmergency ? "Emergency Slip" : "Collection Slip",
                            number = b.CollectionSlip.SlipNumber,
                            supplier = "",
                            school = b.CollectionSlip.SchoolName ?? "",
                            emisCode = b.CollectionSlip.EmisCode ?? "",
                            uploadedAt = b.CreatedAt
                        },
                        schoolSupplier = b.CollectionSlip.SchoolName ?? "",
                        status = b.Status.ToString(),
                        deviceCount = b.ExpectedCount,
                        actualCount = b.ActualCount,
                        createdAt = b.CreatedAt,
                        lastUpdated = b.UpdatedAt,
                        createdBy = b.ReceivedBy ?? "unknown",
                        notes = b.Notes ?? ""
                    };
                }

                // Fallback for unknown
                return new
                {
                    batchId = b.ReceivingBatchId.ToString(),
                    sourceType = (int)b.SourceType,
                    sourceTypeName = b.SourceType.ToString(),
                    documentInfo = new { type = "Unknown", number = "", supplier = "", school = "", emisCode = "", uploadedAt = b.CreatedAt },
                    schoolSupplier = "",
                    status = b.Status.ToString(),
                    deviceCount = b.ExpectedCount,
                    actualCount = b.ActualCount,
                    createdAt = b.CreatedAt,
                    lastUpdated = b.UpdatedAt,
                    createdBy = b.ReceivedBy ?? "unknown",
                    notes = b.Notes ?? ""
                };
            }).Cast<object>().ToList();
        }

        public async Task<ReceivingBatchDto?> GetReceivingBatchAsync(Guid batchId, CancellationToken ct = default)
        {
            var batch = await _db.ReceivingBatches
                .Include(b => b.Order)
                .Include(b => b.CollectionSlip)
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == batchId, ct);

            if (batch == null) return null;

            return new ReceivingBatchDto(
                batch.ReceivingBatchId,
                batch.SourceType,
                batch.SourceType.ToString(),
                batch.OrderId,
                batch.Order?.OrderNumber,
                batch.NewStockBatchId,
                batch.CollectionSlipId,
                batch.CollectionSlip?.SlipNumber,
                batch.CollectionSlip?.SchoolName,
                batch.Status,
                batch.Status.ToString(),
                batch.ReceivedBy,
                batch.ReceivedDate,
                batch.Items.Count,
                batch.CreatedAt
            );
        }
    }
}
