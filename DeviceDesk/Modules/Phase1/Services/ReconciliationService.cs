using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Services
{
    public class ReconciliationService
    {
        private readonly Phase1DbContext _db;

        public ReconciliationService(Phase1DbContext db)
        {
            _db = db;
        }

        public async Task<ReconciliationStatusDto> StartScanningAsync(StartScanningRequest request, CancellationToken ct = default)
        {
            var batch = await _db.ReceivingBatches
                .Include(b => b.Order).ThenInclude(o => o!.Lines)
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == request.ReceivingBatchId, ct);

            if (batch == null)
                throw new InvalidOperationException("Receiving batch not found.");

            if (batch.IsLocked)
                throw new InvalidOperationException("Batch is locked and cannot be modified.");

            batch.Status = ReceivingBatchStatus.ScanningInProgress;
            batch.ScanningOfficer = request.ScanningOfficer;
            batch.ScanningStartedAt = DateTimeOffset.UtcNow;
            batch.UpdatedAt = DateTimeOffset.UtcNow;

            // Set expected count for New Stock
            if (batch.SourceType == ReceivingSourceType.NewStock && batch.Order?.Lines != null)
            {
                batch.ExpectedCount = batch.Order.Lines.Sum(l => l.QuantityOrdered);
            }

            await _db.SaveChangesAsync(ct);

            return MapToReconciliationStatus(batch);
        }

        public async Task<ReconciliationStatusDto> CompleteScanningAsync(CompleteScanningRequest request, CancellationToken ct = default)
        {
            var batch = await _db.ReceivingBatches
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == request.ReceivingBatchId, ct);

            if (batch == null)
                throw new InvalidOperationException("Receiving batch not found.");

            if (batch.Status != ReceivingBatchStatus.ScanningInProgress)
                throw new InvalidOperationException("Batch is not in scanning mode.");

            batch.Status = ReceivingBatchStatus.PendingVerification;
            batch.ScanningCompletedAt = DateTimeOffset.UtcNow;
            batch.ActualCount = batch.Items.Count;
            batch.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            return MapToReconciliationStatus(batch);
        }

        public async Task<ReconciliationStatusDto> SubmitCountAsync(SubmitCountRequest request, CancellationToken ct = default)
        {
            var batch = await _db.ReceivingBatches
                .Include(b => b.Items)
                .Include(b => b.Order).ThenInclude(o => o!.Lines)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == request.ReceivingBatchId, ct);

            if (batch == null)
                throw new InvalidOperationException("Receiving batch not found.");

            if (batch.Status != ReceivingBatchStatus.PendingVerification)
                throw new InvalidOperationException("Batch is not pending verification.");

            batch.ActualCount = batch.Items.Count;
            batch.VarianceCount = batch.ActualCount - batch.ExpectedCount;
            batch.HasVariance = batch.VarianceCount != 0;
            batch.VerifiedBy = request.VerifiedBy;
            batch.VerifiedAt = DateTimeOffset.UtcNow;
            batch.UpdatedAt = DateTimeOffset.UtcNow;

            if (batch.HasVariance)
            {
                // Variance detected
                batch.Status = ReceivingBatchStatus.VarianceDetected;
            }
            else
            {
                // Counts match - proceed to verified
                batch.Status = ReceivingBatchStatus.Verified;
                batch.IsLocked = true; // Lock serials
            }

            await _db.SaveChangesAsync(ct);

            return MapToReconciliationStatus(batch);
        }

        public async Task<ReconciliationStatusDto> ResolveVarianceAsync(ResolveVarianceRequest request, CancellationToken ct = default)
        {
            var batch = await _db.ReceivingBatches
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == request.ReceivingBatchId, ct);

            if (batch == null)
                throw new InvalidOperationException("Receiving batch not found.");

            if (batch.Status != ReceivingBatchStatus.VarianceDetected)
                throw new InvalidOperationException("Batch does not have a variance to resolve.");

            batch.VarianceResolution = request.Resolution;
            batch.VarianceReason = request.Reason;
            batch.UpdatedAt = DateTimeOffset.UtcNow;

            switch (request.Resolution)
            {
                case Models.VarianceResolution.Recount:
                    // Reset to scanning mode
                    batch.Status = ReceivingBatchStatus.ScanningInProgress;
                    batch.HasVariance = false;
                    batch.VarianceCount = 0;
                    // Clear scanned items for recount
                    _db.ReceivingBatchItems.RemoveRange(batch.Items);
                    break;

                case Models.VarianceResolution.SupervisorApproval:
                    if (string.IsNullOrWhiteSpace(request.SupervisorName))
                        throw new InvalidOperationException("Supervisor name is required for approval.");
                    
                    batch.SupervisorApprovedBy = request.SupervisorName;
                    batch.SupervisorApprovedAt = DateTimeOffset.UtcNow;
                    batch.Status = ReceivingBatchStatus.Verified;
                    batch.IsLocked = true;
                    break;

                case Models.VarianceResolution.SupplierError:
                case Models.VarianceResolution.NCRIssued:
                    // Mark as verified with variance documented
                    batch.Status = ReceivingBatchStatus.Verified;
                    batch.IsLocked = true;
                    break;
            }

            await _db.SaveChangesAsync(ct);

            return MapToReconciliationStatus(batch);
        }

        public async Task<ReconciliationStatusDto> GetReconciliationStatusAsync(Guid batchId, CancellationToken ct = default)
        {
            var batch = await _db.ReceivingBatches
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == batchId, ct);

            if (batch == null)
                throw new InvalidOperationException("Receiving batch not found.");

            return MapToReconciliationStatus(batch);
        }

        private ReconciliationStatusDto MapToReconciliationStatus(ReceivingBatch batch)
        {
            bool canSubmitCount = batch.Status == ReceivingBatchStatus.PendingVerification;
            bool canRecount = batch.Status == ReceivingBatchStatus.VarianceDetected;
            bool requiresSupervisor = batch.Status == ReceivingBatchStatus.VarianceDetected && batch.HasVariance;

            return new ReconciliationStatusDto(
                batch.ReceivingBatchId,
                batch.Status,
                batch.Status.ToString(),
                batch.ExpectedCount,
                batch.ActualCount,
                batch.VarianceCount,
                batch.HasVariance,
                batch.VarianceReason,
                batch.IsLocked,
                canSubmitCount,
                canRecount,
                requiresSupervisor
            );
        }
    }
}
