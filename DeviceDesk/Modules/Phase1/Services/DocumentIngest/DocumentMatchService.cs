using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Services.DocumentIngest;

public class DocumentMatchService
{
    private static readonly string[] PoKeys =
    {
        "po_number", "PO Number", "PO #", "Purchase Order", "Purchase Order Number", "P.O.", "Order Number", "order_number"
    };

    private static readonly string[] RefKeys =
    {
        "invoice_number", "Invoice Number", "Invoice No", "batch_number", "Batch Number", "Order Reference",
        "order_reference", "GRV", "grv_number", "Delivery Note", "Waybill"
    };

    private static readonly string[] SchoolKeys = { "School", "School Name", "school_name", "Institution" };
    private static readonly string[] FyKeys = { "Financial Year", "financial_year", "FY", "F.Y." };

    public async Task<DocumentMatchDto> TryMatchAsync(
        Phase1DbContext db,
        DocumentClassificationResult classification,
        CancellationToken ct)
    {
        var docType = classification.DocumentType?.Trim().ToLowerInvariant() ?? "unknown";
        if (docType is "unknown" or "")
            return new DocumentMatchDto { Matched = false };

        if (docType != "procurement_order")
            return new DocumentMatchDto { Matched = false };

        var kf = classification.KeyFields;

        var po = TryGetFirstValue(kf, PoKeys);
        if (!string.IsNullOrWhiteSpace(po))
        {
            var normalized = NormalizeToken(po);
            var order = await db.Orders.AsNoTracking()
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.OrderNumber == po || o.OrderNumber == normalized, ct);
            if (order != null)
                return BuildMatch("po_number", "Orders", order, kf);
        }

        foreach (var refVal in TryGetRefValues(kf))
        {
            if (string.IsNullOrWhiteSpace(refVal)) continue;
            var token = refVal.Trim();
            var order = await db.Orders.AsNoTracking()
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o =>
                    o.OrderNumber == token ||
                    (o.InvoiceNumber != null && o.InvoiceNumber == token), ct);
            if (order != null)
                return BuildMatch("reference", "Orders", order, kf);

