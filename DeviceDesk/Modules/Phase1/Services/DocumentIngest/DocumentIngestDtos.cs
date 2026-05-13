namespace DeviceDesk.Modules.Phase1.Services.DocumentIngest;

public sealed class DocumentIngestUploadResponse
{
    public Guid? AuditLogId { get; init; }
    public string IngestSessionId { get; init; } = "";
    public string FileName { get; init; } = "";
    public string? FileType { get; init; }
    public string FileSha256 { get; init; } = "";
    public string? StoredRelativePath { get; init; }
    public string? ExtractionError { get; init; }
    public string ExtractedPreview { get; init; } = "";
    public DocumentClassificationResult Classification { get; init; } = new();
    public DocumentMatchDto? Match { get; init; }
    public DuplicateUploadInfo? Duplicate { get; init; }
    public UserTableRouteDto? UserTableRoute { get; init; }
    public IReadOnlyList<string> StatusMessages { get; init; } = Array.Empty<string>();
}

public sealed class UserTableRouteDto
{
    public string TableName { get; init; } = "";
    public string DocumentTypeKey { get; init; } = "";
}

public sealed class DuplicateUploadInfo
{
    public DateTimeOffset PreviousUploadedAt { get; init; }
    public string? PreviousFileName { get; init; }
}

public sealed class DocumentMatchDto
{
    public bool Matched { get; init; }
    public string? MatchMethod { get; init; }
    public string? MatchedTable { get; init; }
    public Guid? MatchedRecordId { get; init; }
    public object? CurrentSnapshot { get; init; }
    public object? ProposedSnapshot { get; init; }
}

public sealed class DocumentIngestConfirmRequest
{
    public string IngestSessionId { get; set; } = "";
    public string FileSha256 { get; set; } = "";
    public string? ManualDocumentType { get; set; }
    public DocumentClassificationResult? Classification { get; set; }
    public bool ForceDuplicate { get; set; }

    /// <summary>update_matched | create_new | create_custom_table</summary>
    public string ConfirmMode { get; set; } = "";

    public Guid? MatchedOrderId { get; set; }
    public Dictionary<string, string>? OrderFieldUpdates { get; set; }
    public List<OrderLinePatchDto>? LinePatches { get; set; }

    /// <summary>For generic document kinds (delivery_note, invoice, …).</summary>
    public Dictionary<string, string>? GenericKeyFields { get; set; }

    public string? CustomTableName { get; set; }
    public string? CustomDisplayName { get; set; }
    public string? CustomDocumentTypeKey { get; set; }
    public List<CustomColumnDefDto>? CustomColumns { get; set; }
    public Dictionary<string, string>? CustomRowValues { get; set; }
}

public sealed class OrderLinePatchDto
{
    public Guid OrderLineId { get; set; }
    public int? QuantityReceived { get; set; }
    public string? Description { get; set; }
}

public sealed class CustomColumnDefDto
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "nvarchar(400)";
    public bool Include { get; set; } = true;
}

public sealed class DocumentIngestConfirmResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? ActionTaken { get; init; }
    public Guid? AuditLogId { get; init; }
    public Guid? CreatedOrUpdatedRecordId { get; init; }

    /// <summary>
    /// When the document is a procurement order (matched or new), this is the
    /// Phase 0 ProcurementOrderId so the UI can deep-link back to it.
    /// </summary>
    public Guid? ProcurementOrderId { get; init; }

    /// <summary>
    /// When the document is a procurement order, this is the NewStockBatchId that
    /// was created/updated for Phase 1 receiving. The UI uses this to render the
    /// "Go to New Stock Receiving →" call-to-action.
    /// </summary>
    public Guid? NewStockBatchId { get; init; }

    /// <summary>
    /// Best-effort target page for the post-save call-to-action button. Examples:
    /// "new-stock-receiving" | "delivery-tracking" | "financial-reconciliation".
    /// </summary>
    public string? NextStepRoute { get; init; }

    /// <summary>Optional human-readable summary to render in the success banner.</summary>
    public string? Message { get; init; }
}
