using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfDocument = QuestPDF.Fluent.Document;

namespace DeviceDesk.Modules.Phase1.Services
{
    public class GRVService
    {
        private readonly Phase1DbContext _db;
        private readonly InventoryIntegrationService _inventory;
        // private readonly OrderIntegrationService _orderIntegration; // Commented out - using NewStockBatch workflow now
        private readonly ILogger<GRVService> _logger;

        public GRVService(
            Phase1DbContext db, 
            InventoryIntegrationService inventory,
            // OrderIntegrationService orderIntegration, // Commented out
            ILogger<GRVService> logger)
        {
            _db = db;
            _inventory = inventory;
            // _orderIntegration = orderIntegration; // Commented out
            _logger = logger;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<GRVDto> GenerateGRVAsync(Guid receivingBatchId, CancellationToken ct = default)
        {
            var batch = await _db.ReceivingBatches
                .Include(b => b.Order)
                .Include(b => b.CollectionSlip)
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == receivingBatchId, ct);

            if (batch == null)
                throw new InvalidOperationException("Receiving batch not found.");

            if (batch.Status != ReceivingBatchStatus.Verified)
                throw new InvalidOperationException("Batch must be verified before generating GRV.");

            if (batch.GRV != null)
                throw new InvalidOperationException("GRV already exists for this batch.");

            // For RnR batches, ensure ActualCount is the distinct scanned count excluding duplicates
            List<ReceivingBatchScan> rnrScans = new();
            Dictionary<string, RnrExpectedItem> expectedItemsMap = new();
            if (batch.SourceType is ReceivingSourceType.RnrNormal or ReceivingSourceType.RnrEmergency)
            {
                rnrScans = await _db.ReceivingBatchScans
                    .Where(s => s.BatchId == receivingBatchId && s.Status != RnrScanStatus.Duplicate)
                    .OrderBy(s => s.ScannedAt)
                    .ToListAsync(ct);
                batch.ActualCount = rnrScans.Count;
                
                // Load expected items to get DeviceName + Model for GRV display
                var expectedItems = await _db.RnrExpectedItems
                    .Where(e => e.BatchId == receivingBatchId)
                    .ToListAsync(ct);
                
                foreach (var item in expectedItems)
                {
                    if (!string.IsNullOrEmpty(item.Serial))
                        expectedItemsMap[item.Serial] = item;
                }
            }

            // Generate GRV number
            var grvNumber = await GenerateGRVNumberAsync(ct);

            var grv = new GoodsReceivedNote
            {
                ReceivingBatchId = receivingBatchId,
                GRVNumber = grvNumber,
                GRVDate = DateTimeOffset.UtcNow,
                SupplierName = batch.SourceType == ReceivingSourceType.NewStock
                    ? batch.Order?.SupplierName
                    : batch.CollectionSlip?.SchoolName,
                OrderNumber = batch.Order?.OrderNumber,
                InvoiceNumber = batch.Order?.InvoiceNumber,
                TotalQuantity = batch.ActualCount,
                ReceivedBy = batch.ReceivedBy,
                VerifiedBy = batch.VerifiedBy
            };

            // Generate PDF
            grv.PdfData = GenerateGRVPdf(grv, batch, rnrScans, expectedItemsMap);

            _db.GoodsReceivedNotes.Add(grv);

            // Update batch status
            batch.Status = ReceivingBatchStatus.GRVIssued;
            batch.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            // Commented out - using NewStockBatch workflow now
            // // CRITICAL: Sync back to Phase 0 Order
            // if (batch.OrderId.HasValue)
            // {
            //     try
            //     {
            //         await _orderIntegration.UpdateOrderReceivedQuantitiesAsync(
            //             batch.OrderId.Value,
            //             batch.ActualCount,
            //             grvNumber,
            //             ct);
            //         
            //         _logger.LogInformation(
            //             "[GRV → Phase 0] Order {OrderId} updated with {Quantity} received devices, GRV {GRVNumber}",
            //             batch.OrderId.Value, batch.ActualCount, grvNumber);
            //     }
            //     catch (Exception ex)
            //     {
            //         _logger.LogError(ex, 
            //             "[GRV → Phase 0] Failed to sync GRV {GRVNumber} to Order {OrderId}", 
            //             grvNumber, batch.OrderId.Value);
            //     }
            // }

            // CRITICAL: Transfer devices to Phase 0 inventory
            try
            {
                var transferredCount = await _inventory.TransferToInventoryAsync(receivingBatchId, ct);
                // Batch status updated to Completed in TransferToInventoryAsync
            }
            catch (Exception ex)
            {
                // Log error but don't fail GRV generation
                // In production, this should be logged and retried
                _logger.LogWarning(ex, "Failed to transfer devices to inventory for batch {BatchId}", receivingBatchId);
            }

            return new GRVDto(
                grv.GRVId,
                grv.GRVNumber,
                grv.GRVDate,
                grv.SupplierName,
                grv.OrderNumber,
                grv.TotalQuantity,
                grv.ReceivedBy,
                grv.VerifiedBy
            );
        }