            var grv = await db.GoodsReceivedNotes.AsNoTracking()
                .FirstOrDefaultAsync(g => g.GRVNumber == token || g.InvoiceNumber == token || g.OrderNumber == token, ct);
            if (grv != null)
            {
                var batch = await db.ReceivingBatches.AsNoTracking()
                    .Where(b => b.ReceivingBatchId == grv.ReceivingBatchId)
                    .Select(b => b.OrderId)
                    .FirstOrDefaultAsync(ct);
                if (batch != null)
                {
                    var o = await db.Orders.AsNoTracking().Include(x => x.Lines)
                        .FirstOrDefaultAsync(x => x.OrderId == batch, ct);
                    if (o != null)
                        return BuildMatch("grv_reference", "Orders", o, kf);
                }
            }
        }

        var school = TryGetFirstValue(kf, SchoolKeys);
        var fy = TryGetFirstValue(kf, FyKeys);
        if (!string.IsNullOrWhiteSpace(school))
        {
            var slip = await db.CollectionSlips.AsNoTracking()
                .Where(s => s.SchoolName.Contains(school.Trim()))
                .OrderBy(s => s.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (slip != null)
            {
                var q =
                    from b in db.ReceivingBatches.AsNoTracking()
                    join o in db.Orders.AsNoTracking() on b.OrderId equals o.OrderId
                    where b.CollectionSlipId == slip.CollectionSlipId && b.OrderId != null
                    select new { b, o };
                if (!string.IsNullOrWhiteSpace(fy))
                {
                    var fyTrim = fy.Trim();
                    q = q.Where(x =>
                        (x.o.Notes != null && x.o.Notes.Contains(fyTrim)) ||
                        x.o.OrderNumber.Contains(fyTrim));
                }

                var orderId = await q
                    .OrderByDescending(x => x.b.CreatedAt)
                    .Select(x => (Guid?)x.o.OrderId)
                    .FirstOrDefaultAsync(ct);
                if (orderId != null)
                {
                    var o = await db.Orders.AsNoTracking().Include(x => x.Lines)
                        .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
                    if (o != null)
                        return BuildMatch("school_financial_year", "Orders", o, kf);
                }
            }
        }

        return new DocumentMatchDto { Matched = false };
    }

    private static DocumentMatchDto BuildMatch(string method, string table, Order order, IReadOnlyDictionary<string, string> keyFields)
    {
        var current = OrderSnapshot.From(order);
        var proposed = current.ApplyKeyFields(keyFields);
        return new DocumentMatchDto
        {
            Matched = true,
            MatchMethod = method,
            MatchedTable = table,
            MatchedRecordId = order.OrderId,
            CurrentSnapshot = current,
            ProposedSnapshot = proposed
        };
    }

    private static string? TryGetFirstValue(IReadOnlyDictionary<string, string> kf, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            foreach (var pair in kf)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pair.Value))
                    return pair.Value.Trim();
            }
        }

        return null;
    }

    private static IEnumerable<string> TryGetRefValues(IReadOnlyDictionary<string, string> kf)
    {
        foreach (var key in RefKeys)
        {
            foreach (var pair in kf)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pair.Value))
                    yield return pair.Value.Trim();
            }
        }
    }

    private static string NormalizeToken(string po) => po.Trim();

    private sealed record OrderLineSnapshot(Guid OrderLineId, string? Brand, string? Model, int QuantityOrdered, int QuantityReceived, string? Description);

    private sealed record OrderSnapshot(
        Guid OrderId,
        string OrderNumber,
        string? InvoiceNumber,
        string? SupplierName,
        DateTimeOffset OrderDate,
        DateTimeOffset? DeliveryDate,
        OrderStatus Status,
        string? Notes,
        IReadOnlyList<OrderLineSnapshot> Lines)
    {
        public static OrderSnapshot From(Order o) => new(
            o.OrderId,
            o.OrderNumber,
            o.InvoiceNumber,
            o.SupplierName,
            o.OrderDate,
            o.DeliveryDate,
            o.Status,
            o.Notes,
            o.Lines.Select(l => new OrderLineSnapshot(l.OrderLineId, l.Brand, l.Model, l.QuantityOrdered, l.QuantityReceived, l.Description)).ToList());

        public OrderSnapshot ApplyKeyFields(IReadOnlyDictionary<string, string> kf)
        {
            var inv = TryGet(kf, "invoice_number", "Invoice Number", "Invoice No") ?? InvoiceNumber;
            var sup = TryGet(kf, "supplier", "Supplier", "Supplier Name") ?? SupplierName;
            var notes = TryGet(kf, "notes", "Notes", "Remarks") ?? Notes;
            var lines = Lines.ToList();
            if (lines.Count == 1)
            {
                var l0 = lines[0];
                var qtyTxt = TryGet(kf, "Qty", "Quantity", "quantity", "Quantity Received");
                int? qty = null;
                if (!string.IsNullOrWhiteSpace(qtyTxt) && int.TryParse(qtyTxt, out var q)) qty = q;
                var desc = TryGet(kf, "Item", "Description", "item", "description") ?? l0.Description;
                var delivery = TryGet(kf, "delivery_status", "Delivery Status", "Status");
                var lineDesc = delivery != null ? $"{desc ?? ""} | Status: {delivery}".Trim() : desc;
                lines[0] = new OrderLineSnapshot(l0.OrderLineId, l0.Brand, l0.Model, l0.QuantityOrdered, qty ?? l0.QuantityReceived, lineDesc ?? l0.Description);
            }

            return this with
            {
                InvoiceNumber = inv,
                SupplierName = sup,
                Notes = notes,
                Lines = lines
            };
        }

        private static string? TryGet(IReadOnlyDictionary<string, string> kf, params string[] keys)
        {
            foreach (var k in keys)
            {
                foreach (var p in kf)
                    if (string.Equals(p.Key, k, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.Value))
                        return p.Value.Trim();
            }

            return null;
        }
    }
}
