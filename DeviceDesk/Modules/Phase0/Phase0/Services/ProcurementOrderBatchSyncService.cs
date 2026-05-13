using System.Text.Json;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase0.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase0.Services
{
    /// <summary>
    /// Bridge between Phase 0 (procurement orders) and Phase 1 (new-stock receiving).
    ///
    /// Whenever a <see cref="ProcurementOrder"/> is created or updated (either via the
    /// Phase 0 form or via the document-ingest path) we mirror the device line items
    /// into a <see cref="NewStockBatch"/> so the receiving side never starts blank.
    ///
    /// One NewStockBatch per ProcurementOrder. Items are grouped by Brand+Model+DeviceType
    /// across all schools and the per-school allocation is stored in
    /// <see cref="NewStockBatchItem.SchoolBreakdownJson"/> for downstream UIs.
    /// </summary>
    public class ProcurementOrderBatchSyncService
    {
        private static readonly JsonSerializerOptions BreakdownJsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly DeviceDeskDbContext _db;
        private readonly ILogger<ProcurementOrderBatchSyncService> _logger;

        public ProcurementOrderBatchSyncService(
            DeviceDeskDbContext db,
            ILogger<ProcurementOrderBatchSyncService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Idempotently create or refresh the NewStockBatch (+ items) for a procurement order.
        /// Returns the batch id (existing or new), or null if the order has no orderable lines.
        /// </summary>
        public async Task<Guid?> SyncBatchForOrderAsync(
            Guid procurementOrderId,
            string createdBy,
            CancellationToken ct = default)
        {
            var order = await _db.ProcurementOrders
                .Include(o => o.Schools)
                .ThenInclude(s => s.Items)
                .FirstOrDefaultAsync(o => o.ProcurementOrderId == procurementOrderId, ct);

            if (order == null)
            {
                _logger.LogWarning("[BatchSync] ProcurementOrder {Id} not found; skipping batch sync.", procurementOrderId);
                return null;
            }

            // Materialise all (schoolName, item) pairs and skip non-orderable rows
            var rows = order.Schools
                .SelectMany(s => s.Items.Select(i => new
                {
                    SchoolName = s.SchoolName?.Trim() ?? string.Empty,
                    Item = i
                }))
                .Where(x => x.Item.QtyOrdered > 0)
                .ToList();

            if (rows.Count == 0)
            {
                _logger.LogInformation("[BatchSync] Order {Po} has no orderable items; no batch created.", order.PoNumber);
                return order.NewStockBatchId;
            }

            var groups = rows
                .GroupBy(r => new
                {
                    Brand = NormaliseKey(r.Item.Brand),
                    Model = NormaliseKey(r.Item.Model),
                    DeviceType = NormaliseKey(r.Item.DeviceType),
                    Description = NormaliseKey(r.Item.Description)
                })
                .ToList();

            var totalExpected = groups.Sum(g => g.Sum(r => r.Item.QtyOrdered));

            // Look up an existing batch first via the back-link, then by ProcurementOrderId
            var batch = order.NewStockBatchId.HasValue
                ? await _db.NewStockBatches
                    .Include(b => b.Items)
                    .FirstOrDefaultAsync(b => b.BatchId == order.NewStockBatchId.Value, ct)
                : null;

            batch ??= await _db.NewStockBatches
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.ProcurementOrderId == order.ProcurementOrderId, ct);

            var isNew = batch == null;
            if (batch == null)
            {
                batch = new NewStockBatch
                {
                    BatchId = Guid.NewGuid(),
                    BatchNumber = await GenerateBatchNumberAsync(ct),
                    Status = NewStockBatchStatus.PendingScan,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy
                };
                _db.NewStockBatches.Add(batch);
            }

            // Always refresh PO header fields (they may have been edited on the order)
            batch.ProcurementOrderId = order.ProcurementOrderId;
            batch.PoNumber = order.PoNumber;
            batch.ProjectName = order.ProjectName;
            batch.FinancialYear = order.FinancialYear;
            batch.SupplierName = order.SupplierName;
            batch.ExpectedDeliveryDate = order.ExpectedDeliveryDate?.UtcDateTime;
            batch.TotalQuantityExpected = totalExpected;

            // Do not regress a batch that has already moved past PendingScan.
            if (batch.Status == NewStockBatchStatus.PendingScan || isNew)
            {
                batch.Status = NewStockBatchStatus.PendingScan;
            }

            // Replace the item lines wholesale — simpler and safer for a PendingScan batch.
            if (!isNew)
            {
                _db.NewStockBatchItems.RemoveRange(batch.Items);
                batch.Items.Clear();
            }

            foreach (var g in groups)
            {
                var qty = g.Sum(r => r.Item.QtyOrdered);
                var unitPrice = g.Select(r => r.Item.UnitPrice).FirstOrDefault();

                var schoolBreakdown = g
                    .GroupBy(r => r.SchoolName)
                    .Select(sg => new SchoolBreakdownEntry
                    {
                        SchoolName = sg.Key,
                        QtyOrdered = sg.Sum(r => r.Item.QtyOrdered),
                        DeliveryStatus = sg.First().Item.DeliveryStatus.ToString()
                    })
                    .ToList();

                batch.Items.Add(new NewStockBatchItem
                {
                    ItemId = Guid.NewGuid(),
                    BatchId = batch.BatchId,
                    Brand = NullIfEmpty(g.Key.Brand),
                    Model = NullIfEmpty(g.Key.Model),
                    DeviceType = NullIfEmpty(g.Key.DeviceType),
                    Description = NullIfEmpty(g.Key.Description),
                    QuantityExpected = qty,
                    QuantityScanned = 0,
                    UnitPrice = unitPrice,
                    Zone = "New Stock",
                    SchoolBreakdownJson = JsonSerializer.Serialize(schoolBreakdown, BreakdownJsonOpts)
                });
            }

            order.NewStockBatchId = batch.BatchId;
            order.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[BatchSync] {Action} NewStockBatch {Batch} for PO {Po} ({Items} item groups, {Total} devices)",
                isNew ? "Created" : "Updated", batch.BatchNumber, order.PoNumber, groups.Count, totalExpected);

            return batch.BatchId;
        }

        private async Task<string> GenerateBatchNumberAsync(CancellationToken ct)
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"NB-{year}-";
            var last = await _db.NewStockBatches
                .Where(b => b.BatchNumber.StartsWith(prefix))
                .OrderByDescending(b => b.BatchNumber)
                .Select(b => b.BatchNumber)
                .FirstOrDefaultAsync(ct);

            var next = 1;
            if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            {
                next = n + 1;
            }

            return $"{prefix}{next:D5}";
        }

        private static string NormaliseKey(string? s) =>
            string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim();

        private static string? NullIfEmpty(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private sealed class SchoolBreakdownEntry
        {
            public string SchoolName { get; set; } = string.Empty;
            public int QtyOrdered { get; set; }
            public string DeliveryStatus { get; set; } = string.Empty;
        }
    }
}