        private async Task<string> GenerateGRVNumberAsync(CancellationToken ct)
        {
            var year = DateTime.Now.Year;
            var prefix = $"GRV-{year}-";
            
            var lastGRV = await _db.GoodsReceivedNotes
                .Where(g => g.GRVNumber.StartsWith(prefix))
                .OrderByDescending(g => g.GRVNumber)
                .FirstOrDefaultAsync(ct);

            int nextNumber = 1;
            if (lastGRV != null)
            {
                var lastNumberStr = lastGRV.GRVNumber.Substring(prefix.Length);
                if (int.TryParse(lastNumberStr, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D5}";
        }

        private byte[] GenerateGRVPdf(GoodsReceivedNote grv, ReceivingBatch batch, List<ReceivingBatchScan> rnrScans, Dictionary<string, RnrExpectedItem> expectedItemsMap)
        {
            var document = PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(c => ComposeHeader(c, grv));
                    page.Content().Element(c => ComposeContent(c, grv, batch, rnrScans, expectedItemsMap));
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ").FontSize(9);
                        text.CurrentPageNumber().FontSize(9);
                    });
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, GoodsReceivedNote grv)
        {
            container.Column(column =>
            {
                column.Item().BorderBottom(2).BorderColor(Colors.Green.Darken2).PaddingBottom(10).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("GOODS RECEIVED NOTE (GRV)")
                            .FontSize(20).Bold().FontColor(Colors.Green.Darken2);
                        col.Item().Text($"GRV Number: {grv.GRVNumber}")
                            .FontSize(12).Bold();
                    });

                    row.ConstantItem(120).AlignRight().Column(col =>
                    {
                        col.Item().Text($"Date: {grv.GRVDate:yyyy-MM-dd}").FontSize(10);
                        col.Item().Text($"Time: {grv.GRVDate:HH:mm}").FontSize(10);
                    });
                });
            });
        }

        private void ComposeContent(IContainer container, GoodsReceivedNote grv, ReceivingBatch batch, List<ReceivingBatchScan> rnrScans, Dictionary<string, RnrExpectedItem> expectedItemsMap)
        {
            container.PaddingVertical(20).Column(column =>
            {
                // Supplier/Source Info
                column.Item().PaddingBottom(15).Background(Colors.Grey.Lighten4).Padding(10).Column(infoCol =>
                {
                    infoCol.Item().PaddingBottom(5).Text("Delivery Information").FontSize(14).Bold();
                    if (batch.SourceType == ReceivingSourceType.NewStock)
                    {
                        if (!string.IsNullOrEmpty(grv.SupplierName))
                        {
                            infoCol.Item().Row(row =>
                            {
                                row.ConstantItem(120).Text("Supplier:").Bold();
                                row.RelativeItem().Text(grv.SupplierName);
                            });
                        }

                        if (!string.IsNullOrEmpty(grv.OrderNumber))
                        {
                            infoCol.Item().Row(row =>
                            {
                                row.ConstantItem(120).Text("Order Number:").Bold();
                                row.RelativeItem().Text(grv.OrderNumber);
                            });
                        }

                        if (!string.IsNullOrEmpty(grv.InvoiceNumber))
                        {
                            infoCol.Item().Row(row =>
                            {
                                row.ConstantItem(120).Text("Invoice Number:").Bold();
                                row.RelativeItem().Text(grv.InvoiceNumber);
                            });
                        }
                    }
                    else
                    {
                        infoCol.Item().Row(row => {
                            row.ConstantItem(120).Text("School:").Bold();
                            row.RelativeItem().Text(batch.CollectionSlip?.SchoolName ?? "N/A");
                        });
                        infoCol.Item().Row(row => {
                            row.ConstantItem(120).Text("EMIS Code:").Bold();
                            row.RelativeItem().Text(batch.CollectionSlip?.EmisCode ?? "N/A");
                        });
                        infoCol.Item().Row(row => {
                            row.ConstantItem(120).Text("Collection Slip:").Bold();
                            row.RelativeItem().Text(batch.CollectionSlip?.SlipNumber ?? "N/A");
                        });
                    }
                });

                // Quantity Summary
                column.Item().PaddingBottom(15).Border(1).BorderColor(Colors.Grey.Medium).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Total Quantity Received").Bold();
                        col.Item().Text(grv.TotalQuantity.ToString()).FontSize(24).Bold().FontColor(Colors.Green.Darken1);
                    });

                    if (batch.HasVariance)
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Variance").Bold().FontColor(Colors.Red.Medium);
                            col.Item().Text(batch.VarianceCount.ToString()).FontSize(20).Bold().FontColor(Colors.Red.Medium);
                        });
                    }
                });

                // Device Line Items
                column.Item().PaddingTop(15).Element(c => ComposeDeviceTable(c, batch, rnrScans, expectedItemsMap));

                // Verification Details
                column.Item().PaddingBottom(15).Column(verCol =>
                {
                    verCol.Item().PaddingBottom(5).Text("Verification Details").FontSize(14).Bold();
                    
                    verCol.Item().Row(row =>
                    {
                        row.ConstantItem(150).Text("Received By:").Bold();
                        row.RelativeItem().Text(grv.ReceivedBy ?? "N/A");
                    });

                    verCol.Item().Row(row =>
                    {
                        row.ConstantItem(150).Text("Verified By:").Bold();
                        row.RelativeItem().Text(grv.VerifiedBy ?? "N/A");
                    });

                    if (!string.IsNullOrEmpty(batch.ScanningOfficer))
                    {
                        verCol.Item().Row(row =>
                        {
                            row.ConstantItem(150).Text("Scanning Officer:").Bold();
                            row.RelativeItem().Text(batch.ScanningOfficer);
                        });
                    }
                });

                // Variance Info (if applicable)
                if (batch.HasVariance && !string.IsNullOrEmpty(batch.VarianceReason))
                {
                    column.Item().Background(Colors.Yellow.Lighten3).Padding(10).Column(varCol =>
                    {
                        varCol.Item().Text("Variance Information").Bold().FontColor(Colors.Red.Darken1);
                        varCol.Item().Text($"Reason: {batch.VarianceReason}").FontSize(10);
                        
                        if (!string.IsNullOrEmpty(batch.SupervisorApprovedBy))
                        {
                            varCol.Item().Text($"Approved By: {batch.SupervisorApprovedBy}").FontSize(10).Bold();
                        }
                    });
                }

                // Signatures
                column.Item().PaddingTop(40).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().BorderTop(1).PaddingTop(5).Text("Authorized Signature");
                        col.Item().PaddingTop(5).Text("Date: _________________").FontSize(9);
                    });

                    row.ConstantItem(40);

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().BorderTop(1).PaddingTop(5).Text("Finance Approval");
                        col.Item().PaddingTop(5).Text("Date: _________________").FontSize(9);
                    });
                });
            });
        }

        private void ComposeDeviceTable(IContainer container, ReceivingBatch batch, List<ReceivingBatchScan> rnrScans, Dictionary<string, RnrExpectedItem> expectedItemsMap)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(5).Text("Device Line Items").FontSize(14).Bold();

                column.Item().Border(1).BorderColor(Colors.Grey.Medium).Column(table =>
                {
                    // Header
                    table.Item().Background(Colors.Grey.Lighten3).Padding(5).Row(header =>
                    {
                        header.ConstantItem(40).Text("#").Bold();
                        header.RelativeItem().Text("Serial Number").Bold();
                        header.RelativeItem().Text("Model/Info").Bold();
                        if (batch.SourceType != ReceivingSourceType.NewStock)
                        {
                            header.RelativeItem().Text("Status").Bold();
                        }
                    });

                    // Body
                    var index = 1;
                    if (batch.SourceType is ReceivingSourceType.RnrNormal or ReceivingSourceType.RnrEmergency)
                    {
                        foreach (var item in rnrScans)
                        {
                            // Get device info from expected items (DeviceName + Model)
                            string deviceInfo = "N/A";
                            if (expectedItemsMap.TryGetValue(item.Serial, out var expected))
                            {
                                var parts = new List<string>();
                                if (!string.IsNullOrWhiteSpace(expected.DeviceName))
                                    parts.Add(expected.DeviceName);
                                if (!string.IsNullOrWhiteSpace(expected.Model))
                                    parts.Add(expected.Model);
                                deviceInfo = parts.Count > 0 ? string.Join(" ", parts) : "N/A";
                            }
                            else if (!string.IsNullOrWhiteSpace(item.DeviceInfo))
                            {
                                deviceInfo = item.DeviceInfo;
                            }
                            
                            table.Item().Padding(5)
                                .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Row(row =>
                                {
                                    row.ConstantItem(40).Text(index++.ToString());
                                    row.RelativeItem().Text(item.Serial);
                                    row.RelativeItem().Text(deviceInfo);
                                    row.RelativeItem().Text(item.Status.ToString());
                                });
                        }
                    }
                    else // NewStock
                    {
                        foreach (var item in batch.Items.OrderBy(i => i.SerialNumber))
                        {
                            table.Item().Padding(5)
                                .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Row(row =>
                                {
                                    row.ConstantItem(40).Text(index++.ToString());
                                    row.RelativeItem().Text(item.SerialNumber ?? item.IMEI);
                                    row.RelativeItem().Text($"{item.Brand} {item.Model}");
                                });
                        }
                    }

                    if (index == 1) // No items
                    {
                        table.Item().Padding(10).Text("No devices found for this batch.").Italic();
                    }
                });
            });
        }

        public async Task<byte[]?> GetGRVPdfAsync(Guid grvId, CancellationToken ct = default)
        {
            var grv = await _db.GoodsReceivedNotes.FindAsync(new object[] { grvId }, ct);
            return grv?.PdfData;
        }
    }
}
