using System.Security.Claims;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Middleware;
using DeviceDesk.Modules.Phase0.Models;
using DeviceDesk.Modules.Phase0.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase0.Controllers
{
    [ApiController]
    [Route("api/phase0/orders")]
    [Authorize]
    public class ProcurementOrdersController : ControllerBase
    {
        private readonly DeviceDeskDbContext _db;
        private readonly OrderValidationService _validation;
        private readonly ProcurementOrderExportService _export;
        private readonly CloseOutReportDocxService _closeOutReport;
        private readonly ProcurementOrderBatchSyncService _batchSync;

        public ProcurementOrdersController(
            DeviceDeskDbContext db,
            OrderValidationService validation,
            ProcurementOrderExportService export,
            CloseOutReportDocxService closeOutReport,
            ProcurementOrderBatchSyncService batchSync)
        {
            _db = db;
            _validation = validation;
            _export = export;
            _closeOutReport = closeOutReport;
            _batchSync = batchSync;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProcurementOrderRequest request, CancellationToken ct)
        {
            _validation.ValidateCreateRequest(request);

            var existing = await _db.ProcurementOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PoNumber == request.PoNumber.Trim(), ct);
            if (existing != null)
                throw new ConflictException($"An order with PO Number '{request.PoNumber}' already exists.");

            var order = new ProcurementOrder
            {
                PoNumber = request.PoNumber.Trim(),
                ProjectName = request.ProjectName.Trim(),
                FinancialYear = request.FinancialYear.Trim(),
                TotalOrderValue = request.TotalOrderValue,
                TotalInvoicedToDepartment = request.TotalInvoicedToDepartment,
                TotalPaidByDepartment = request.TotalPaidByDepartment,
                TotalPaidToSuppliers = request.TotalPaidToSuppliers,
                SupplierName = string.IsNullOrWhiteSpace(request.SupplierName) ? null : request.SupplierName.Trim(),
                ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Schools = request.Schools.Select(s => new ProcurementOrderSchool
                {
                    SchoolName = s.SchoolName.Trim(),
                    SchoolSubTotal = s.SchoolSubTotal,
                    Items = s.Items.Select(i => new ProcurementOrderItem
                    {
                        Description = i.Description.Trim(),
                        Brand = NullIfEmpty(i.Brand),
                        Model = NullIfEmpty(i.Model),
                        DeviceType = NullIfEmpty(i.DeviceType),
                        UnitPrice = i.UnitPrice,
                        QtyOrdered = i.QtyOrdered,
                        TotalPrice = i.TotalPrice,
                        DeliveryStatus = i.DeliveryStatus
                    }).ToList()
                }).ToList()
            };

            _db.ProcurementOrders.Add(order);
            await _db.SaveChangesAsync(ct);

            // Mirror into Phase 1 receiving as a PendingScan NewStockBatch so the
            // receiving side never starts blank for this order.
            var createdBy = User?.Identity?.Name
                ?? User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? "phase0-form";
            var batchId = await _batchSync.SyncBatchForOrderAsync(order.ProcurementOrderId, createdBy, ct);

            return CreatedAtAction(nameof(GetById), new { id = order.ProcurementOrderId }, new
            {
                id = order.ProcurementOrderId,
                poNumber = order.PoNumber,
                newStockBatchId = batchId
            });
        }

        private static string? NullIfEmpty(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            var orders = await _db.ProcurementOrders
                .AsNoTracking()
                .Include(o => o.Schools)
                .ThenInclude(s => s.Items)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(ct);

            return Ok(orders.Select(ToOrderDto).ToList());
        }

        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportExcel(CancellationToken ct)
        {
            var orders = await LoadOrdersForExportAsync(ct);
            var bytes = _export.BuildExcel(orders);
            var fileName = $"procurement-orders-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet("export/pdf")]
        public async Task<IActionResult> ExportPdf(CancellationToken ct)
        {
            var orders = await LoadOrdersForExportAsync(ct);
            var bytes = _export.BuildPdf(orders);
            var fileName = $"procurement-orders-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
            return File(bytes, "application/pdf", fileName);
        }

        private Task<List<ProcurementOrder>> LoadOrdersForExportAsync(CancellationToken ct) =>
            _db.ProcurementOrders
                .AsNoTracking()
                .Include(o => o.Schools)
                .ThenInclude(s => s.Items)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(ct);

        [HttpGet("{id:guid}/close-out-report")]
        public async Task<IActionResult> GetCloseOutReport(Guid id, CancellationToken ct)
        {
            var order = await _db.ProcurementOrders
                .AsNoTracking()
                .Include(o => o.Schools)
                .ThenInclude(s => s.Items)
                .FirstOrDefaultAsync(o => o.ProcurementOrderId == id, ct);

            if (order == null)
                throw new NotFoundException("ProcurementOrder", id);

            var bytes = _closeOutReport.BuildDocument(order, DateTime.UtcNow.Date);
            var fileName = CloseOutReportDocxService.BuildFileName(order.PoNumber, order.FinancialYear);
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var order = await _db.ProcurementOrders
                .AsNoTracking()
                .Include(o => o.Schools)
                .ThenInclude(s => s.Items)
                .FirstOrDefaultAsync(o => o.ProcurementOrderId == id, ct);

            if (order == null)
                throw new NotFoundException("ProcurementOrder", id);

            return Ok(ToOrderDto(order));
        }

        private static object ToOrderDto(ProcurementOrder order)
        {
            var schoolsTotal = order.Schools.Sum(s => s.SchoolSubTotal);
            var outstandingBalance = order.TotalInvoicedToDepartment - order.TotalPaidByDepartment;
            var status = schoolsTotal == order.TotalOrderValue && outstandingBalance == 0m
                ? FinancialBalanceStatus.Balanced
                : FinancialBalanceStatus.Outstanding;

            return new
            {
                id = order.ProcurementOrderId,
                poNumber = order.PoNumber,
                projectName = order.ProjectName,
                financialYear = order.FinancialYear,
                supplierName = order.SupplierName,
                expectedDeliveryDate = order.ExpectedDeliveryDate,
                newStockBatchId = order.NewStockBatchId,
                totalOrderValue = order.TotalOrderValue,
                totalInvoicedToDepartment = order.TotalInvoicedToDepartment,
                totalPaidByDepartment = order.TotalPaidByDepartment,
                totalPaidToSuppliers = order.TotalPaidToSuppliers,
                outstandingBalance,
                financialBalanceStatus = status.ToString(),
                createdAt = order.CreatedAt,
                updatedAt = order.UpdatedAt,
                isBalanced = schoolsTotal == order.TotalOrderValue,
                schoolTotals = schoolsTotal,
                timelineNotes = order.TimelineNotes,
                scopeNotes = order.ScopeNotes,
                schools = order.Schools.Select(s => new
                {
                    id = s.ProcurementOrderSchoolId,
                    schoolName = s.SchoolName,
                    schoolSubTotal = s.SchoolSubTotal,
                    items = s.Items.Select(i => new
                    {
                        id = i.ProcurementOrderItemId,
                        description = i.Description,
                        brand = i.Brand,
                        model = i.Model,
                        deviceType = i.DeviceType,
                        unitPrice = i.UnitPrice,
                        qtyOrdered = i.QtyOrdered,
                        totalPrice = i.TotalPrice,
                        deliveryStatus = i.DeliveryStatus.ToString()
                    }).ToList()
                }).ToList()
            };
        }
    }
}
