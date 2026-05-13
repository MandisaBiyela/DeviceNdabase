using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.Json;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase0.Models;
using DeviceDesk.Modules.Phase0.Services;
using DeviceDesk.Modules.Phase1.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Services.DocumentIngest;

public class ReceivingDocumentIngestService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IWebHostEnvironment _env;
    private readonly Phase1DbContext _db;
    private readonly DeviceDeskDbContext _coreDb;
    private readonly ProcurementOrderBatchSyncService _batchSync;
    private readonly DocumentTextExtractorService _extractor;
    private readonly AnthropicDocumentClassifier _classifier;
    private readonly DocumentMatchService _matcher;
    private readonly DynamicIngestTableService _ddl;
    private readonly ILogger<ReceivingDocumentIngestService> _logger;

    public ReceivingDocumentIngestService(
        IWebHostEnvironment env,
        Phase1DbContext db,
        DeviceDeskDbContext coreDb,
        ProcurementOrderBatchSyncService batchSync,
        DocumentTextExtractorService extractor,
        AnthropicDocumentClassifier classifier,
        DocumentMatchService matcher,
        DynamicIngestTableService ddl,
        ILogger<ReceivingDocumentIngestService> logger)
    {
        _env = env;
        _db = db;
        _coreDb = coreDb;
        _batchSync = batchSync;
        _extractor = extractor;
        _classifier = classifier;
        _matcher = matcher;
        _ddl = ddl;
        _logger = logger;
    }

    public async Task<DocumentIngestUploadResponse> UploadAndAnalyzeAsync(
        IFormFile file,
        ClaimsPrincipal? user,
        CancellationToken ct)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionId = Guid.NewGuid().ToString("N");
        var ingestRoot = Path.Combine(_env.ContentRootPath, "Data", "receiving-ingest", sessionId);
        Directory.CreateDirectory(ingestRoot);

        var ext = Path.GetExtension(file.FileName);
        var storedName = "source" + (string.IsNullOrEmpty(ext) ? "" : ext);
        var fullPath = Path.Combine(ingestRoot, storedName);
        await using (var fs = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(fs, ct);
        }

        var relPath = Path.Combine("Data", "receiving-ingest", sessionId, storedName).Replace('\\', '/');
        var sha = await ComputeSha256Async(fullPath, ct);

        DuplicateUploadInfo? dup = null;
        var prior = await _db.UploadAuditLogs.AsNoTracking()
            .Where(a => a.FileSha256 == sha && a.ActionTaken != "pending_review")
            .OrderByDescending(a => a.UploadedAt)
            .FirstOrDefaultAsync(ct);
        if (prior != null)
        {
            dup = new DuplicateUploadInfo
            {
                PreviousUploadedAt = prior.UploadedAt,
                PreviousFileName = prior.FileName
            };
        }

        string? extractErr = null;
        string extracted = "";
        await using (var read = System.IO.File.OpenRead(fullPath))
        {
            (extracted, extractErr) = await _extractor.ExtractAsync(read, file.FileName, file.ContentType ?? "", ct);
        }

        var audit = new UploadAuditLog
        {
            FileName = file.FileName,
            FileType = ext.TrimStart('.'),
            FileSha256 = sha,
            DocumentTypeDetected = null,
            ConfidenceLevel = null,
            MatchedRecordId = null,
            MatchedTable = null,
            ActionTaken = string.IsNullOrEmpty(extractErr) && !string.IsNullOrWhiteSpace(extracted)
                ? "pending_review"
                : "failed",
            UploadedByUserId = userId,
            FileStoragePath = relPath,
            ClassificationJson = null,
            Notes = extractErr
        };
        _db.UploadAuditLogs.Add(audit);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(extractErr) || string.IsNullOrWhiteSpace(extracted))
        {
            audit.DocumentTypeDetected = "unreadable";
            audit.Notes = string.IsNullOrEmpty(extractErr) ? "Empty extraction." : extractErr;
            await _db.SaveChangesAsync(ct);
            return new DocumentIngestUploadResponse
            {
                AuditLogId = audit.Id,
                IngestSessionId = sessionId,
                FileName = file.FileName,
                FileType = audit.FileType,
                FileSha256 = sha,
                StoredRelativePath = relPath,
                ExtractionError = string.IsNullOrEmpty(extractErr) ? "Could not extract content. Please check the file." : extractErr,
                ExtractedPreview = extracted,
                Classification = AnthropicDocumentClassifier.HeuristicClassify(""),
                Duplicate = dup,
                StatusMessages = new[] { "Reading document...", "Could not extract content." }
            };
        }

        var registryKeys = await _db.DocumentTypeRegistries.AsNoTracking()
            .Select(r => r.DocumentTypeKey)
            .ToListAsync(ct);

        var classification = await _classifier.ClassifyAsync(extracted, registryKeys, ct);
        audit.DocumentTypeDetected = classification.DocumentType;
        audit.ConfidenceLevel = classification.Confidence;
        audit.ClassificationJson = JsonSerializer.Serialize(classification, JsonOpts);

        DocumentMatchDto match = new() { Matched = false };
        var reg = await _db.DocumentTypeRegistries.AsNoTracking()
            .FirstOrDefaultAsync(r => r.DocumentTypeKey == classification.DocumentType, ct);

        DocumentIngestSessionFile session;
        if (reg != null && !reg.IsSystemType)
        {
            session = new DocumentIngestSessionFile
            {
                AuditLogId = audit.Id,
                FileName = file.FileName,
                Sha256 = sha,
                RelativeFilePath = relPath,
                ExtractedPreview = extracted.Length > 8000 ? extracted[..8000] : extracted,
                ClassificationJson = JsonSerializer.Serialize(classification, JsonOpts),
                RoutedToUserTable = true,
                RoutedTableName = reg.TableName,
                RoutedDocumentTypeKey = reg.DocumentTypeKey,
                HadMatch = false,
                SuggestedOrderId = null
            };
        }
        else
        {
            match = await _matcher.TryMatchAsync(_db, classification, ct);
            if (match is { Matched: true })
            {
                audit.MatchedRecordId = match.MatchedRecordId;
                audit.MatchedTable = match.MatchedTable;
            }

            session = new DocumentIngestSessionFile
            {
                AuditLogId = audit.Id,
                FileName = file.FileName,
                Sha256 = sha,
                RelativeFilePath = relPath,
                ExtractedPreview = extracted.Length > 8000 ? extracted[..8000] : extracted,
                ClassificationJson = JsonSerializer.Serialize(classification, JsonOpts),
                RoutedToUserTable = false,
                RoutedTableName = null,
                RoutedDocumentTypeKey = null,
                HadMatch = match.Matched,
                SuggestedOrderId = match.MatchedRecordId
            };
        }

        await WriteSessionAsync(ingestRoot, session, ct);
        await _db.SaveChangesAsync(ct);

        UserTableRouteDto? route = reg != null && !reg.IsSystemType
            ? new UserTableRouteDto { TableName = reg.TableName, DocumentTypeKey = reg.DocumentTypeKey }
            : null;

        return new DocumentIngestUploadResponse
        {
            AuditLogId = audit.Id,
            IngestSessionId = sessionId,
            FileName = file.FileName,
            FileType = audit.FileType,
            FileSha256 = sha,
            StoredRelativePath = relPath,
            ExtractedPreview = session.ExtractedPreview,
            Classification = classification,
            Match = reg != null && !reg.IsSystemType ? null : match,
            Duplicate = dup,
            UserTableRoute = route,
            StatusMessages = new[]
            {
                "Reading document...",
                "Identifying document type...",
                "Searching for matching records...",
                "Ready for review"
            }
        };
    }

    public async Task<DocumentIngestConfirmResponse> ConfirmAsync(
        DocumentIngestConfirmRequest req,
        ClaimsPrincipal? user,
        CancellationToken ct)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var session = await ReadSessionAsync(req.IngestSessionId, ct);
        if (session == null)
            return new DocumentIngestConfirmResponse { Success = false, Error = "Session expired or not found." };

        if (!string.Equals(session.Sha256, req.FileSha256, StringComparison.OrdinalIgnoreCase))
            return new DocumentIngestConfirmResponse { Success = false, Error = "File hash mismatch; session invalid." };

        var classification = req.Classification ?? ParseClassification(session.ClassificationJson);
        var effectiveType = string.IsNullOrWhiteSpace(req.ManualDocumentType)
            ? classification.DocumentType
            : req.ManualDocumentType.Trim();

        var audit = await _db.UploadAuditLogs.FirstOrDefaultAsync(a => a.Id == session.AuditLogId, ct);
        if (audit == null)
            return new DocumentIngestConfirmResponse { Success = false, Error = "Audit record missing." };

        if (string.Equals(effectiveType, "procurement_order", StringComparison.OrdinalIgnoreCase)
            && session.HadMatch && session.SuggestedOrderId is Guid suggestId
            && !string.Equals(req.ConfirmMode, "create_new", StringComparison.OrdinalIgnoreCase))
        {
            req.MatchedOrderId ??= suggestId;
            req.ConfirmMode = "update_matched";
        }

        try
        {
            if (session.RoutedToUserTable && !string.IsNullOrEmpty(session.RoutedTableName))
            {
                var conn = _db.Database.GetDbConnection();
                var raw = req.CustomRowValues ?? req.GenericKeyFields ?? classification.KeyFields.ToDictionary(k => k.Key, k => k.Value);
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in raw)
                {
                    try
                    {
                        row[_ddl.NormalizeColumnName(kv.Key)] = kv.Value;
                    }
                    catch (ArgumentException)
                    {
                        /* skip unusable keys */
                    }
                }

                await _ddl.InsertRowFlexibleAsync(conn, session.RoutedTableName, row, session.RelativeFilePath, ct);
                audit.ActionTaken = "created";
                audit.DocumentTypeDetected = session.RoutedDocumentTypeKey ?? effectiveType;
                audit.Notes = JsonSerializer.Serialize(new { changes = row }, JsonOpts);
                await _db.SaveChangesAsync(ct);
                return new DocumentIngestConfirmResponse
                {
                    Success = true,
                    ActionTaken = "created",
                    AuditLogId = audit.Id
                };
            }

            if (string.Equals(req.ConfirmMode, "update_matched", StringComparison.OrdinalIgnoreCase)
                && req.MatchedOrderId != null
                && string.Equals(effectiveType, "procurement_order", StringComparison.OrdinalIgnoreCase))
            {
                var order = await _db.Orders.Include(o => o.Lines)
                    .FirstOrDefaultAsync(o => o.OrderId == req.MatchedOrderId, ct);
                if (order == null)
                    return new DocumentIngestConfirmResponse { Success = false, Error = "Matched order no longer exists." };

                var changes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                if (req.OrderFieldUpdates != null)
                {
                    if (req.OrderFieldUpdates.TryGetValue("invoiceNumber", out var inv)) { order.InvoiceNumber = inv; changes["invoiceNumber"] = inv; }
                    if (req.OrderFieldUpdates.TryGetValue("supplierName", out var sup)) { order.SupplierName = sup; changes["supplierName"] = sup; }
                    if (req.OrderFieldUpdates.TryGetValue("notes", out var notes)) { order.Notes = notes; changes["notes"] = notes; }
                }

                foreach (var p in req.LinePatches ?? Enumerable.Empty<OrderLinePatchDto>())
                {
                    var line = order.Lines.FirstOrDefault(l => l.OrderLineId == p.OrderLineId);
                    if (line == null) continue;
                    if (p.QuantityReceived.HasValue) { line.QuantityReceived = p.QuantityReceived.Value; changes[$"line:{line.OrderLineId}:qty"] = p.QuantityReceived; }
                    if (p.Description != null) { line.Description = p.Description; changes[$"line:{line.OrderLineId}:description"] = p.Description; }
                }

                order.UpdatedAt = DateTimeOffset.UtcNow;
                audit.ActionTaken = "updated";
                audit.MatchedRecordId = order.OrderId;
                audit.MatchedTable = "Orders";
                audit.DocumentTypeDetected = effectiveType;
                audit.Notes = JsonSerializer.Serialize(new { changes }, JsonOpts);
                await _db.SaveChangesAsync(ct);

                // Mirror into Phase 0 ProcurementOrder + NewStockBatch so the receiving side
                // picks this up. Match by PO number; create if missing.
                var (procurementOrderId, newStockBatchId) =
                    await UpsertProcurementOrderAndBatchAsync(classification, req, order, userId, ct);

                return new DocumentIngestConfirmResponse
                {
                    Success = true,
                    ActionTaken = "updated",
                    AuditLogId = audit.Id,
                    CreatedOrUpdatedRecordId = order.OrderId,
                    ProcurementOrderId = procurementOrderId,
                    NewStockBatchId = newStockBatchId,
                    NextStepRoute = newStockBatchId.HasValue ? "new-stock-receiving" : null,
                    Message = newStockBatchId.HasValue
                        ? $"{order.OrderNumber} matched. Batch created and ready for receiving."
                        : null
                };
            }

            if (string.Equals(effectiveType, "procurement_order", StringComparison.OrdinalIgnoreCase))
            {
                var kf = req.GenericKeyFields ?? classification.KeyFields;
                var po = FirstValue(kf, "po_number", "PO Number", "Purchase Order", "Order Number") ?? $"AUTO-{Guid.NewGuid():N}"[..12];
                var order = new Order
                {
                    OrderNumber = po,
                    InvoiceNumber = FirstValue(kf, "invoice_number", "Invoice Number"),
                    SupplierName = FirstValue(kf, "supplier", "Supplier", "Supplier Name"),
                    OrderDate = DateTimeOffset.UtcNow,
                    Status = OrderStatus.Draft,
                    Notes = FirstValue(kf, "notes", "Notes", "Financial Year", "financial_year")
                };
                var desc = FirstValue(kf, "Item", "Description", "item") ?? "Imported line";
                var qty = ParseInt(FirstValue(kf, "Qty", "Quantity", "quantity")) ?? 1;
                order.Lines.Add(new OrderLine
                {
                    Description = desc,
                    QuantityOrdered = qty,
                    QuantityReceived = ParseInt(FirstValue(kf, "Quantity Received", "quantity_received")) ?? 0,
                    Brand = FirstValue(kf, "Brand", "brand"),
                    Model = FirstValue(kf, "Model", "model")
                });
                _db.Orders.Add(order);
                audit.ActionTaken = "created";
                audit.MatchedRecordId = order.OrderId;
                audit.MatchedTable = "Orders";
                audit.DocumentTypeDetected = effectiveType;
                audit.Notes = "New procurement order from document ingest.";
                await _db.SaveChangesAsync(ct);

                var (procurementOrderId, newStockBatchId) =
                    await UpsertProcurementOrderAndBatchAsync(classification, req, order, userId, ct);

                return new DocumentIngestConfirmResponse
                {
                    Success = true,
                    ActionTaken = "created",
                    AuditLogId = audit.Id,
                    CreatedOrUpdatedRecordId = order.OrderId,
                    ProcurementOrderId = procurementOrderId,
                    NewStockBatchId = newStockBatchId,
                    NextStepRoute = newStockBatchId.HasValue ? "new-stock-receiving" : null,
                    Message = newStockBatchId.HasValue
                        ? "New order created from document. Ready for receiving."
                        : null
                };
            }

            if (IsGenericBuiltIn(effectiveType))
            {
                var payload = JsonSerializer.Serialize(new
                {
                    keyFields = req.GenericKeyFields ?? classification.KeyFields,
                    tables = classification.Tables
                }, JsonOpts);
                Guid? linked = null;
                var poLink = FirstValue(classification.KeyFields, "po_number", "PO Number", "Purchase Order");
                if (!string.IsNullOrWhiteSpace(poLink))
                {
                    linked = await _db.Orders.AsNoTracking()
                        .Where(o => o.OrderNumber == poLink.Trim())
                        .Select(o => (Guid?)o.OrderId)
                        .FirstOrDefaultAsync(ct);
                }

                var doc = new ReceivingGenericDocument
                {
                    DocumentKind = effectiveType,
                    PayloadJson = payload,
                    SourceFilePath = session.RelativeFilePath,
                    LinkedProcurementOrderId = linked,
                    CreatedByUserId = userId
                };
                _db.ReceivingGenericDocuments.Add(doc);
                audit.ActionTaken = "created";
                audit.MatchedRecordId = doc.Id;
                audit.MatchedTable = "receiving_generic_documents";
                audit.DocumentTypeDetected = effectiveType;
                audit.Notes = null;
                await _db.SaveChangesAsync(ct);

                var nextRoute = effectiveType.ToLowerInvariant() switch
                {
                    "delivery_note" or "proof_of_delivery" => "delivery-tracking",
                    "invoice" or "financial_report" => "financial-reconciliation",
                    _ => null
                };

                return new DocumentIngestConfirmResponse
                {
                    Success = true,
                    ActionTaken = "created",
                    AuditLogId = audit.Id,
                    CreatedOrUpdatedRecordId = doc.Id,
                    NextStepRoute = nextRoute
                };
            }

            if (string.Equals(effectiveType, "unknown", StringComparison.OrdinalIgnoreCase)
                || string.Equals(req.ConfirmMode, "create_custom_table", StringComparison.OrdinalIgnoreCase))
            {
                var tableName = _ddl.NormalizeTableName(req.CustomTableName ?? classification.SuggestedTableName ?? "ing_custom_doc");
                var display = string.IsNullOrWhiteSpace(req.CustomDisplayName) ? tableName : req.CustomDisplayName.Trim();
                var typeKey = SanitizeTypeKey(req.CustomDocumentTypeKey ?? tableName.Replace("ing_", ""));

                var cols = (req.CustomColumns ?? new List<CustomColumnDefDto>())
                    .Where(c => c.Include && !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => (_ddl.NormalizeColumnName(c.Name), _ddl.MapSqlType(c.DataType)))
                    .ToList();

                if (cols.Count == 0 && classification.SuggestedSchema is { Count: > 0 })
                {
                    cols = classification.SuggestedSchema
                        .Select(kv => (_ddl.NormalizeColumnName(kv.Key), _ddl.MapSqlType(kv.Value)))
                        .ToList();
                }

                if (cols.Count == 0)
                    return new DocumentIngestConfirmResponse { Success = false, Error = "No columns selected for the new table." };

                var conn = _db.Database.GetDbConnection();
                await _ddl.CreateUserTableAsync(conn, tableName, cols, ct);

                var colNameSet = cols.Select(c => c.Item1).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var rawRow = req.CustomRowValues ?? classification.KeyFields;
                var rowFiltered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in rawRow)
                {
                    try
                    {
                        var n = _ddl.NormalizeColumnName(kv.Key);
                        if (colNameSet.Contains(n))
                            rowFiltered[n] = kv.Value;
                    }
                    catch (ArgumentException)
                    {
                        /* skip */
                    }
                }

                await _ddl.InsertRowFlexibleAsync(conn, tableName, rowFiltered, session.RelativeFilePath, ct);

                var schemaJson = JsonSerializer.Serialize(cols.ToDictionary(c => c.Item1, c => c.Item2), JsonOpts);
                var baseKey = typeKey;
                for (var i = 0; i < 30; i++)
                {
                    var candidate = i == 0 ? baseKey : $"{baseKey}_{i}";
                    if (candidate.Length > 80) candidate = candidate[..80];
                    if (!await _db.DocumentTypeRegistries.AnyAsync(r => r.DocumentTypeKey == candidate, ct))
                    {
                        typeKey = candidate;
                        break;
                    }
                }

                _db.DocumentTypeRegistries.Add(new DocumentTypeRegistry
                {
                    TableName = tableName,
                    DisplayName = display,
                    DocumentTypeKey = typeKey,
                    IsSystemType = false,
                    SchemaJson = schemaJson,
                    CreatedByUserId = userId,
                    SampleFileName = session.FileName
                });

                audit.ActionTaken = "new_table_created";
                audit.MatchedTable = tableName;
                audit.DocumentTypeDetected = typeKey;
                audit.Notes = schemaJson;
                await _db.SaveChangesAsync(ct);
                return new DocumentIngestConfirmResponse
                {
                    Success = true,
                    ActionTaken = "new_table_created",
                    AuditLogId = audit.Id
                };
            }

            return new DocumentIngestConfirmResponse { Success = false, Error = "Unsupported document type for this confirmation." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document ingest confirm failed.");
            audit.ActionTaken = "failed";
            audit.Notes = ex.Message;
            await _db.SaveChangesAsync(ct);
            return new DocumentIngestConfirmResponse { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Upsert a Phase 0 ProcurementOrder for the classified document and mirror it into a
    /// NewStockBatch via <see cref="ProcurementOrderBatchSyncService"/>. Returns
    /// (ProcurementOrderId, NewStockBatchId) when anything was created or refreshed.
    /// </summary>
    private async Task<(Guid? procurementOrderId, Guid? newStockBatchId)> UpsertProcurementOrderAndBatchAsync(
        DocumentClassificationResult classification,
        DocumentIngestConfirmRequest req,
        Order legacyOrder,
        string? userId,
        CancellationToken ct)
    {
        try
        {
            var kf = req.GenericKeyFields ?? classification.KeyFields;
            var poNumber = FirstValue(kf, "po_number", "PO Number", "Purchase Order", "Order Number")?.Trim();
            if (string.IsNullOrEmpty(poNumber))
            {
                poNumber = legacyOrder.OrderNumber;
            }
            if (string.IsNullOrEmpty(poNumber))
            {
                return (null, null);
            }

            var po = await _coreDb.ProcurementOrders
                .Include(o => o.Schools)
                .ThenInclude(s => s.Items)
                .FirstOrDefaultAsync(o => o.PoNumber == poNumber, ct);

            var supplier = FirstValue(kf, "supplier", "Supplier", "Supplier Name") ?? legacyOrder.SupplierName;
            var project = FirstValue(kf, "project", "Project", "Project Name") ?? legacyOrder.OrderNumber;
            var financialYear = FirstValue(kf, "financial_year", "Financial Year", "FY") ?? "";
            var schoolName = FirstValue(kf, "school", "School", "School Name");
            var description = FirstValue(kf, "item", "Description", "Item") ?? "Imported from document";
            var brand = FirstValue(kf, "brand", "Brand");
            var model = FirstValue(kf, "model", "Model");
            var deviceType = FirstValue(kf, "device_type", "DeviceType", "Device Type", "Type");
            var qty = ParseInt(FirstValue(kf, "qty", "Quantity", "Qty")) ?? 1;
            var unitPrice = ParseDecimal(FirstValue(kf, "unit_price", "Unit Price", "Price")) ?? 0m;
            var totalPrice = ParseDecimal(FirstValue(kf, "total_price", "Total", "Line Total")) ?? unitPrice * qty;

            if (po == null)
            {
                po = new ProcurementOrder
                {
                    ProcurementOrderId = Guid.NewGuid(),
                    PoNumber = poNumber,
                    ProjectName = string.IsNullOrWhiteSpace(project) ? poNumber : project,
                    FinancialYear = string.IsNullOrWhiteSpace(financialYear) ? "Unknown" : financialYear,
                    SupplierName = supplier,
                    TotalOrderValue = totalPrice,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Schools = new List<ProcurementOrderSchool>
                    {
                        new()
                        {
                            ProcurementOrderSchoolId = Guid.NewGuid(),
                            SchoolName = string.IsNullOrWhiteSpace(schoolName) ? "Unspecified School" : schoolName,
                            SchoolSubTotal = totalPrice,
                            Items = new List<ProcurementOrderItem>
                            {
                                new()
                                {
                                    ProcurementOrderItemId = Guid.NewGuid(),
                                    Description = description,
                                    Brand = brand,
                                    Model = model,
                                    DeviceType = deviceType,
                                    UnitPrice = unitPrice,
                                    QtyOrdered = qty,
                                    TotalPrice = totalPrice,
                                    DeliveryStatus = SchoolItemDeliveryStatus.Pending
                                }
                            }
                        }
                    }
                };
                _coreDb.ProcurementOrders.Add(po);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(supplier)) po.SupplierName = supplier;
                if (!string.IsNullOrWhiteSpace(project)) po.ProjectName = project;
                if (!string.IsNullOrWhiteSpace(financialYear)) po.FinancialYear = financialYear;
                po.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _coreDb.SaveChangesAsync(ct);
            var createdBy = userId ?? "document-ingest";
            var batchId = await _batchSync.SyncBatchForOrderAsync(po.ProcurementOrderId, createdBy, ct);
            return (po.ProcurementOrderId, batchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert ProcurementOrder + NewStockBatch from document ingest.");
            return (null, null);
        }
    }

    private static decimal? ParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var cleaned = new string(s.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-').ToArray())
            .Replace(",", "");
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static bool IsGenericBuiltIn(string t) =>
        t.Equals("delivery_note", StringComparison.OrdinalIgnoreCase)
        || t.Equals("invoice", StringComparison.OrdinalIgnoreCase)
        || t.Equals("proof_of_delivery", StringComparison.OrdinalIgnoreCase)
        || t.Equals("stock_receipt", StringComparison.OrdinalIgnoreCase)
        || t.Equals("financial_report", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeTypeKey(string s)
    {
        var x = s.Trim().ToLowerInvariant().Replace(' ', '_');
        x = System.Text.RegularExpressions.Regex.Replace(x, @"[^a-z0-9_]", "");
        if (string.IsNullOrEmpty(x)) x = "custom_" + Guid.NewGuid().ToString("N")[..8];
        if (x.Length > 80) x = x[..80];
        return x;
    }

    private static string? FirstValue(IReadOnlyDictionary<string, string> kf, params string[] keys)
    {
        foreach (var key in keys)
        {
            foreach (var p in kf)
                if (string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.Value))
                    return p.Value.Trim();
        }

        return null;
    }

    private static int? ParseInt(string? s) => int.TryParse(s, out var n) ? n : null;

    private static DocumentClassificationResult ParseClassification(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DocumentClassificationResult();
        try
        {
            return JsonSerializer.Deserialize<DocumentClassificationResult>(json, JsonOpts) ?? new DocumentClassificationResult();
        }
        catch
        {
            return new DocumentClassificationResult();
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var fs = System.IO.File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash);
    }

    private static async Task WriteSessionAsync(string ingestRoot, DocumentIngestSessionFile session, CancellationToken ct)
    {
        var path = Path.Combine(ingestRoot, "session.json");
        await System.IO.File.WriteAllTextAsync(path, JsonSerializer.Serialize(session, JsonOpts), ct);
    }

    private async Task<DocumentIngestSessionFile?> ReadSessionAsync(string ingestSessionId, CancellationToken ct)
    {
        var path = Path.Combine(_env.ContentRootPath, "Data", "receiving-ingest", ingestSessionId, "session.json");
        if (!System.IO.File.Exists(path)) return null;
        var json = await System.IO.File.ReadAllTextAsync(path, ct);
        try
        {
            return JsonSerializer.Deserialize<DocumentIngestSessionFile>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private sealed class DocumentIngestSessionFile
    {
        public Guid AuditLogId { get; set; }
        public string FileName { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public string RelativeFilePath { get; set; } = "";
        public string ExtractedPreview { get; set; } = "";
        public string ClassificationJson { get; set; } = "{}";
        public bool RoutedToUserTable { get; set; }
        public string? RoutedTableName { get; set; }
        public string? RoutedDocumentTypeKey { get; set; }
        public bool HadMatch { get; set; }
        public Guid? SuggestedOrderId { get; set; }
    }
}
