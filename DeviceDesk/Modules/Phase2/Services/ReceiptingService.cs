using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Infrastructure.Data; // Phase1DbContext namespace
using DeviceDesk.Modules.Phase1.Models; // RnrScanStatus, ReceivingBatchScan
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services;

public class ReceiptingService
{
    private readonly Phase2DbContext _db;
    private readonly DeviceDeskDbContext _coreDb;
    private readonly AuditService _audit;
    private readonly Phase1DbContext _phase1;
    public ReceiptingService(Phase2DbContext db, DeviceDeskDbContext coreDb, AuditService audit, Phase1DbContext phase1)
    {
        _db = db;
        _coreDb = coreDb;
        _audit = audit;
        _phase1 = phase1;
    }

    private static string NormalizeSerial(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace(" ", "").Replace("-", "");

    // Step 1.1-1.4: Scan, Verify, Accept, Categorize
    public async Task<Receipt> CreateReceiptAsync(string grvNumber, string clerkId, IEnumerable<(string serial, Phase2Zone zone)> items)
    {
        // Validate GRV exists in Phase 1
        var grv = await _phase1.GoodsReceivedNotes.FirstOrDefaultAsync(g => g.GRVNumber == grvNumber)
                  ?? throw new InvalidOperationException($"GRV {grvNumber} not found in Phase 1.");

        // 1) Normalise requested serials from UI and make distinct
        var requestedSerials = items
            .Select(i => NormalizeSerial(i.serial))
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 2) Normal case: use ReceivingBatchItems (Phase 1 new stock flow)
        var validSerials = await _phase1.ReceivingBatchItems
            .Where(i => i.ReceivingBatchId == grv.ReceivingBatchId && i.SerialNumber != null)
            .Select(i => NormalizeSerial(i.SerialNumber))
            .ToListAsync();

        // 3) R&R fallback: if no items exist, derive valid list from scans excluding duplicates
        if (!validSerials.Any())
        {
            validSerials = await _phase1.ReceivingBatchScans
                .Where(s => s.BatchId == grv.ReceivingBatchId && s.Status != RnrScanStatus.Duplicate)
                .Select(s => NormalizeSerial(s.Serial))
                .ToListAsync();
        }

        var validSet = new HashSet<string>(validSerials, StringComparer.OrdinalIgnoreCase);
        var missing = requestedSerials.Where(s => !validSet.Contains(s)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Serials not in GRV {grvNumber}: {string.Join(", ", missing)}");
        }

        // 4) Block duplicates already receipted in Phase 2 (normalize for comparison)
        var existingDbSerials = await _db.Devices.Select(d => d.Serial).ToListAsync();
        var existingNormalized = new HashSet<string>(existingDbSerials.Select(NormalizeSerial), StringComparer.OrdinalIgnoreCase);
        var duplicates = requestedSerials.Where(s => existingNormalized.Contains(s)).ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException($"Serials already receipted: {string.Join(", ", duplicates)}");
        }

        // Create receipt and devices
        var receipt = new Receipt { GrvNumber = grvNumber, ItemCount = items.Count() };

        // Load batch with CollectionSlip to get school info as fallback
        var batch = await _phase1.ReceivingBatches
            .Include(b => b.CollectionSlip)
            .FirstOrDefaultAsync(b => b.ReceivingBatchId == grv.ReceivingBatchId);

        // Get school info from batch/slip as fallback
        long? batchSchoolId = batch?.CollectionSlip?.SchoolId ?? batch?.SchoolId;
        string? batchSchoolName = batch?.CollectionSlip?.SchoolName;
        
        // If batch school name is empty, try to look it up from Schools table
        if (batchSchoolId.HasValue && string.IsNullOrEmpty(batchSchoolName))
        {
            var school = await _coreDb.Schools
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SchoolId == batchSchoolId.Value);
            batchSchoolName = school?.Name;
        }

        foreach (var it in items)
        {
            // Look up core device to get school info
            var coreDevice = await _coreDb.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.SerialNumber == it.serial.Trim());

            // Prefer Device record, fallback to batch/slip school info
            int? schoolId = null;
            string? schoolName = null;
            
            if (coreDevice != null && coreDevice.SchoolId.HasValue)
            {
                schoolId = (int)coreDevice.SchoolId.Value;
                schoolName = coreDevice.SchoolName;
            }
            
            // Fallback to batch/slip if Device doesn't have school info OR school name is missing
            if ((schoolId == null || string.IsNullOrWhiteSpace(schoolName)) && batch != null)
            {
                // prefer batch.SchoolId / CollectionSlip.SchoolName (from collection slip) as a fallback
                schoolId = schoolId ?? (batch.SchoolId.HasValue ? (int?)batch.SchoolId.Value : null);
                schoolName = !string.IsNullOrWhiteSpace(schoolName) ? schoolName : batch.CollectionSlip?.SchoolName;
            }

            // CRITICAL: If we have SchoolId but SchoolName is null/empty, look it up from Schools table
            if (schoolId.HasValue && string.IsNullOrEmpty(schoolName))
            {
                var school = await _coreDb.Schools
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SchoolId == schoolId.Value);
                schoolName = school?.Name;
            }

            receipt.Devices.Add(new Phase2Device
            {
                Serial = it.serial.Trim(),
                Zone = it.zone,
                Stage = Phase2Stage.Received,
                IctClerkId = clerkId,
                ReceivingDate = DateTime.UtcNow,
                VerificationStatus = true,
                SchoolId = schoolId,
                SchoolName = schoolName
            });
        }
        _db.Receipts.Add(receipt);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(clerkId, "ReceiptCreated", details: $"GRV: {grvNumber}, Items: {items.Count()}");
        return receipt;
    }

    // Pending GRVs for ICT Clerk to receipt (Phase 1 GRVs not yet receipted in Phase 2)
    public record PendingGrvDto(string GrvNumber, DateTimeOffset IssueDate, string IssuerName, int DeviceCount, string IssuerEmail);

    public async Task<List<PendingGrvDto>> GetPendingGrvsAsync()
    {
        // All GRVs issued in Phase 1
        var issuedGrvs = await _phase1.GoodsReceivedNotes
            .AsNoTracking()
            .ToListAsync();

        // GRVs already receipted in Phase 2
        var receiptedNumbers = await _db.Receipts
            .AsNoTracking()
            .Select(r => r.GrvNumber)
            .ToListAsync();

        var pending = issuedGrvs
            .Where(g => !receiptedNumbers.Contains(g.GRVNumber))
            .Select(g => new PendingGrvDto(
                g.GRVNumber,
                g.GRVDate,
                g.ReceivedBy ?? "Warehouse Receiving",
                g.TotalQuantity,
                "receiving.clerk@local" // fallback Phase 1 clerk email
            ))
            .ToList();

        return pending;
    }
}
