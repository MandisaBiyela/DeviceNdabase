using DeviceDesk.Infrastructure.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfDocument = QuestPDF.Fluent.Document;

namespace DeviceDesk.Modules.Phase3.Services
{
    public class DispatchDocumentService
    {
        private readonly DeviceDeskDbContext _phase0Db;

        public DispatchDocumentService(DeviceDeskDbContext phase0Db)
        {
            _phase0Db = phase0Db;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<(long podDocId, long dnDocId, string podFileName, string dnFileName)>
            CreatePodAndDeliveryNoteAsync(
                string podNumber,
                string schoolName,
                string stockType,
                string sourceReference,
                IEnumerable<string> deviceSerials,
                CancellationToken ct = default)
        {
            var podPdf = GeneratePodPdf(podNumber, schoolName, stockType, sourceReference, deviceSerials);
            var dnPdf = GenerateDeliveryNotePdf(podNumber, schoolName, stockType, sourceReference, deviceSerials);

            var podDoc = new DeviceDesk.Infrastructure.Data.Document
            {
                DocType = "POD",
                FileName = $"POD-{podNumber}.pdf",
                ContentType = "application/pdf",
                FileData = podPdf
            };
            var dnDoc = new DeviceDesk.Infrastructure.Data.Document
            {
                DocType = "DeliveryNote",
                FileName = $"DeliveryNote-{podNumber}.pdf",
                ContentType = "application/pdf",
                FileData = dnPdf
            };

            _phase0Db.Documents.Add(podDoc);
            _phase0Db.Documents.Add(dnDoc);
            await _phase0Db.SaveChangesAsync(ct);

            return (podDoc.DocumentId, dnDoc.DocumentId, podDoc.FileName, dnDoc.FileName);
        }

        private byte[] GeneratePodPdf(string podNumber, string schoolName, string stockType, string sourceReference, IEnumerable<string> deviceSerials)
        {
            var serialList = deviceSerials.ToList();
            var document = PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PROOF OF DELIVERY")
                                .FontSize(20).Bold().FontColor(Colors.Green.Darken2);
                            col.Item().Text($"POD Number: {podNumber}").Bold();
                        });
                        row.ConstantItem(140).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Date: {DateTime.UtcNow:yyyy-MM-dd}").FontSize(10);
                            col.Item().Text($"Time: {DateTime.UtcNow:HH:mm}").FontSize(10);
                        });
                    });

                    page.Content().PaddingVertical(20).Column(column =>
                    {
                        column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(info =>
                        {
                            info.Item().Text("Delivery Details").Bold();
                            info.Item().Row(r => { r.ConstantItem(140).Text("School:").Bold(); r.RelativeItem().Text(schoolName); });
                            info.Item().Row(r => { r.ConstantItem(140).Text("Stock Type:").Bold(); r.RelativeItem().Text(stockType); });
                            info.Item().Row(r => { r.ConstantItem(140).Text("Reference:").Bold(); r.RelativeItem().Text(sourceReference); });
                        });

                        column.Item().PaddingTop(15).Text("Items").Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40);
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("#").Bold();
                                header.Cell().Text("Serial").Bold();
                            });

                            for (int i = 0; i < serialList.Count; i++)
                            {
                                var s = serialList[i];
                                table.Cell().Text((i + 1).ToString());
                                table.Cell().Text(s);
                            }
                        });

                        column.Item().PaddingTop(20).Text("Received By: ______________________    Date: __________");
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ").FontSize(9);
                        text.CurrentPageNumber().FontSize(9);
                    });
                });
            });
            return document.GeneratePdf();
        }

        private byte[] GenerateDeliveryNotePdf(string podNumber, string schoolName, string stockType, string sourceReference, IEnumerable<string> deviceSerials)
        {
            var serialList = deviceSerials.ToList();
            var document = PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("DELIVERY NOTE")
                                .FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().Text($"Reference: {podNumber}").Bold();
                        });
                        row.ConstantItem(140).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Date: {DateTime.UtcNow:yyyy-MM-dd}").FontSize(10);
                            col.Item().Text($"Time: {DateTime.UtcNow:HH:mm}").FontSize(10);
                        });
                    });

                    page.Content().PaddingVertical(20).Column(column =>
                    {
                        column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(info =>
                        {
                            info.Item().Text("Dispatch Details").Bold();
                            info.Item().Row(r => { r.ConstantItem(140).Text("School:").Bold(); r.RelativeItem().Text(schoolName); });
                            info.Item().Row(r => { r.ConstantItem(140).Text("Stock Type:").Bold(); r.RelativeItem().Text(stockType); });
                            info.Item().Row(r => { r.ConstantItem(140).Text("Source Ref:").Bold(); r.RelativeItem().Text(sourceReference); });
                        });

                        column.Item().PaddingTop(15).Text("Items").Bold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40);
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("#").Bold();
                                header.Cell().Text("Serial").Bold();
                            });

                            for (int i = 0; i < serialList.Count; i++)
                            {
                                var s = serialList[i];
                                table.Cell().Text((i + 1).ToString());
                                table.Cell().Text(s);
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ").FontSize(9);
                        text.CurrentPageNumber().FontSize(9);
                    });
                });
            });
            return document.GeneratePdf();
        }

        public async Task<(long tripDocId, string fileName)> CreateTripSheetAsync(
            string tripRef,
            string driverName,
            string vehicleReg,
            IEnumerable<(string podNumber, string school)> pods,
            CancellationToken ct = default)
        {
            var pdf = GenerateTripSheetPdf(tripRef, driverName, vehicleReg, pods);
            var doc = new DeviceDesk.Infrastructure.Data.Document
            {
                DocType = "TripSheet",
                FileName = $"TripSheet-{tripRef}.pdf",
                ContentType = "application/pdf",
                FileData = pdf
            };
            _phase0Db.Documents.Add(doc);
            await _phase0Db.SaveChangesAsync(ct);
            return (doc.DocumentId, doc.FileName);
        }

        private byte[] GenerateTripSheetPdf(
            string tripRef,
            string driverName,
            string vehicleReg,
            IEnumerable<(string podNumber, string school)> pods)
        {
            var podList = pods.ToList();
            var document = PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text($"TRIP SHEET {tripRef}")
                                .FontSize(20).Bold().FontColor(Colors.Orange.Darken2);
                        });
                    });

                    page.Content().PaddingVertical(20).Column(column =>
                    {
                        column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(info =>
                        {
                            info.Item().Row(r => { r.ConstantItem(140).Text("Driver:"); r.RelativeItem().Text(driverName); });
                            info.Item().Row(r => { r.ConstantItem(140).Text("Vehicle:"); r.RelativeItem().Text(vehicleReg); });
                        });

                        column.Item().PaddingTop(15).Text("PODs");
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(160);
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("POD");
                                header.Cell().Text("School");
                            });

                            foreach (var p in podList)
                            {
                                table.Cell().Text(p.podNumber);
                                table.Cell().Text(p.school);
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                    });
                });
            });
            return document.GeneratePdf();
        }
    }
}