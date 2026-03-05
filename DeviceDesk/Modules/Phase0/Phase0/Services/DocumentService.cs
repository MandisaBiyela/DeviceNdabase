using DeviceDesk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase0.Services
{
    public class DocumentService
    {
        private readonly DeviceDeskDbContext _db;
        public DocumentService(DeviceDeskDbContext db) => _db = db;

        public async Task<(long id, string fileName, string docType)> SaveForBatchAsync(Guid batchId, IFormFile file, string docType, CancellationToken ct)
        {
            if (file == null || file.Length == 0) throw new InvalidOperationException("No file");
            var exists = await _db.Batches.AnyAsync(b => b.BatchId == batchId, ct);
            if (!exists) throw new InvalidOperationException("Unknown batch");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            var doc = new Document
            {
                BatchId = batchId,
                DocType = docType,
                FileName = file.FileName,
                ContentType = file.ContentType ?? "application/octet-stream",
                FileData = ms.ToArray()
            };
            _db.Documents.Add(doc);
            await _db.SaveChangesAsync(ct);
            return (doc.DocumentId, doc.FileName, doc.DocType);
        }

        public async Task<(long id, string fileName, string docType)> SaveLooseAsync(IFormFile file, string docType, CancellationToken ct)
        {
            if (file == null || file.Length == 0) throw new InvalidOperationException("No file");
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            var doc = new Document
            {
                BatchId = null,
                SchoolId = null,
                DocType = docType,
                FileName = file.FileName,
                ContentType = file.ContentType ?? "application/octet-stream",
                FileData = ms.ToArray()
            };
            _db.Documents.Add(doc);
            await _db.SaveChangesAsync(ct);
            return (doc.DocumentId, doc.FileName, doc.DocType);
        }

        public async Task<List<object>> GetDocumentsForBatchAsync(Guid batchId, CancellationToken ct)
        {
            var documents = await _db.Documents
                .Where(d => d.BatchId == batchId)
                .Select(d => new {
                    d.DocumentId,
                    d.FileName,
                    d.DocType,
                    d.ContentType,
                    d.UploadedAt,
                    FileSizeBytes = d.FileData.Length
                })
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync(ct);

            return documents.Cast<object>().ToList();
        }

        public async Task<Document?> GetDocumentAsync(long documentId, CancellationToken ct)
        {
            return await _db.Documents
                .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
        }

        public async Task<bool> DeleteDocumentAsync(long documentId, CancellationToken ct)
        {
            var document = await _db.Documents
                .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
            
            if (document == null) return false;
            
            _db.Documents.Remove(document);
            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}