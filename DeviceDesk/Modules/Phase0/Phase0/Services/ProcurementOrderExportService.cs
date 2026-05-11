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
            summary.Cell(1, 4).Value = "Total order value";
            summary.Cell(1, 5).Value = "School totals";
            summary.Cell(1, 6).Value = "Total invoiced to dept";
            summary.Cell(1, 7).Value = "Total paid by dept";
            summary.Cell(1, 8).Value = "Total paid to suppliers";
            summary.Cell(1, 9).Value = "Outstanding balance";
            summary.Cell(1, 10).Value = "Balance status";
            summary.Cell(1, 11).Value = "Created (UTC)";
            summary.Range(1, 1, 1, 11).Style.Font.Bold = true;

            var row = 2;
            foreach (var o in orders)
            {
                var schoolTotals = o.Schools.Sum(s => s.SchoolSubTotal);
                var outstanding = o.TotalInvoicedToDepartment - o.TotalPaidByDepartment;
                summary.Cell(row, 1).Value = o.PoNumber;
                summary.Cell(row, 2).Value = o.ProjectName;
                summary.Cell(row, 3).Value = o.FinancialYear;
                summary.Cell(row, 4).Value = o.TotalOrderValue;
                summary.Cell(row, 5).Value = schoolTotals;
                summary.Cell(row, 6).Value = o.TotalInvoicedToDepartment;
                summary.Cell(row, 7).Value = o.TotalPaidByDepartment;
                summary.Cell(row, 8).Value = o.TotalPaidToSuppliers;
                summary.Cell(row, 9).Value = outstanding;
                summary.Cell(row, 10).Value = GetBalanceStatus(o);
                summary.Cell(row, 11).Value = o.CreatedAt.UtcDateTime;
                row++;
            }

            summary.Columns().AdjustToContents();

            var lines = wb.Worksheets.Add("Line items");
            lines.Cell(1, 1).Value = "PO Number";
            lines.Cell(1, 2).Value = "Project";
            lines.Cell(1, 3).Value = "Financial year";
            lines.Cell(1, 4).Value = "School";
            lines.Cell(1, 5).Value = "Description";
            lines.Cell(1, 6).Value = "Unit price";
            lines.Cell(1, 7).Value = "Qty";
            lines.Cell(1, 8).Value = "Line total";
            lines.Cell(1, 9).Value = "Delivery status";
            lines.Range(1, 1, 1, 9).Style.Font.Bold = true;

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
                        lines.Cell(lr, 6).Value = item.UnitPrice;
                        lines.Cell(lr, 7).Value = item.QtyOrdered;
                        lines.Cell(lr, 8).Value = item.TotalPrice;
                        lines.Cell(lr, 9).Value = item.DeliveryStatus.ToString();
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
                            var schoolTotals = o.Schools.Sum(s => s.SchoolSubTotal);
                            var outstanding = o.TotalInvoicedToDepartment - o.TotalPaidByDepartment;

                            col.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(info =>
                            {
                                info.Item().Text("Financial summary").Bold().FontSize(11);
                                info.Item().Row(r => { r.ConstantItem(160).Text("Financial year:").SemiBold(); r.RelativeItem().Text(o.FinancialYear); });
                                info.Item().Row(r => { r.ConstantItem(160).Text("Total order value:").SemiBold(); r.RelativeItem().Text($"R {o.TotalOrderValue:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(160).Text("Sum of school subtotals:").SemiBold(); r.RelativeItem().Text($"R {schoolTotals:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(160).Text("Invoiced to department:").SemiBold(); r.RelativeItem().Text($"R {o.TotalInvoicedToDepartment:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(160).Text("Paid by department:").SemiBold(); r.RelativeItem().Text($"R {o.TotalPaidByDepartment:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(160).Text("Paid to suppliers:").SemiBold(); r.RelativeItem().Text($"R {o.TotalPaidToSuppliers:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(160).Text("Outstanding balance:").SemiBold(); r.RelativeItem().Text($"R {outstanding:N2}"); });
                                info.Item().Row(r => { r.ConstantItem(160).Text("Balance status:").SemiBold(); r.RelativeItem().Text(GetBalanceStatus(o)); });
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

        private static string GetBalanceStatus(ProcurementOrder o)
        {
            var schoolsTotal = o.Schools.Sum(s => s.SchoolSubTotal);
            var outstandingBalance = o.TotalInvoicedToDepartment - o.TotalPaidByDepartment;
            return schoolsTotal == o.TotalOrderValue && outstandingBalance == 0m
                ? FinancialBalanceStatus.Balanced.ToString()
                : FinancialBalanceStatus.Outstanding.ToString();
        }
    }
}
