using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace DeviceDesk.Modules.Phase1.Services.DocumentIngest;

public class DocumentTextExtractorService
{
    private const int MaxChars = 120_000;

    public async Task<(string Text, string? Error)> ExtractAsync(Stream stream, string fileName, string contentType, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            if (ext == ".pdf" || contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
                return (Truncate(await ExtractPdfAsync(stream, ct)), null);

            if (ext is ".docx" || contentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase))
                return (Truncate(await ExtractDocxAsync(stream, ct)), null);

            if (ext is ".xlsx" or ".xls" || contentType.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase))
                return (Truncate(await ExtractXlsxAsync(stream, ct)), null);

            if (ext is ".csv" || contentType.Contains("csv", StringComparison.OrdinalIgnoreCase))
                return (Truncate(await ExtractCsvAsync(stream, ct)), null);

            if (ext is ".png" or ".jpg" or ".jpeg" || (contentType?.Contains("image/", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return ("[Image file — OCR is not configured on this server. Please use PDF/DOCX/CSV/XLSX or enter details manually after upload.]",
                    null);
            }

            return (string.Empty, $"Unsupported file type: {ext}");
        }
        catch (Exception ex)
        {
            return (string.Empty, ex.Message);
        }
    }

    private static string Truncate(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= MaxChars ? s : s[..MaxChars] + "\n...[truncated]";
    }

    private static async Task<string> ExtractPdfAsync(Stream stream, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(ms);
        foreach (var page in doc.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }

    private static async Task<string> ExtractDocxAsync(Stream stream, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;
        using var wordDoc = WordprocessingDocument.Open(ms, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body == null) return string.Empty;
        return body.InnerText;
    }

    private static async Task<string> ExtractXlsxAsync(Stream stream, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;
        using var wb = new XLWorkbook(ms);
        var sb = new StringBuilder();
        foreach (var ws in wb.Worksheets)
        {
            sb.AppendLine($"## Sheet: {ws.Name}");
            var used = ws.RangeUsed();
            if (used == null) continue;
            foreach (var row in used.Rows())
            {
                sb.AppendLine(string.Join("\t", row.Cells().Select(c => c.GetString())));
            }
        }

        return sb.ToString();
    }

    private static async Task<string> ExtractCsvAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }
}
