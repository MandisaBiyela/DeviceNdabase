using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DeviceDesk.Modules.Phase1.Models;

/// <summary>Registry of document types (built-in and user-created) for routing ingest results.</summary>
public class DocumentTypeRegistry
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Physical SQL table name for user-created types; for built-in generic types use receiving_generic_documents.</summary>
    [Required]
    [MaxLength(128)]
    public string TableName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Classifier key, e.g. delivery_note, invoice, or custom slug.</summary>
    [Required]
    [MaxLength(80)]
    public string DocumentTypeKey { get; set; } = string.Empty;

    public bool IsSystemType { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? SchemaJson { get; set; }

    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    [MaxLength(512)]
    public string? SampleFileName { get; set; }
}

/// <summary>Audit trail for every document ingest attempt.</summary>
public class UploadAuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(512)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? FileType { get; set; }

    [MaxLength(64)]
    public string? FileSha256 { get; set; }

    [MaxLength(80)]
    public string? DocumentTypeDetected { get; set; }

    [MaxLength(20)]
    public string? ConfidenceLevel { get; set; }

    public Guid? MatchedRecordId { get; set; }

    [MaxLength(128)]
    public string? MatchedTable { get; set; }

    /// <summary>updated | created | new_table_created | rejected | failed</summary>
    [Required]
    [MaxLength(40)]
    public string ActionTaken { get; set; } = "failed";

    [MaxLength(450)]
    public string? UploadedByUserId { get; set; }

    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(1024)]
    public string? FileStoragePath { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ClassificationJson { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? Notes { get; set; }
}

/// <summary>Stores captured content for built-in non-procurement document kinds in one table.</summary>
public class ReceivingGenericDocument
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>delivery_note, invoice, proof_of_delivery, stock_receipt, financial_report</summary>
    [Required]
    [MaxLength(80)]
    public string DocumentKind { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string PayloadJson { get; set; } = "{}";

    [MaxLength(1024)]
    public string? SourceFilePath { get; set; }

    public Guid? LinkedProcurementOrderId { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
