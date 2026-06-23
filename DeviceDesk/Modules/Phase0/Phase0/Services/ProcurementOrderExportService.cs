using ClosedXML.Excel;
using DeviceDesk.Modules.Phase0.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfDocument = QuestPDF.Fluent.Document;

namespace DeviceDesk.Modules.Phase0.Services
{
    public class ProcurementOrderExportService
    {
        private readonly ProcurementOrderFinancialService _financials = new();

        static ProcurementOrderExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] BuildExcel(IReadOnlyList<ProcurementOrder> orders)
        {
            using var wb = new XLWorkbook();
            var summary = wb.Worksheets.Add("Orders summary");
            summary.Cell(1, 1).Value = "PO Number";
            summary.Cell(1, 2).Value = "Project";
            summary.Cell(1, 3).Value = "Financial year";
            summary.Cell(1, 4).Value = "DOE order value";
            summary.Cell(1, 5).Value = "Management fee %";
            summary.Cell(1, 6).Value = "Management fee amount";
            summary.Cell(1, 7).Value = "Supplier fee";
            summary.Cell(1, 8).Value = "Total allocated to schools";
            summary.Cell(1, 9).Value = "Allocation variance";
            summary.Cell(1, 10).Value = "Allocation status";
            summary.Cell(1, 11).Value = "Total invoiced to dept";
            summary.Cell(1, 12).Value = "Total paid by dept";
            summary.Cell(1, 13).Value = "Total paid to suppliers";
            summary.Cell(1, 14).Value = "Outstanding from DOE";
            summary.Cell(1, 15).Value = "Outstanding to supplier";
            summary.Cell(1, 16).Value = "Created (UTC)";
            summary.Range(1, 1, 1, 16).Style.Font.Bold = true;

            var row = 2;
            foreach (var o in orders)
            {
                var fin = _financials.Summarize(o);
                summary.Cell(row, 1).Value = o.PoNumber;
                summary.Cell(row, 2).Value = o.ProjectName;
                summary.Cell(row, 3).Value = o.FinancialYear;
                summary.Cell(row, 4).Value = o.TotalOrderValue;
                summary.Cell(row, 5).Value = o.ManagementFeePercentage;
                summary.Cell(row, 6).Value = fin.ManagementFeeAmount;
                summary.Cell(row, 7).Value = fin.SupplierFee;
                summary.Cell(row, 8).Value = fin.TotalAllocatedToSchools;
                summary.Cell(row, 9).Value = fin.AllocationVariance;
                summary.Cell(row, 10).Value = fin.AllocationBalanceStatus.ToString();
                summary.Cell(row, 11).Value = o.TotalInvoicedToDepartment;
                summary.Cell(row, 12).Value = o.TotalPaidByDepartment;
                summary.Cell(row, 13).Value = o.TotalPaidToSuppliers;
                summary.Cell(row, 14).Value = fin.OutstandingFromDoe;
                summary.Cell(row, 15).Value = fin.OutstandingToSupplier;
                summary.Cell(row, 16).Value = o.CreatedAt.UtcDateTime;
                row++;
            }

            summary.Columns().AdjustToContents();

            var lines = wb.Worksheets.Add("Line items");
            lines.Cell(1, 1).Value = "PO Number";
            lines.Cell(1, 2).Value = "Project";
            lines.Cell(1, 3).Value = "Financial year";
            lines.Cell(1, 4).Value = "School";
            lines.Cell(1, 5).Value = "Description";
            lines.Cell(1, 6).Value = "Brand";
            lines.Cell(1, 7).Value = "Model";
            lines.Cell(1, 8).Value = "Unit price";
            lines.Cell(1, 9).Value = "Qty";
            lines.Cell(1, 10).Value = "Line total";
            lines.Cell(1, 11).Value = "Delivery status";
            lines.Range(1, 1, 1, 11).Style.Font.Bold = true;

            var lr = 2;
            foreach (var o in orders)
            {
                foreach (var school in o.Schools)
                {
                    foreach (var item in school.Items)
                    {
                        lines.Cell(lr, 1).Value = o.PoNumber;
                        lines.Cell(lr, 2).Value = o.ProjectName;
                        lines.Cell(lr, 3).Value = o.FinancialYear;
                        lines.Cell(lr, 4).Value = school.SchoolName;
                        lines.Cell(lr, 5).Value = item.Description;
                        lines.Cell(lr, 6).Value = item.Brand;
                        lines.Cell(lr, 7).Value = item.Model;
                        lines.Cell(lr, 8).Value = item.UnitPrice;
                        lines.Cell(lr, 9).Value = item.QtyOrdered;
                        lines.Cell(lr, 10).Value = item.TotalPrice;
                        lines.Cell(lr, 11).Value = item.DeliveryStatus.ToString();
                        lr++;
                    }
                }
            }

            lines.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public byte[] BuildPdf(IReadOnlyList<ProcurementOrder> orders)
        {
            return PdfDocument.Create(document =>
            {
                if (orders.Count == 0)
                {
                    document.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.Content().Text("No procurement orders are on file.").FontSize(12);
                    });
                    return;
                }

                foreach (var o in orders)
                {
                    var fin = _financials.Summarize(o);

                    document.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(36);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Procurement order").FontSize(16).Bold();
                                c.Item().Text($"{o.PoNumber} — {o.ProjectName}").SemiBold();
                            });
                            row.ConstantItem(120).AlignRight().Text(o.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm") + " UTC").FontSize(9);
                        });

