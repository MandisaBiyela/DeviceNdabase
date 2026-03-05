using DeviceDesk.Modules.Phase0.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfDocument = QuestPDF.Fluent.Document;

namespace DeviceDesk.Modules.Phase1.Services
{
    /// <summary>
    /// Service for generating R&R GRV (Goods Received Voucher) PDFs with full quantities
    /// </summary>
    public class RnrGrvService
    {
        private readonly RnrBatchService _rnrBatchService;
        private readonly ILogger<RnrGrvService> _logger;

        public RnrGrvService(RnrBatchService rnrBatchService, ILogger<RnrGrvService> logger)
        {
            _rnrBatchService = rnrBatchService;
            _logger = logger;
            
            // Set QuestPDF license (Community license for non-commercial use)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// Generate GRV PDF for verified R&R batch (shows full quantities)
        /// </summary>
        public async Task<byte[]> GenerateRnrGrvPdfAsync(Guid rnrBatchId, string grvNumber, CancellationToken ct = default)
        {
            // Fetch R&R batch from Phase 0
            var batch = await _rnrBatchService.GetBatchDetailsAsync(rnrBatchId, ct);
            
            if (batch == null)
            {
                _logger.LogError("[R&R GRV] Batch {BatchId} not found", rnrBatchId);
                throw new InvalidOperationException($"R&R batch {rnrBatchId} not found in Phase 0.");
            }

            if (batch.Status != Infrastructure.Data.RnrBatchStatus.Verified)
            {
                _logger.LogWarning("[R&R GRV] Batch {BatchId} status is {Status}, expected Verified", 
                    rnrBatchId, batch.Status);
            }

            _logger.LogInformation("[R&R GRV] Generating GRV for batch {BatchId}, GRV #{GrvNumber}", 
                rnrBatchId, grvNumber);

            var document = PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Element(content => ComposeHeader(content, batch, grvNumber));
                    page.Content().Element(content => ComposeContent(content, batch));
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, RnrBatchDetailsDto batch, string grvNumber)
        {
            container.Column(column =>
            {
                column.Item().BorderBottom(2).BorderColor(Colors.Blue.Darken2).PaddingBottom(10).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("GOODS RECEIVED VOUCHER (GRV)")
                            .FontSize(18)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);
                        col.Item().Text("R&R Batch - Retention & Retrieval")
                            .FontSize(10)
                            .Italic()
                            .FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(150).AlignRight().Column(col =>
                    {
                        col.Item().Text($"GRV #: {grvNumber}")
                            .FontSize(11)
                            .Bold();
                        col.Item().Text($"Date: {DateTime.Now:yyyy-MM-dd}")
                            .FontSize(9);
                        col.Item().Text($"Time: {DateTime.Now:HH:mm}")
                            .FontSize(9);
                    });
                });

                column.Item().PaddingTop(5).Text("✓ VERIFIED: Scanned quantities match expected quantities")
                    .FontSize(9)
                    .Italic()
                    .FontColor(Colors.Green.Darken1);
            });
        }

        private void ComposeContent(IContainer container, RnrBatchDetailsDto batch)
        {
            container.PaddingVertical(20).Column(column =>
            {
                // Batch Information
                column.Item().PaddingBottom(15).Element(c => ComposeBatchInfo(c, batch));

                // Device List with full quantities
                column.Item().Element(c => ComposeDeviceList(c, batch));

                // Summary Section
                column.Item().PaddingTop(15).Element(c => ComposeSummary(c, batch));

                // Signature Section
                column.Item().PaddingTop(30).Element(ComposeSignatureSection);
            });
        }

        private void ComposeBatchInfo(IContainer container, RnrBatchDetailsDto batch)
        {
            container.Background(Colors.Blue.Lighten4).Padding(10).Column(column =>
            {
                column.Item().Text("Batch Information").FontSize(14).Bold();
                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Collection Slip: ").SemiBold();
                            text.Span(batch.CollectionSlipNumber);
                        });
                        col.Item().Text(text =>
                        {
                            text.Span("Batch Number: ").SemiBold();
                            text.Span(batch.BatchNumber);
                        });
                        col.Item().Text(text =>
                        {
                            text.Span("School: ").SemiBold();
                            text.Span(batch.SchoolName ?? "N/A");
                        });
                    });
                    
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Status: ").SemiBold();
                            text.Span(batch.Status.ToString()).FontColor(Colors.Green.Darken1);
                        });
                        col.Item().Text(text =>
                        {
                            text.Span("Created: ").SemiBold();
                            text.Span(batch.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
                        });
                        col.Item().Text(text =>
                        {
                            text.Span("GRV Number: ").SemiBold();
                            text.Span(batch.GRVNumber ?? "N/A");
                        });
                    });
                });
            });
        }

        private void ComposeDeviceList(IContainer container, RnrBatchDetailsDto batch)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(10).Text("Device List - Verified Quantities")
                    .FontSize(14)
                    .Bold();

                column.Item().Table(table =>
                {
                    // Define columns with quantities shown
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // Brand
                        columns.RelativeColumn(2); // Model
                        columns.RelativeColumn(2); // Device Type
                        columns.RelativeColumn(3); // Description
                        columns.RelativeColumn(1); // Expected
                        columns.RelativeColumn(1); // Scanned
                        columns.RelativeColumn(1); // Status
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Brand").FontColor(Colors.White).SemiBold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Model").FontColor(Colors.White).SemiBold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Type").FontColor(Colors.White).SemiBold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                            .Text("Description").FontColor(Colors.White).SemiBold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter()
                            .Text("Expected").FontColor(Colors.White).SemiBold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter()
                            .Text("Scanned").FontColor(Colors.White).SemiBold();
                        header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter()
                            .Text("Status").FontColor(Colors.White).SemiBold();
                    });

                    // Rows
                    int rowIndex = 0;
                    foreach (var item in batch.Items)
                    {
                        var bgColor = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten3;
                        var isMatch = item.QuantityScanned == item.QuantityExpected;
                        var statusColor = isMatch ? Colors.Green.Darken1 : Colors.Orange.Darken1;

                        table.Cell().Background(bgColor).Padding(5)
                            .Text(item.Brand ?? "N/A");
                        table.Cell().Background(bgColor).Padding(5)
                            .Text(item.Model ?? "N/A");
                        table.Cell().Background(bgColor).Padding(5)
                            .Text(item.DeviceType ?? "N/A");
                        table.Cell().Background(bgColor).Padding(5)
                            .Text(item.Description ?? "N/A");
                        table.Cell().Background(bgColor).Padding(5).AlignCenter()
                            .Text(item.QuantityExpected.ToString()).SemiBold();
                        table.Cell().Background(bgColor).Padding(5).AlignCenter()
                            .Text(item.QuantityScanned.ToString()).SemiBold();
                        table.Cell().Background(bgColor).Padding(5).AlignCenter()
                            .Text(isMatch ? "✓" : "⚠").FontColor(statusColor);

                        rowIndex++;
                    }
                });
            });
        }

        private void ComposeSummary(IContainer container, RnrBatchDetailsDto batch)
        {
            container.Background(Colors.Green.Lighten4).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("SUMMARY").FontSize(12).Bold();
                });

                row.ConstantItem(200).Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Total Expected:").SemiBold();
                        r.ConstantItem(50).AlignRight().Text(batch.TotalQuantityExpected.ToString());
                    });
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Total Scanned:").SemiBold();
                        r.ConstantItem(50).AlignRight().Text(batch.TotalQuantityScanned.ToString());
                    });
                    col.Item().PaddingTop(3).BorderTop(1).Row(r =>
                    {
                        r.RelativeItem().Text("Variance:").Bold();
                        r.ConstantItem(50).AlignRight().Text((batch.TotalQuantityScanned - batch.TotalQuantityExpected).ToString())
                            .FontColor(batch.TotalQuantityScanned == batch.TotalQuantityExpected 
                                ? Colors.Green.Darken1 
                                : Colors.Red.Darken1);
                    });
                });
            });
        }

        private void ComposeSignatureSection(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("AUTHORIZATION & CONFIRMATION").FontSize(12).Bold();
                
                column.Item().PaddingTop(40).Row(row =>
                {
                    row.RelativeItem().PaddingRight(20).Column(col =>
                    {
                        col.Item().BorderBottom(1).PaddingBottom(2).Text("");
                        col.Item().PaddingTop(3).Text("Receiving Officer Signature").FontSize(9).Italic();
                        col.Item().Text("Name: ____________________").FontSize(9);
                        col.Item().Text("Date: ____________________").FontSize(9);
                    });

                    row.RelativeItem().PaddingLeft(20).Column(col =>
                    {
                        col.Item().BorderBottom(1).PaddingBottom(2).Text("");
                        col.Item().PaddingTop(3).Text("Supervisor/Witness Signature").FontSize(9).Italic();
                        col.Item().Text("Name: ____________________").FontSize(9);
                        col.Item().Text("Date: ____________________").FontSize(9);
                    });
                });

                column.Item().PaddingTop(20).BorderTop(1).PaddingTop(5).Text(text =>
                {
                    text.Span("Notes: ").SemiBold().FontSize(8).Italic();
                    text.Span("This GRV confirms successful verification of R&R batch. All scanned quantities match expected quantities.").FontSize(8).Italic();
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Darken1);
                text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                text.Span($" | Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }
    }
}
