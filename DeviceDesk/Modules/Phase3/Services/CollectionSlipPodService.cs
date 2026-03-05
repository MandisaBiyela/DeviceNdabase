using DeviceDesk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase3.Services
{
    public class CollectionSlipPodService
    {
        private readonly DeviceDeskDbContext _coreDb;
        private readonly ILogger<CollectionSlipPodService> _logger;

        public CollectionSlipPodService(DeviceDeskDbContext coreDb, ILogger<CollectionSlipPodService> logger)
        {
            _coreDb = coreDb;
            _logger = logger;
        }

        /// <summary>
        /// Validate that a collection slip (RnrBatch) is ready for dispatch
        /// </summary>
        public async Task<(bool isValid, string? error, RnrBatch? batch)> ValidateCollectionSlipForDispatch(
            Guid rnrBatchId, 
            CancellationToken ct = default)
        {
            var batch = await _coreDb.RnrBatches
                .FirstOrDefaultAsync(b => b.BatchId == rnrBatchId, ct);
            
            if (batch == null)
                return (false, "Collection slip not found", null);
            
            if (batch.Status < RnrBatchStatus.Verified)
                return (false, "Collection slip must be verified before dispatch", batch);
            
            if (batch.Status >= RnrBatchStatus.Completed)
                return (false, "Collection slip is already completed", batch);
            
            if (batch.TotalQuantityScanned == 0)
                return (false, "Collection slip has no scanned devices", batch);
            
            return (true, null, batch);
        }

        /// <summary>
        /// Create or update a POD from a collection slip (RnrBatch)
        /// </summary>
        public async Task<(DispatchPod pod, bool isNew)> CreateOrUpdatePodFromCollectionSlip(
            Guid rnrBatchId, 
            string userId, 
            CancellationToken ct = default)
        {
            // Validate collection slip
            var (isValid, error, batch) = await ValidateCollectionSlipForDispatch(rnrBatchId, ct);
            if (!isValid || batch == null)
                throw new InvalidOperationException(error ?? "Collection slip validation failed");
            
            // Check if POD already exists for this collection slip
            var existingPod = await _coreDb.DispatchPods
                .FirstOrDefaultAsync(p => p.RnrBatchId == rnrBatchId, ct);
            
            bool isNew = existingPod == null;
            DispatchPod pod;
            
            if (isNew)
            {
                // Generate POD number
                var now = DateTime.UtcNow;
                var yearPrefix = $"POD-{now:yyyy}-";
                var dailyCount = await _coreDb.DispatchPods
                    .Where(p => p.PodNumber.StartsWith(yearPrefix))
                    .CountAsync(ct);
                
                var podNumber = $"POD-{now:yyyy}-{(dailyCount + 1):D3}";
                
                // Create new POD
                pod = new DispatchPod
                {
                    PodNumber = podNumber,
                    SchoolName = batch.SchoolName ?? "Unknown School",
                    District = null, // Will be set from school lookup below
                    EmisCode = null, // Will be set from school lookup below
                    StockType = "R&R",
                    SourceReference = batch.CollectionSlipNumber,
                    Status = DispatchPodStatus.Ready,
                    RnrBatchId = rnrBatchId,
                    CollectionSlipNumber = batch.CollectionSlipNumber,
                    IsLockedToCollectionSlip = true,
                    CollectionSlipValidated = true,
                    CollectionSlipValidatedAt = DateTimeOffset.UtcNow,
                    CollectionSlipValidatedBy = userId,
                    TotalDevicesExpected = batch.TotalQuantityExpected,
                    TotalDevicesScanned = batch.TotalQuantityScanned,
                    CreatedByUserId = userId,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                
                _coreDb.DispatchPods.Add(pod);
            }
            else
            {
                // Update existing POD
                pod = existingPod!; // existingPod is not null here because isNew is false
                pod.SchoolName = batch.SchoolName ?? pod.SchoolName;
                pod.SourceReference = batch.CollectionSlipNumber;
                pod.TotalDevicesExpected = batch.TotalQuantityExpected;
                pod.TotalDevicesScanned = batch.TotalQuantityScanned;
                pod.CollectionSlipValidated = true;
                pod.CollectionSlipValidatedAt = DateTimeOffset.UtcNow;
                pod.CollectionSlipValidatedBy = userId;
            }
            
            // Get school info if available
            if (batch.SchoolId.HasValue)
            {
                var school = await _coreDb.Schools
                    .FirstOrDefaultAsync(s => s.SchoolId == batch.SchoolId.Value, ct);
                
                if (school != null)
                {
                    pod.District = school.District;
                    pod.EmisCode = school.EmisCode;
                }
            }
            
            await _coreDb.SaveChangesAsync(ct);
            
            _logger.LogInformation(
                "{Action} POD {PodNumber} from collection slip {CollectionSlipNumber} (RnrBatch {RnrBatchId})",
                isNew ? "Created" : "Updated", 
                pod.PodNumber, 
                batch.CollectionSlipNumber, 
                rnrBatchId);
            
            return (pod, isNew);
        }

        /// <summary>
        /// Get collection slip summary information
        /// </summary>
        public async Task<CollectionSlipSummary?> GetCollectionSlipSummary(
            Guid rnrBatchId, 
            CancellationToken ct = default)
        {
            var batch = await _coreDb.RnrBatches
                .FirstOrDefaultAsync(b => b.BatchId == rnrBatchId, ct);
            
            if (batch == null)
                return null;
            
            // Get linked POD if exists
            var pod = await _coreDb.DispatchPods
                .FirstOrDefaultAsync(p => p.RnrBatchId == rnrBatchId, ct);
            
            return new CollectionSlipSummary(
                RnrBatchId: batch.BatchId,
                CollectionSlipNumber: batch.CollectionSlipNumber,
                SchoolName: batch.SchoolName ?? "Unknown School",
                District: null, // Retrieved from school if available (see school lookup above)
                EmisCode: null, // Retrieved from school if available (see school lookup above)
                StockType: "R&R",
                GRVNumber: batch.GRVNumber,
                TotalDevicesExpected: batch.TotalQuantityExpected,
                TotalDevicesScanned: batch.TotalQuantityScanned,
                Status: batch.Status.ToString(),
                CreatedAt: batch.CreatedAt,
                ConfirmedAt: batch.ConfirmedAt,
                ConfirmedBy: batch.ConfirmedBy,
                PodId: pod?.Id,
                PodNumber: pod?.PodNumber,
                PodStatus: pod?.Status.ToString()
            );
        }

        /// <summary>
        /// Validate POD edits against the source collection slip
        /// </summary>
        public async Task<List<string>> ValidatePodEditAgainstCollectionSlip(
            Guid podId, 
            int? newDeviceCount, 
            string? newSchoolName, 
            CancellationToken ct = default)
        {
            var errors = new List<string>();
            
            var pod = await _coreDb.DispatchPods
                .FirstOrDefaultAsync(p => p.Id == podId, ct);
            
            if (pod == null)
            {
                errors.Add("POD not found");
                return errors;
            }
            
            if (!pod.RnrBatchId.HasValue)
            {
                // POD not linked to collection slip, no validation needed
                return errors;
            }
            
            var batch = await _coreDb.RnrBatches
                .FirstOrDefaultAsync(b => b.BatchId == pod.RnrBatchId.Value, ct);
            
            if (batch == null)
            {
                errors.Add("Collection slip not found");
                return errors;
            }
            
            // Validate device count
            if (newDeviceCount.HasValue && pod.IsLockedToCollectionSlip)
            {
                if (newDeviceCount.Value != batch.TotalQuantityScanned)
                {
                    errors.Add($"Device count mismatch: POD has {newDeviceCount.Value} but collection slip has {batch.TotalQuantityScanned}");
                }
            }
            
            // Validate school name
            if (!string.IsNullOrWhiteSpace(newSchoolName) && pod.IsLockedToCollectionSlip)
            {
                if (!string.IsNullOrWhiteSpace(batch.SchoolName) && 
                    !string.Equals(newSchoolName, batch.SchoolName, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"School name mismatch: POD has '{newSchoolName}' but collection slip has '{batch.SchoolName}'");
                }
            }
            
            return errors;
        }
    }

    /// <summary>
    /// Summary information about a collection slip
    /// </summary>
    public record CollectionSlipSummary(
        Guid RnrBatchId,
        string CollectionSlipNumber,
        string SchoolName,
        string? District,
        string? EmisCode,
        string StockType,
        string? GRVNumber,
        int TotalDevicesExpected,
        int TotalDevicesScanned,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ConfirmedAt,
        string? ConfirmedBy,
        Guid? PodId,
        string? PodNumber,
        string? PodStatus
    );
}