                        page.Content().PaddingTop(16).Column(col =>
                        {
                            col.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(info =>
                            {
                                info.Item().Text("Financial summary").Bold().FontSize(11);
                                info.Item().Row(r => { r.ConstantItem(180).Text("Financial year:").SemiBold(); r.RelativeItem().Text(o.FinancialYear); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("DOE order value:").SemiBold(); r.RelativeItem().Text($"R {o.TotalOrderValue:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("Management fee:").SemiBold(); r.RelativeItem().Text($"{o.ManagementFeePercentage:N2}% (R {fin.ManagementFeeAmount:N2})"); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("Supplier fee:").SemiBold(); r.RelativeItem().Text($"R {fin.SupplierFee:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("Allocated to schools:").SemiBold(); r.RelativeItem().Text($"R {fin.TotalAllocatedToSchools:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("Allocation variance:").SemiBold(); r.RelativeItem().Text($"R {fin.AllocationVariance:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("Allocation status:").SemiBold(); r.RelativeItem().Text(fin.AllocationBalanceStatus.ToString()); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("Invoiced to DOE:").SemiBold(); r.RelativeItem().Text($"R {o.TotalInvoicedToDepartment:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("Paid by DOE:").SemiBold(); r.RelativeItem().Text($"R {o.TotalPaidByDepartment:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("Paid to supplier:").SemiBold(); r.RelativeItem().Text($"R {o.TotalPaidToSuppliers:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("Outstanding from DOE:").SemiBold(); r.RelativeItem().Text($"R {fin.OutstandingFromDoe:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(180).Text("Outstanding to supplier:").SemiBold(); r.RelativeItem().Text($"R {fin.OutstandingToSupplier:N2}"); });
                            });

                            foreach (var school in o.Schools)
                            {
                                col.Item().PaddingTop(12).Text(school.SchoolName).Bold().FontSize(11);
                                col.Item().Text($"School subtotal: R {school.SchoolSubTotal:N2}").FontSize(9).FontColor(Colors.Grey.Darken2);

                                col.Item().PaddingTop(4).Table(table =>
                                {
                                    table.ColumnsDefinition(c =>
                                    {
                                        c.RelativeColumn(3);
                                        c.ConstantColumn(70);
                                        c.ConstantColumn(40);
                                        c.ConstantColumn(70);
                                        c.RelativeColumn(2);
                                    });

                                    table.Header(h =>
                                    {
                                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Description").FontSize(9).Bold();
                                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Unit").FontSize(9).Bold();
                                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Qty").FontSize(9).Bold();
                                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Total").FontSize(9).Bold();
                                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Delivery").FontSize(9).Bold();
                                    });

                                    foreach (var item in school.Items)
                                    {
                                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.Description).FontSize(9);
                                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"R {item.UnitPrice:N2}").FontSize(9);
                                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.QtyOrdered.ToString()).FontSize(9);
                                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text($"R {item.TotalPrice:N2}").FontSize(9);
                                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.DeliveryStatus.ToString()).FontSize(9);
                                    }
                                });
                            }
                        });

                        page.Footer().AlignCenter().Text(t =>
                        {
                            t.Span("DeviceDesk — Phase 0 procurement").FontSize(8).FontColor(Colors.Grey.Medium);
                            t.Span("  ·  ").FontSize(8);
                            t.CurrentPageNumber().FontSize(8);
                        });
                    });
                }
            }).GeneratePdf();
        }
    }
}
