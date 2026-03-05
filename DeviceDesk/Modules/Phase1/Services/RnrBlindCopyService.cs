using DeviceDesk.Modules.Phase0.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfDocument = QuestPDF.Fluent.Document;

namespace DeviceDesk.Modules.Phase1.Services
{
    /// <summary>
    /// Service for generating R&R blind copy PDFs (PAN copy with hidden quantities)
    /// </summary>
    public class RnrBlindCopyService
    {
        private readonly RnrBatchService _rnrBatchService;

        public RnrBlindCopyService(RnrBatchService rnrBatchService)
        {
            _rnrBatchService = rnrBatchService;
            
            // Set QuestPDF license (Community license for non-commercial use)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// Generate blind copy PDF for R&R batch (hides quantities)
        /// </summary>
        public async Task<byte[]> GenerateRnrBlindCopyPdfAsync(Guid rnrBatchId, CancellationToken ct = default)
        {
            // Fetch R&R batch from Phase 0
            var batch = await _rnrBatchService.GetBatchDetailsAsync(rnrBatchId, ct);
            
            if (batch == null)
                throw new InvalidOperationException($"R&R batch {rnrBatchId} not found in Phase 0.");

            var document = PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Element(content => ComposeHeader(content, batch));
                    page.Content().Element(content => ComposeContent(content, batch));
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, RnrBatchDetailsDto batch)
        {
            container.Column(column =>
            {
                column.Item().BorderBottom(2).BorderColor(Colors.Green.Darken2).PaddingBottom(10).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("R&R BLIND COPY - PAN DOCUMENT")
                            .FontSize(18)
                            .Bold()
                            .FontColor(Colors.Green.Darken2);
                        col.Item().Text("Retention & Retrieval - Physical Count Only")
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

                column.Item().PaddingTop(5).Text("⚠️ CONFIDENTIAL: Quantities intentionally hidden for blind verification")
                    .FontSize(9)
                    .Italic()
                    .FontColor(Colors.Red.Darken1);
            });
        }

        private void ComposeContent(IContainer container, RnrBatchDetailsDto batch)
        {
            container.PaddingVertical(20).Column(column =>
            {
                // Batch Information
                column.Item().PaddingBottom(15).Element(c => ComposeBatchInfo(c, batch));

                // Device List (without quantities)
                column.Item().Element(c => ComposeDeviceList(c, batch));

                // Signature Section
                column.Item().PaddingTop(30).Element(ComposeSignatureSection);
            });
        }

        private void ComposeBatchInfo(IContainer container, RnrBatchDetailsDto batch)
        {
            container.Background(Colors.Grey.Lighten3).Padding(10).Column(column =>
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
                    });
                    
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("School: ").SemiBold();
                            text.Span(batch.SchoolName ?? "N/A");
                        });
                        col.Item().Text(text =>
                        {
                            text.Span("Created: ").SemiBold();
                            text.Span(batch.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
                        });
                    });
                });
            });
        }

        private void ComposeDeviceList(IContainer container, RnrBatchDetailsDto batch)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(10).Text("Device List (Quantities Hidden)")
                    .FontSize(14)
                    .Bold();

                column.Item().Table(table =>
                {
                    // Define columns - NO Quantity column for blind copy
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40); // #
                        columns.RelativeColumn(3); // Brand
                        columns.RelativeColumn(3); // Model
                        columns.RelativeColumn(2); // Device Type
                        columns.RelativeColumn(4); // Description
                        columns.RelativeColumn(2); // Scan Count (empty for officer to fill)
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("#").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Brand").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Model").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Type").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Description").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Scanned").FontColor(Colors.White).Bold();
                    });

                    // Items
                    int index = 1;
                    foreach (var item in batch.Items)
                    {
                        var bgColor = index % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                        table.Cell().Background(bgColor).Padding(5).Text(index.ToString());
                        table.Cell().Background(bgColor).Padding(5).Text(item.Brand ?? "");
                        table.Cell().Background(bgColor).Padding(5).Text(item.Model ?? "");
                        table.Cell().Background(bgColor).Padding(5).Text(item.DeviceType ?? "");
                        table.Cell().Background(bgColor).Padding(5).Text(item.Description ?? "");
                        table.Cell().Background(bgColor).Padding(5).Text("_____"); // Empty for manual count

                        index++;
                    }
                });

                // Note about quantities
                column.Item().PaddingTop(10).Text("Note: Expected quantities are hidden. Please scan all devices and record actual counts.")
                    .FontSize(9)
                    .Italic()
                    .FontColor(Colors.Grey.Darken1);
            });
        }

        private void ComposeSignatureSection(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(10);
                
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Receiving Officer").FontSize(10).SemiBold();
                        col.Item().PaddingTop(30).BorderBottom(1).BorderColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(5).Text("Signature & Date").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(50); // Spacing

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Supervisor/Witness").FontSize(10).SemiBold();
                        col.Item().PaddingTop(30).BorderBottom(1).BorderColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(5).Text("Signature & Date").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });

                column.Item().PaddingTop(20).Text("Instructions: 1) Scan all devices physically present. 2) Record scanned counts. 3) Click 'Done' when complete. 4) System will verify against expected quantities.")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text($"R&R Blind Copy - Page {{CurrentPageNumber}} of {{TotalPages}} - Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .FontSize(8)
                .FontColor(Colors.Grey.Medium);
        }
    }
}
