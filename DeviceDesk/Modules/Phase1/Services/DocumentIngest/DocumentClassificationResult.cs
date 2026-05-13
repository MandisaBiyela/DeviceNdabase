using System.Text.Json.Serialization;

namespace DeviceDesk.Modules.Phase1.Services.DocumentIngest;

/// <summary>JSON shape returned by Claude (or heuristic fallback).</summary>
public class DocumentClassificationResult
{
    [JsonPropertyName("document_type")]
    public string DocumentType { get; set; } = "unknown";

    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = "low";

    [JsonPropertyName("key_fields")]
    public Dictionary<string, string> KeyFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("tables")]
    public List<Dictionary<string, string>> Tables { get; set; } = new();

    [JsonPropertyName("suggested_table_name")]
    public string? SuggestedTableName { get; set; }

    [JsonPropertyName("suggested_schema")]
    public Dictionary<string, string>? SuggestedSchema { get; set; }

    [JsonIgnore]
    public string? RawJson { get; set; }

    [JsonIgnore]
    public bool UsedFallbackClassifier { get; set; }
}
