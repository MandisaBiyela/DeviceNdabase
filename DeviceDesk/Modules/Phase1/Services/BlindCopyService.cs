using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Modules.Phase0.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfDocument = QuestPDF.Fluent.Document;

namespace DeviceDesk.Modules.Phase1.Services
{
    public class BlindCopyService
    {
        private readonly Phase1DbContext _db;
        private readonly NewStockBatchService _newStockBatchService;

        public BlindCopyService(Phase1DbContext db, NewStockBatchService newStockBatchService)
        {
            _db = db;
            _newStockBatchService = newStockBatchService;
            
            // Set QuestPDF license (Community license for non-commercial use)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<byte[]> GenerateBlindCopyPdfAsync(Guid receivingBatchId, CancellationToken ct = default)
        {
            var batch = await _db.ReceivingBatches
                .Include(b => b.Order)
                    .ThenInclude(o => o!.Lines)
                .Include(b => b.CollectionSlip)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == receivingBatchId, ct);

            if (batch == null)
                throw new InvalidOperationException($"Receiving batch {receivingBatchId} not found.");

            // Fetch NewStockBatch data if Order is null but NewStockBatchId exists (Phase 0 integration)
            Modules.Phase0.Services.NewStockBatchDetailsDto? newStockBatch = null;
            if (batch.SourceType == ReceivingSourceType.NewStock && batch.NewStockBatchId.HasValue && batch.Order == null)
            {
                newStockBatch = await _newStockBatchService.GetBatchDetailsAsync(batch.NewStockBatchId.Value, ct);
            }

            var document = PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(content => ComposeContent(content, batch, newStockBatch));
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().BorderBottom(2).BorderColor(Colors.Blue.Darken2).PaddingBottom(10).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("BLIND COPY - RECEIVING DOCUMENT")
                            .FontSize(18)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);
                        col.Item().Text("For Physical Count & Scanning Only")
                            .FontSize(10)
                            .Italic()
                            .FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(100).AlignRight().Column(col =>
                    {
                        col.Item().Text($"Date: {DateTime.Now:yyyy-MM-dd}")
                            .FontSize(9);
                        col.Item().Text($"Time: {DateTime.Now:HH:mm}")
                            .FontSize(9);
                    });
                });

                column.Item().PaddingTop(5).Text("⚠️ CONFIDENTIAL: Quantities and prices intentionally omitted")
                    .FontSize(9)
                    .Italic()
                    .FontColor(Colors.Red.Darken1);
            });
        }

        private void ComposeContent(IContainer container, ReceivingBatch batch, Modules.Phase0.Services.NewStockBatchDetailsDto? newStockBatch)
        {
            container.PaddingVertical(20).Column(column =>
            {
                // Batch Information
                column.Item().PaddingBottom(15).Element(c => ComposeBatchInfo(c, batch));

                // Source Information
                column.Item().PaddingBottom(15).Element(c => ComposeSourceInfo(c, batch, newStockBatch));

                // Device List
                column.Item().Element(c => ComposeDeviceList(c, batch, newStockBatch));

                // Signature Section
                column.Item().PaddingTop(30).Element(ComposeSignatureSection);
            });
        }

        private void ComposeBatchInfo(IContainer container, ReceivingBatch batch)
        {
            container.Background(Colors.Grey.Lighten3).Padding(10).Column(column =>
            {
                column.Item().Text("Batch Information").FontSize(14).Bold();
                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Batch ID: {batch.ReceivingBatchId}").FontSize(9);
                    row.RelativeItem().Text($"Source Type: {batch.SourceType}").FontSize(9);
                });
                column.Item().Text($"Created: {batch.CreatedAt:yyyy-MM-dd HH:mm}").FontSize(9);
            });
        }

        private void ComposeSourceInfo(IContainer container, ReceivingBatch batch, Modules.Phase0.Services.NewStockBatchDetailsDto? newStockBatch)
        {
            container.Border(1).BorderColor(Colors.Grey.Medium).Padding(10).Column(column =>
            {
                column.Item().PaddingBottom(5).Text("Source Reference").FontSize(12).Bold();

                if (batch.SourceType == ReceivingSourceType.NewStock)
                {
                    // Try Phase 1 Order first, then Phase 0 NewStockBatch
                    if (batch.Order != null)
                    {
                        column.Item().Row(row =>
                        {
                            row.ConstantItem(120).Text("Order Number:").Bold();
                            row.RelativeItem().Text(batch.Order.OrderNumber);
                        });
                        
                        if (!string.IsNullOrEmpty(batch.Order.InvoiceNumber))
                        {
                            column.Item().Row(row =>
                            {
                                row.ConstantItem(120).Text("Invoice Number:").Bold();
                                row.RelativeItem().Text(batch.Order.InvoiceNumber);
                            });
                        }

                        if (!string.IsNullOrEmpty(batch.Order.SupplierName))
                        {
                            column.Item().Row(row =>
                            {
                                row.ConstantItem(120).Text("Supplier:").Bold();
                                row.RelativeItem().Text(batch.Order.SupplierName);
                            });
                        }
                    }
                    else if (newStockBatch != null)
                    {
                        // Phase 0 NewStockBatch data
                        column.Item().Row(row =>
                        {
                            row.ConstantItem(120).Text("Batch Number:").Bold();
                            row.RelativeItem().Text(newStockBatch.BatchNumber);
                        });
                        
                        if (!string.IsNullOrEmpty(newStockBatch.InvoiceNumber))
                        {
                            column.Item().Row(row =>
                            {
                                row.ConstantItem(120).Text("Invoice Number:").Bold();
                                row.RelativeItem().Text(newStockBatch.InvoiceNumber);
                            });
                        }

                        if (!string.IsNullOrEmpty(newStockBatch.SupplierName))
                        {
                            column.Item().Row(row =>
                            {
                                row.ConstantItem(120).Text("Supplier:").Bold();
                                row.RelativeItem().Text(newStockBatch.SupplierName);
                            });
                        }
                    }
                }
                else if (batch.CollectionSlip != null)
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(120).Text("Slip Number:").Bold();
                        row.RelativeItem().Text(batch.CollectionSlip.SlipNumber);
                    });
                    
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(120).Text("School:").Bold();
                        row.RelativeItem().Text($"{batch.CollectionSlip.SchoolName} ({batch.CollectionSlip.EmisCode})");
                    });
                }
            });
        }

        private void ComposeDeviceList(IContainer container, ReceivingBatch batch, Modules.Phase0.Services.NewStockBatchDetailsDto? newStockBatch)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(10).Text("Expected Devices").FontSize(14).Bold();

                // Table header
                column.Item().Border(1).BorderColor(Colors.Grey.Medium).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);  // #
                        columns.RelativeColumn(2);    // Brand
                        columns.RelativeColumn(3);    // Model
                        columns.RelativeColumn(2);    // Notes
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("#").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Brand").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Model").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Notes / Observations").FontColor(Colors.White).Bold();
                    });

                    // Rows - from Order Lines (for New Stock) or NewStockBatch Items
                    if (batch.SourceType == ReceivingSourceType.NewStock)
                    {
                        int rowNum = 1;

                        // Check Phase 1 Order first
                        if (batch.Order?.Lines != null && batch.Order.Lines.Count > 0)
                        {
                            foreach (var line in batch.Order.Lines.OrderBy(l => l.Brand).ThenBy(l => l.Model))
                            {
                                var bgColor = rowNum % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                                table.Cell().Background(bgColor).Padding(5).Text(rowNum.ToString());
                                table.Cell().Background(bgColor).Padding(5).Text(line.Brand ?? "N/A");
                                table.Cell().Background(bgColor).Padding(5).Text(line.Model ?? "N/A");
                                table.Cell().Background(bgColor).Padding(5).Text(""); // Empty for manual notes

                                rowNum++;
                            }
                        }
                        // Then check Phase 0 NewStockBatch
                        else if (newStockBatch?.Items != null && newStockBatch.Items.Count > 0)
                        {
                            foreach (var item in newStockBatch.Items.OrderBy(i => i.Brand).ThenBy(i => i.Model))
                            {
                                var bgColor = rowNum % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                                table.Cell().Background(bgColor).Padding(5).Text(rowNum.ToString());
                                table.Cell().Background(bgColor).Padding(5).Text(item.Brand ?? "N/A");
                                table.Cell().Background(bgColor).Padding(5).Text(item.Model ?? "N/A");
                                table.Cell().Background(bgColor).Padding(5).Text(""); // Empty for manual notes

                                rowNum++;
                            }
                        }
                    }
                    else
                    {
                        // For RnR, show placeholder rows for manual entry
                        for (int i = 1; i <= 20; i++)
                        {
                            var bgColor = i % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                            table.Cell().Background(bgColor).Padding(5).Text(i.ToString());
                            table.Cell().Background(bgColor).Padding(5).Text("");
                            table.Cell().Background(bgColor).Padding(5).Text("");
                            table.Cell().Background(bgColor).Padding(5).Text("");
                        }
                    }
                });

                // Important notes
                column.Item().PaddingTop(10).Background(Colors.Yellow.Lighten3).Padding(8).Column(noteCol =>
                {
                    noteCol.Item().Text("INSTRUCTIONS FOR RECEIVING OFFICER:").Bold().FontSize(10);
                    noteCol.Item().Text("1. Physically count each device and verify model information").FontSize(9);
                    noteCol.Item().Text("2. Scan or record serial numbers/IMEI for each device").FontSize(9);
                    noteCol.Item().Text("3. Note any discrepancies or damage in the Notes column").FontSize(9);
                    noteCol.Item().Text("4. DO NOT refer to quantities - perform independent count").FontSize(9).Bold();
                });
            });
        }

        private void ComposeSignatureSection(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(40).Text(""); // Spacer

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(5)
                            .Text("Receiving Officer Signature");
                        col.Item().PaddingTop(5).Text("Name: _______________________").FontSize(9);
                        col.Item().Text("Date: _______________________").FontSize(9);
                    });

                    row.ConstantItem(40); // Spacer

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(5)
                            .Text("Supervisor Signature");
                        col.Item().PaddingTop(5).Text("Name: _______________________").FontSize(9);
                        col.Item().Text("Date: _______________________").FontSize(9);
                    });
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.Span("DeviceDesk Phase 1 - Main Receiving System | ").FontSize(8).FontColor(Colors.Grey.Medium);
                text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }
    }
}
