using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Services
{
    public class ScanningService
    {
        private readonly Phase1DbContext _db;
        private readonly DeviceDeskDbContext _legacyDb;

        public ScanningService(Phase1DbContext db, DeviceDeskDbContext legacyDb)
        {
            _db = db;
            _legacyDb = legacyDb;
        }

        public async Task<ScanValidationResponse> ValidateAndScanDeviceAsync(
            ScanDeviceRequest request, 
            CancellationToken ct = default)
        {
            // Get the receiving batch
            var batch = await _db.ReceivingBatches
                .Include(b => b.Items)
                .Include(b => b.Order)
                    .ThenInclude(o => o!.Lines)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == request.ReceivingBatchId, ct);

            if (batch == null)
                throw new InvalidOperationException("Receiving batch not found.");

            // Validate input
            var identifier = !string.IsNullOrWhiteSpace(request.SerialNumber) 
                ? request.SerialNumber.Trim() 
                : request.IMEI?.Trim();

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return new ScanValidationResponse(
                    false,
                    ScanValidationResult.InvalidFormat,
                    "Serial Number or IMEI is required.",
                    null, null, null,
                    batch.Items.Count,
                    GetExpectedCount(batch)
                );
            }

            // Check for duplicate in current batch
            var duplicateInBatch = batch.Items.Any(i => 
                (i.SerialNumber != null && i.SerialNumber.Equals(identifier, StringComparison.OrdinalIgnoreCase)) ||
                (i.IMEI != null && i.IMEI.Equals(identifier, StringComparison.OrdinalIgnoreCase)));

            if (duplicateInBatch)
            {
                return new ScanValidationResponse(
                    false,
                    ScanValidationResult.DuplicateInBatch,
                    $"⚠️ DUPLICATE: This device has already been scanned in this batch!",
                    batch.ReceivingBatchId.ToString(),
                    null, null,
                    batch.Items.Count,
                    GetExpectedCount(batch)
                );
            }

            // Check for duplicate in other batches (Phase 1)
            var duplicateInOtherBatch = await _db.ReceivingBatchItems
                .Where(i => i.ReceivingBatchId != request.ReceivingBatchId)
                .Where(i => 
                    (i.SerialNumber != null && i.SerialNumber == identifier) ||
                    (i.IMEI != null && i.IMEI == identifier))
                .Select(i => i.ReceivingBatchId)
                .FirstOrDefaultAsync(ct);

            if (duplicateInOtherBatch != Guid.Empty)
            {
                return new ScanValidationResponse(
                    false,
                    ScanValidationResult.DuplicateInSystem,
                    $"⚠️ DUPLICATE: This device was already received in batch {duplicateInOtherBatch}!",
                    duplicateInOtherBatch.ToString(),
                    null, null,
                    batch.Items.Count,
                    GetExpectedCount(batch)
                );
            }

            // Check for duplicate in legacy Phase 0 system
            var duplicateInLegacy = await _legacyDb.Devices
                .AnyAsync(d => 
                    (d.SerialNumber != null && d.SerialNumber == identifier) ||
                    (d.IMEI != null && d.IMEI == identifier), ct);

            if (duplicateInLegacy)
            {
                return new ScanValidationResponse(
                    false,
                    ScanValidationResult.DuplicateInSystem,
                    $"⚠️ DUPLICATE: This device already exists in the system (Phase 0)!",
                    "Phase0",
                    null, null,
                    batch.Items.Count,
                    GetExpectedCount(batch)
                );
            }

            // Model verification for New Stock orders
            if (batch.SourceType == ReceivingSourceType.NewStock && batch.Order?.Lines != null)
            {
                var expectedModels = batch.Order.Lines
                    .Select(l => new { l.Brand, l.Model })
                    .Distinct()
                    .ToList();

                var modelMatch = expectedModels.Any(m => 
                    (string.IsNullOrWhiteSpace(m.Brand) || m.Brand.Equals(request.Brand, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(m.Model) || m.Model.Equals(request.Model, StringComparison.OrdinalIgnoreCase)));

                if (!modelMatch && expectedModels.Any())
                {
                    var expected = expectedModels.First();
                    return new ScanValidationResponse(
                        false,
                        ScanValidationResult.ModelMismatch,
                        $"⚠️ MODEL MISMATCH: Expected {expected.Brand} {expected.Model}, but scanned {request.Brand} {request.Model}",
                        null,
                        expected.Brand,
                        expected.Model,
                        batch.Items.Count,
                        GetExpectedCount(batch)
                    );
                }
            }

            // All validations passed - add the device
            var item = new ReceivingBatchItem
            {
                ReceivingBatchId = request.ReceivingBatchId,
                SerialNumber = request.SerialNumber?.Trim(),
                IMEI = request.IMEI?.Trim(),
                Brand = request.Brand?.Trim(),
                Model = request.Model?.Trim(),
                Notes = request.Notes?.Trim()
            };

            _db.ReceivingBatchItems.Add(item);
            await _db.SaveChangesAsync(ct);

            return new ScanValidationResponse(
                true,
                ScanValidationResult.Valid,
                $"✓ Device scanned successfully! ({batch.Items.Count + 1} of {GetExpectedCount(batch)})",
                null, null, null,
                batch.Items.Count + 1,
                GetExpectedCount(batch)
            );
        }

        public async Task<List<ScannedDeviceDto>> GetScannedDevicesAsync(
            Guid receivingBatchId, 
            CancellationToken ct = default)
        {
            var items = await _db.ReceivingBatchItems
                .Where(i => i.ReceivingBatchId == receivingBatchId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync(ct);

            return items.Select(i => new ScannedDeviceDto(
                i.ReceivingBatchItemId,
                i.SerialNumber,
                i.IMEI,
                i.Brand,
                i.Model,
                i.Notes,
                i.CreatedAt
            )).ToList();
        }

        public async Task<bool> DeleteScannedDeviceAsync(
            Guid receivingBatchItemId, 
            CancellationToken ct = default)
        {
            var item = await _db.ReceivingBatchItems.FindAsync(new object[] { receivingBatchItemId }, ct);
            if (item == null) return false;

            _db.ReceivingBatchItems.Remove(item);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        private int GetExpectedCount(ReceivingBatch batch)
        {
            if (batch.SourceType == ReceivingSourceType.NewStock && batch.Order?.Lines != null)
            {
                return batch.Order.Lines.Sum(l => l.QuantityOrdered);
            }
            return 0; // Unknown for RnR
        }
    }
}
