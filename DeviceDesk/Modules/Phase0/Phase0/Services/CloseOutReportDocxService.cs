using DeviceDesk.Middleware;
using DeviceDesk.Modules.Phase0.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DeviceDesk.Modules.Phase0.Services
{
    /// <summary>
    /// Builds the KZN MST/ICT Close-Out &amp; Financial Report as .docx (Open XML) from procurement order data.
    /// </summary>
    public class CloseOutReportDocxService
    {
        private const string NavyHex = "1F4E79";
        private const string LightBlueHex = "EBF3FB";
        private const string Arial = "Arial";

        private readonly ProcurementOrderFinancialService _financials = new();

        private static decimal Round2(decimal v) => ProcurementOrderFinancialService.RoundCurrency(v);

        /// <summary>Returns attachment file name (sanitized).</summary>
        public static string BuildFileName(string poNumber, string financialYear)
        {
            static string San(string s) =>
                string.Join("_", (s ?? "").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
                    .Trim();
            return $"CloseOut_{San(poNumber)}_{San(financialYear)}.docx";
        }

        public void ValidateOrThrow(ProcurementOrder order)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(order.PoNumber))
                errors.Add(new ValidationError("poNumber", "PO Number is required for the close-out report."));
            if (string.IsNullOrWhiteSpace(order.ProjectName))
                errors.Add(new ValidationError("projectName", "Project Name is required for the close-out report."));
            if (string.IsNullOrWhiteSpace(order.FinancialYear))
                errors.Add(new ValidationError("financialYear", "Financial Year is required for the close-out report."));

            if (order.Schools == null || order.Schools.Count == 0)
                errors.Add(new ValidationError("schools", "No schools linked to this order."));

            if (errors.Count > 0)
                throw new ValidationException(errors);
        }

        public byte[] BuildDocument(ProcurementOrder order, DateTime reportDateUtc)
        {
            ValidateOrThrow(order);

            var summary = _financials.Summarize(order);
            var allocationStatus = summary.AllocationBalanceStatus == AllocationBalanceStatus.Balanced
                ? "BALANCED"
                : "NOT BALANCED";

            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new Document();
                var body = main.Document.AppendChild(new Body());

                var hdrPart = main.AddNewPart<HeaderPart>();
                var hdrId = main.GetIdOfPart(hdrPart);
                hdrPart.Header = new Header(BuildHeaderParagraph());

                var ftrPart = main.AddNewPart<FooterPart>();
                var ftrId = main.GetIdOfPart(ftrPart);
                ftrPart.Footer = new Footer(BuildFooterParagraphs());

                // Cover + content
                body.AppendChild(Para("KZN MST/ICT PROJECT REPORT – CLOSE-OUT AND FINANCIAL REPORT", bold: true, navy: true, fontHalfPoints: 28, spaceAfter: 240));
                body.AppendChild(Para($"{order.FinancialYear} MST/ICT PROCUREMENT AND DISTRIBUTION CYCLE", bold: true, navy: true, fontHalfPoints: 22, spaceAfter: 360));

                body.AppendChild(Para("ORDER INFORMATION", bold: true, navy: true, spaceBefore: 120, spaceAfter: 120));
                body.AppendChild(OrderInfoTable(order, summary, reportDateUtc));

                body.AppendChild(Para("SECTION 2 – SCHOOL ALLOCATION SUMMARY", bold: true, navy: true, spaceBefore: 360, spaceAfter: 120));
                body.AppendChild(SchoolAllocationSummaryTable(order));

                body.AppendChild(Para("SECTION 3 – ITEM BREAKDOWN PER SCHOOL", bold: true, navy: true, spaceBefore: 360, spaceAfter: 120));

                var schoolIndex = 1;
                foreach (var school in order.Schools.OrderBy(s => s.SchoolName))
                {
                    body.AppendChild(Para($"School {schoolIndex}: {school.SchoolName}", bold: true, navy: true, spaceBefore: 200, spaceAfter: 80));
                    body.AppendChild(SchoolItemsTable(school));
                    schoolIndex++;
                }

                body.AppendChild(Para(
                    $"TOTAL ALLOCATED TO SCHOOLS: R {summary.TotalAllocatedToSchools:N2}",
                    bold: true,
                    spaceBefore: 120,
                    spaceAfter: 120));

                body.AppendChild(Para("SECTION 4 – DELIVERY STATUS", bold: true, navy: true, spaceBefore: 360, spaceAfter: 120));
                body.AppendChild(Para(
                    "All materials ordered were delivered to the respective schools. POD documents were submitted to the Department.",
                    spaceAfter: 120));
                body.AppendChild(DeliverySummaryTable(order));

                body.AppendChild(Para("SECTION 5 – FINANCIAL RECONCILIATION", bold: true, navy: true, spaceBefore: 360, spaceAfter: 120));
                body.AppendChild(AllocationFinancialTable(summary, allocationStatus));

                body.AppendChild(Para("SECTION 6 – PAYMENT TRACKING", bold: true, navy: true, spaceBefore: 360, spaceAfter: 120));
                var financialMissing =
                    order.TotalOrderValue > 0m &&
                    order.TotalInvoicedToDepartment == 0m &&
                    order.TotalPaidByDepartment == 0m &&
                    order.TotalPaidToSuppliers == 0m;
                if (financialMissing)
                {
                    body.AppendChild(Para(
                        "Note: Payment data has not been recorded for this order (all invoice and payment amounts are zero).",
                        italic: true,
                        spaceAfter: 120));
                }

                body.AppendChild(PaymentTrackingTable(order, summary));

                body.AppendChild(Para("SECTION 7 – TIMELINE REVIEW", bold: true, navy: true, spaceBefore: 360, spaceAfter: 120));
                var timeline = string.IsNullOrWhiteSpace(order.TimelineNotes)
                    ? "The project was delivered within the specified timeframe."
                    : order.TimelineNotes.Trim();
                body.AppendChild(Para(timeline, spaceAfter: 120));

                body.AppendChild(Para("SECTION 8 – SCOPE CHANGES", bold: true, navy: true, spaceBefore: 240, spaceAfter: 120));
                var scope = string.IsNullOrWhiteSpace(order.ScopeNotes)
                    ? "There were no changes to the scope."
                    : order.ScopeNotes.Trim();
                body.AppendChild(Para(scope, spaceAfter: 120));

                body.AppendChild(Para("SECTION 9 – BACKUP DOCUMENTATION", bold: true, navy: true, spaceBefore: 240, spaceAfter: 120));
                body.AppendChild(BulletParagraph("POD copies submitted in hard copy and soft copy"));
                body.AppendChild(BulletParagraph("Purchase Order (PO) documentation"));
                body.AppendChild(BulletParagraph("Supplier invoices and payment confirmations"));
                body.AppendChild(BulletParagraph("School readiness assessments and QC documentation"));

                body.AppendChild(Para("SECTION 10 – CONCLUSION", bold: true, navy: true, spaceBefore: 240, spaceAfter: 120));
                body.AppendChild(Para(
                    "We are of the view that if the partnership between KZN DoE, Ndabase Printing Solutions and all main stakeholders continues to blossom, we can only improve and innovate upon the way we procure and distribute LTSM to schools, ensuring that our clients — the learners and teachers — are resourced with the correct materials to unleash the potential that only Education can provide.",
                    spaceAfter: 240));

                body.AppendChild(Para("SIGNATURES", bold: true, navy: true, spaceBefore: 120, spaceAfter: 120));
                body.AppendChild(SignatureTable());

                body.AppendChild(Para(
                    "KwaZulu-Natal: 8 Lavendergate Drive, Southgate Business Park, Amanzimtoti, 4126 | Tel: 031 828 5900",
                    fontHalfPoints: 18,
                    spaceBefore: 240,
                    spaceAfter: 40));
                body.AppendChild(Para(
                    "Gauteng: Ground Floor, Midcity Square, 501 Jorissen Street, Sunnyside East, Pretoria, 0002 | Tel: 012 343 6291",
                    fontHalfPoints: 18,
                    spaceAfter: 40));
                body.AppendChild(Para(
                    "Managing Members: Mr Thanda Nyide, Mrs Tswelopele Nyide | Reg. No. 2007/246547/23",
                    fontHalfPoints: 18,
                    spaceAfter: 40));
                body.AppendChild(Para("www.ndabaseprinting.co.za", fontHalfPoints: 18, spaceAfter: 0));

                body.AppendChild(new SectionProperties(
                    new HeaderReference { Type = HeaderFooterValues.Default, Id = hdrId },
                    new FooterReference { Type = HeaderFooterValues.Default, Id = ftrId },
                    new PageSize { Width = 11906U, Height = 16838U },
                    new PageMargin { Top = 1440, Right = 1440, Bottom = 1440, Left = 1440, Header = 708U, Footer = 708U, Gutter = 0U }));

                main.Document.Save();
            }

            return ms.ToArray();
        }

        private static Paragraph BuildHeaderParagraph() =>
            Para("KZN MST/ICT PROJECT  |  CLOSE-OUT & FINANCIAL REPORT", bold: true, navy: true, fontHalfPoints: 20, align: JustificationValues.Center);

        private static IEnumerable<OpenXmlElement> BuildFooterParagraphs()
        {
            var tbl = new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None })),
                new TableGrid(new GridColumn { Width = "4500" }, new GridColumn { Width = "4500" }),
                new TableRow(
                    new TableCell(
                        new TableCellProperties(new TableCellWidth { Width = "4500", Type = TableWidthUnitValues.Dxa }),
                        Para("Ndabase Printing Solutions  |  Confidential", fontHalfPoints: 18, align: JustificationValues.Left)),
                    new TableCell(
                        new TableCellProperties(new TableCellWidth { Width = "4500", Type = TableWidthUnitValues.Dxa }),
                        new Paragraph(
                            new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                            new Run(ArialRunProps(18), new Text("Page ")),
                            new SimpleField { Instruction = " PAGE " }))));

            yield return tbl;

            yield return Para("", fontHalfPoints: 8, spaceAfter: 0);
        }

        private static Table OrderInfoTable(
            ProcurementOrder order,
            ProcurementOrderFinancialSummary summary,
            DateTime reportDateUtc)
        {
            var rows = new (string, string)[]
            {
                ("PO Number:", order.PoNumber),
                ("Project Name:", order.ProjectName),
                ("Financial Year:", order.FinancialYear),
                ("Supplier:", string.IsNullOrWhiteSpace(order.SupplierName) ? "—" : order.SupplierName),
                ("DOE Order Value:", $"R {summary.OrderValue:N2}"),
                ("Management Fee %:", $"{summary.ManagementFeePercentage:N2}%"),
                ("Management Fee Amount:", $"R {summary.ManagementFeeAmount:N2}"),
                ("Supplier Fee / Allocation Budget:", $"R {summary.SupplierFee:N2}"),
                ("Compiled By:", "Ndabase Printing Solutions"),
                ("Report Date:", reportDateUtc.ToString("yyyy-MM-dd"))
            };
            return KeyValueTable(rows, shadeRows: true);
        }

        private static Table SchoolAllocationSummaryTable(ProcurementOrder order)
        {
            var headers = new[] { "School Name", "Total Allocated", "Items / Devices", "Delivery Status" };
            var rows = order.Schools.OrderBy(s => s.SchoolName).Select(school =>
            {
                var itemCount = school.Items.Sum(i => i.QtyOrdered);
                var statuses = school.Items.Select(i => i.DeliveryStatus).Distinct().ToList();
                var deliveryLabel = statuses.Count == 1
                    ? statuses[0].ToString()
                    : string.Join(", ", statuses.Select(x => x.ToString()));
                return new[]
                {
                    school.SchoolName,
                    $"R {school.SchoolSubTotal:N2}",
                    itemCount.ToString(),
                    deliveryLabel
                };
            });
            return DataTable(headers, rows);
        }

        private static Table KeyValueTable((string label, string value)[] rows, bool shadeRows)
        {
            var t = new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4U },
                        new BottomBorder { Val = BorderValues.Single, Size = 4U },
                        new LeftBorder { Val = BorderValues.Single, Size = 4U },
                        new RightBorder { Val = BorderValues.Single, Size = 4U },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4U },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4U })),
                new TableGrid(new GridColumn { Width = "3200" }, new GridColumn { Width = "5800" }));

            var i = 0;
            foreach (var (label, value) in rows)
            {
                var shade = shadeRows && i % 2 == 0;
                t.AppendChild(new TableRow(
                    LabelCell(label, shade),
                    ValueCell(value, shade)));
                i++;
            }

            return t;
        }

        private static TableCellProperties CellWidthAndShade(string widthDxa, bool shade)
        {
            var p = new TableCellProperties(new TableCellWidth { Width = widthDxa, Type = TableWidthUnitValues.Dxa });
            if (shade)
                p.AppendChild(CellShading(LightBlueHex));
            return p;
        }

        private static TableCell LabelCell(string text, bool shade) =>
            new TableCell(CellWidthAndShade("3200", shade), Para(text, bold: true, fontHalfPoints: 22));

        private static TableCell ValueCell(string text, bool shade) =>
            new TableCell(CellWidthAndShade("5800", shade), Para(text, fontHalfPoints: 22));

        private static Shading CellShading(string fill) =>
            new Shading { Val = ShadingPatternValues.Clear, Fill = fill, Color = "auto" };

        private static Table SchoolItemsTable(ProcurementOrderSchool school)
        {
            var headers = new[] { "Description", "Brand", "Model", "Unit Price (R)", "Qty", "Total (R)", "Delivery Status" };
            var t = DataTable(headers, school.Items.OrderBy(i => i.Description).Select(i => new[]
            {
                i.Description,
                i.Brand ?? "—",
                i.Model ?? "—",
                i.UnitPrice.ToString("N2"),
                i.QtyOrdered.ToString(),
                i.TotalPrice.ToString("N2"),
                i.DeliveryStatus.ToString()
            }));

            t.AppendChild(new TableRow(
                new TableCell(CellWidthAndShade("3600", true), Para("School Sub-Total:", bold: true)),
                new TableCell(CellWidthAndShade("1200", true), Para("")),
                new TableCell(CellWidthAndShade("1000", true), Para("")),
                new TableCell(CellWidthAndShade("1400", true), Para("")),
                new TableCell(CellWidthAndShade("1000", true), Para("")),
                new TableCell(CellWidthAndShade("1400", true), Para($"R {school.SchoolSubTotal:N2}", bold: true)),
                new TableCell(CellWidthAndShade("1600", true), Para(""))));

            return t;
        }

        private static Table DataTable(string[] headers, IEnumerable<string[]> dataRows)
        {
            var colCount = headers.Length;
            var widths = colCount == 5
                ? new[] { "3600", "1200", "1000", "1400", "1600" }
                : Enumerable.Repeat("2000", colCount).ToArray();

            var grid = new TableGrid();
            foreach (var w in widths)
                grid.AppendChild(new GridColumn { Width = w });

            var t = new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4U },
                        new BottomBorder { Val = BorderValues.Single, Size = 4U },
                        new LeftBorder { Val = BorderValues.Single, Size = 4U },
                        new RightBorder { Val = BorderValues.Single, Size = 4U },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4U },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4U })),
                grid);

            var hr = new TableRow();
            foreach (var h in headers)
            {
                hr.AppendChild(new TableCell(
                    new TableCellProperties(CellShading(LightBlueHex)),
                    Para(h, bold: true, navy: true)));
            }

            t.AppendChild(hr);

            foreach (var row in dataRows)
            {
                var tr = new TableRow();
                foreach (var c in row)
                    tr.AppendChild(new TableCell(new TableCellProperties(CellShading(LightBlueHex)), Para(c)));
                t.AppendChild(tr);
            }

            return t;
        }

        private static (int ordered, int delivered, int outstanding, decimal pct) SchoolDeliveryMetrics(ProcurementOrderSchool school)
        {
            var ordered = 0;
            var delivered = 0;
            foreach (var it in school.Items)
            {
                ordered += it.QtyOrdered;
                if (it.DeliveryStatus == SchoolItemDeliveryStatus.Delivered)
                    delivered += it.QtyOrdered;
            }

            var outstanding = Math.Max(0, ordered - delivered);
            var pct = ordered > 0 ? 100m * delivered / ordered : 100m;
            return (ordered, delivered, outstanding, decimal.Round(pct, 1, MidpointRounding.AwayFromZero));
        }

        private static Table DeliverySummaryTable(ProcurementOrder order)
        {
            var headers = new[] { "School Name", "Items Ordered", "Items Delivered", "Outstanding", "% Complete" };
            var rows = new List<string[]>();
            var sumO = 0;
            var sumD = 0;
            var sumOut = 0;
            var pcts = new List<decimal>();

            foreach (var school in order.Schools.OrderBy(s => s.SchoolName))
            {
                var m = SchoolDeliveryMetrics(school);
                sumO += m.ordered;
                sumD += m.delivered;
                sumOut += m.outstanding;
                pcts.Add(m.pct);
                rows.Add(new[]
                {
                    school.SchoolName,
                    m.ordered.ToString(),
                    m.delivered.ToString(),
                    m.outstanding.ToString(),
                    $"{m.pct:N1}%"
                });
            }

            var avgPct = pcts.Count > 0 ? decimal.Round(pcts.Average(), 1, MidpointRounding.AwayFromZero) : 0m;
            rows.Add(new[]
            {
                "TOTALS",
                sumO.ToString(),
                sumD.ToString(),
                sumOut.ToString(),
                $"{avgPct:N1}%"
            });

            return DataTable(headers, rows);
        }

        private static Table AllocationFinancialTable(ProcurementOrderFinancialSummary summary, string allocationStatus)
        {
            var rows = new (string, string)[]
            {
                ("DOE Order Value", $"R {summary.OrderValue:N2}"),
                ("Management Fee %", $"{summary.ManagementFeePercentage:N2}%"),
                ("Management Fee Amount", $"R {summary.ManagementFeeAmount:N2}"),
                ("Supplier Fee / Allocation Budget", $"R {summary.SupplierFee:N2}"),
                ("Total Allocated to Schools", $"R {summary.TotalAllocatedToSchools:N2}"),
                ("Allocation Variance", $"R {summary.AllocationVariance:N2}"),
                ("Allocation Balance Status", allocationStatus)
            };
            return KeyValueTable(rows, shadeRows: true);
        }

        private static Table PaymentTrackingTable(ProcurementOrder order, ProcurementOrderFinancialSummary summary)
        {
            var rows = new (string, string)[]
            {
                ("Amount Invoiced to DOE", $"R {order.TotalInvoicedToDepartment:N2}"),
                ("Amount Received from DOE", $"R {order.TotalPaidByDepartment:N2}"),
                ("Amount Paid to Supplier", $"R {order.TotalPaidToSuppliers:N2}"),
                ("Outstanding from DOE", $"R {summary.OutstandingFromDoe:N2}"),
                ("Outstanding to Supplier", $"R {summary.OutstandingToSupplier:N2}")
            };
            return KeyValueTable(rows, shadeRows: true);
        }

        private static Table SignatureTable()
        {
            return new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.None },
                        new BottomBorder { Val = BorderValues.None },
                        new LeftBorder { Val = BorderValues.None },
                        new RightBorder { Val = BorderValues.None },
                        new InsideHorizontalBorder { Val = BorderValues.None },
                        new InsideVerticalBorder { Val = BorderValues.None })),
                new TableGrid(new GridColumn { Width = "4500" }, new GridColumn { Width = "4500" }),
                new TableRow(
                    new TableCell(
                        new TableCellProperties(new TableCellWidth { Width = "4500", Type = TableWidthUnitValues.Dxa }),
                        Para("Signed: Ndabase Printing Solutions", bold: true),
                        Para("Name: ___________________________", spaceAfter: 40),
                        Para("Designation: ____________________", spaceAfter: 40),
                        Para("Date: ___________________________", spaceAfter: 0)),
                    new TableCell(
                        new TableCellProperties(new TableCellWidth { Width = "4500", Type = TableWidthUnitValues.Dxa }),
                        Para("Signed: KwaZulu-Natal DoE", bold: true),
                        Para("Name: ___________________________", spaceAfter: 40),
                        Para("Designation: ____________________", spaceAfter: 40),
                        Para("Date: ___________________________", spaceAfter: 0))));
        }

        private static Paragraph BulletParagraph(string text) =>
            new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { After = "80" },
                    new Indentation { Left = "360", Hanging = "360" }),
                new Run(
                    ArialRunProps(22),
                    new Text("• " + text)));

        private static Paragraph Para(
            string text,
            bool bold = false,
            bool navy = false,
            bool italic = false,
            int fontHalfPoints = 22,
            int spaceAfter = 160,
            int spaceBefore = 0,
            JustificationValues? align = null)
        {
            var j = align ?? JustificationValues.Both;
            var pPr = new ParagraphProperties(
                new SpacingBetweenLines { Before = spaceBefore.ToString(), After = spaceAfter.ToString() },
                new Justification { Val = j });

            var r = new Run(ArialRunProps(fontHalfPoints, bold, navy, italic), new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return new Paragraph(pPr, r);
        }

        private static RunProperties ArialRunProps(int fontHalfPoints, bool bold = false, bool navy = false, bool italic = false)
        {
            var rp = new RunProperties(
                new RunFonts { Ascii = Arial, HighAnsi = Arial, ComplexScript = Arial },
                new FontSize { Val = fontHalfPoints.ToString() },
                new FontSizeComplexScript { Val = fontHalfPoints.ToString() });

            if (bold)
                rp.AppendChild(new Bold());
            if (italic)
                rp.AppendChild(new Italic());
            if (navy)
                rp.AppendChild(new Color { Val = NavyHex });

            return rp;
        }
    }
}
