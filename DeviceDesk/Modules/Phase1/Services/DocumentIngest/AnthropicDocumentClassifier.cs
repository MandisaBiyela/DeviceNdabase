using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace DeviceDesk.Modules.Phase1.Services.DocumentIngest;

public class AnthropicDocumentClassifier
{
    private const string SystemPrompt = """
You are a document classifier for a procurement and distribution management system. Analyse the document content and return JSON only.

Identify:
1. document_type: one of [
     'procurement_order',
     'delivery_note',
     'invoice',
     'proof_of_delivery',
     'stock_receipt',
     'financial_report',
     'unknown'
   ]
2. confidence: high / medium / low
3. key_fields: extract all key-value pairs found in the document
4. tables: extract all tabular data found as arrays of objects
5. suggested_table_name: if document_type is 'unknown', suggest a snake_case database table name based on the content
6. suggested_schema: if document_type is 'unknown', propose a schema as { field_name: data_type } for each column detected

Return ONLY valid JSON. No explanation.
""";

    private readonly HttpClient _http;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicDocumentClassifier> _logger;

    public AnthropicDocumentClassifier(HttpClient http, IOptions<AnthropicOptions> options, ILogger<AnthropicDocumentClassifier> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<DocumentClassificationResult> ClassifyAsync(string extractedText, IReadOnlyList<string>? registryKeys, CancellationToken ct)
    {
        var keyList = registryKeys is { Count: > 0 }
            ? string.Join(", ", registryKeys.Take(40))
            : "(none)";

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var h = HeuristicClassify(extractedText);
            h.UsedFallbackClassifier = true;
            return h;
        }

        var userContent = new StringBuilder();
        userContent.AppendLine("User-defined document_type_key values already registered in the system (prefer matching one when appropriate): ");
        userContent.AppendLine(keyList);
        userContent.AppendLine("If the document clearly matches one of these custom keys, set document_type to that exact key string.");
        userContent.AppendLine();
        userContent.AppendLine("Document text follows:");
        userContent.AppendLine(extractedText.Length > 100_000 ? extractedText[..100_000] + "\n...[truncated]" : extractedText);

        try
        {
            _http.DefaultRequestHeaders.Remove("x-api-key");
            _http.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
            _http.DefaultRequestHeaders.Remove("anthropic-version");
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var payload = new
            {
                model = _options.Model,
                max_tokens = _options.MaxTokens,
                system = SystemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userContent.ToString() }
                }
            };

            using var resp = await _http.PostAsJsonAsync("https://api.anthropic.com/v1/messages", payload, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Anthropic API error {Status}: {Body}", (int)resp.StatusCode, body);
                var h = HeuristicClassify(extractedText);
                h.UsedFallbackClassifier = true;
                return h;
            }

            using var doc = JsonDocument.Parse(body);
            var text = ExtractAssistantText(doc.RootElement);
            if (string.IsNullOrWhiteSpace(text))
            {
                var h = HeuristicClassify(extractedText);
                h.UsedFallbackClassifier = true;
                return h;
            }

            var json = StripMarkdownCodeFence(text);
            var parsed = ParseClassificationJson(json);
            parsed.RawJson = json;
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Anthropic classification failed; using heuristic fallback.");
            var h = HeuristicClassify(extractedText);
            h.UsedFallbackClassifier = true;
            return h;
        }
    }

    private static string ExtractAssistantText(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return string.Empty;
        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("text", out var t))
                sb.Append(t.GetString());
        }

        return sb.ToString();
    }

    private static string StripMarkdownCodeFence(string text)
    {
        var s = text.Trim();
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNl = s.IndexOf('\n');
            if (firstNl > 0) s = s[(firstNl + 1)..];
            var last = s.LastIndexOf("```", StringComparison.Ordinal);
            if (last > 0) s = s[..last];
        }

        return s.Trim();
    }

    internal static DocumentClassificationResult ParseClassificationJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var r = new DocumentClassificationResult { RawJson = json };
            r.DocumentType = GetString(root, "document_type") ?? "unknown";
            r.Confidence = GetString(root, "confidence") ?? "low";

            if (root.TryGetProperty("key_fields", out var kf))
            {
                if (kf.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in kf.EnumerateObject())
                        r.KeyFields[p.Name] = p.Value.ToString();
                }
            }

            if (root.TryGetProperty("tables", out var tables) && tables.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in tables.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object) continue;
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in row.EnumerateObject())
                        dict[p.Name] = p.Value.ToString();
                    r.Tables.Add(dict);
                }
            }

            r.SuggestedTableName = GetString(root, "suggested_table_name");
            if (root.TryGetProperty("suggested_schema", out var sch) && sch.ValueKind == JsonValueKind.Object)
            {
                r.SuggestedSchema = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in sch.EnumerateObject())
                    r.SuggestedSchema[p.Name] = p.Value.ToString();
            }

            return r;
        }
        catch
        {
            return HeuristicClassify(json);
        }
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    public static DocumentClassificationResult HeuristicClassify(string text)
    {
        var t = text ?? string.Empty;
        var lower = t.ToLowerInvariant();
        var r = new DocumentClassificationResult { UsedFallbackClassifier = true, Confidence = "low" };

        if (Regex.IsMatch(t, @"\bPO[\s\-#:]*[A-Z0-9\-_/]+\b", RegexOptions.IgnoreCase) ||
            lower.Contains("procurement") && lower.Contains("order"))
        {
            r.DocumentType = "procurement_order";
            r.Confidence = "medium";
        }
        else if (lower.Contains("delivery note") || lower.Contains("waybill") || lower.Contains("despatch"))
        {
            r.DocumentType = "delivery_note";
            r.Confidence = "medium";
        }
        else if (lower.Contains("tax invoice") || lower.Contains("invoice no") || lower.Contains("invoice number"))
        {
            r.DocumentType = "invoice";
            r.Confidence = "medium";
        }
        else if (lower.Contains("proof of delivery") || lower.Contains("pod") && lower.Contains("sign"))
        {
            r.DocumentType = "proof_of_delivery";
            r.Confidence = "low";
        }
        else if (lower.Contains("goods received") || lower.Contains("stock receipt") || lower.Contains("grv"))
        {
            r.DocumentType = "stock_receipt";
            r.Confidence = "low";
        }
        else if (lower.Contains("financial") && lower.Contains("report"))
        {
            r.DocumentType = "financial_report";
            r.Confidence = "low";
        }
        else
        {
            r.DocumentType = "unknown";
            r.SuggestedTableName = "ing_custom_document";
            r.SuggestedSchema = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["raw_text"] = "nvarchar(max)",
                ["captured_at"] = "datetime2"
            };
        }

        foreach (Match m in Regex.Matches(t, @"(PO\s*Number|PO\s*#|Purchase\s*Order)\s*[:\s]+([^\r\n]+)", RegexOptions.IgnoreCase))
            r.KeyFields[m.Groups[1].Value.Trim()] = m.Groups[2].Value.Trim();
        foreach (Match m in Regex.Matches(t, @"\b(PO[\s\-#:]*[A-Z0-9\-_/]+)\b", RegexOptions.IgnoreCase))
            r.KeyFields["po_number"] = m.Groups[1].Value.Trim();

        return r;
    }
}
