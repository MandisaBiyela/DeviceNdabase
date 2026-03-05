using DeviceDesk.Modules.Phase3.Services;
using DeviceDesk.Modules.Phase3.Data;
using DeviceDesk.Modules.Phase3.Models;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using DeviceDesk.Infrastructure.Identity;
 

namespace DeviceDesk.Modules.Phase3.Controllers
{
    [ApiController]
    [Route("api/dispatch")]
    [Authorize]
    public class DispatchController : ControllerBase
    {
        private readonly Phase2DbContext _phase2Db;
        private readonly Phase3DbContext _phase3Db;
        private readonly DispatchDocumentService _docs;
        private readonly DeviceDeskDbContext _coreDb;
        private readonly Phase1DbContext _phase1Db;
        private readonly CollectionSlipPodService _collectionSlipPodService;

        public DispatchController(
            Phase2DbContext phase2Db,
            Phase3DbContext phase3Db,
            DispatchDocumentService docs, 
            DeviceDeskDbContext coreDb, 
            Phase1DbContext phase1Db,
            CollectionSlipPodService collectionSlipPodService)
        {
            _phase2Db = phase2Db;
            _phase3Db = phase3Db;
            _docs = docs;
            _coreDb = coreDb;
            _phase1Db = phase1Db;
            _collectionSlipPodService = collectionSlipPodService;
        }

        public record CreatePodRequest(
            string StockType,
            string SourceReference,
            string SchoolName,
            string? District,
            string? Emis,
            List<int>? DeviceIds,
            List<string>? DeviceSerials);

        public record CreatePodResponse(
            string PodNumber,
            long PodDocumentId,
            long DeliveryNoteDocumentId,
            string PodFileName,
            string DeliveryNoteFileName,
            int LinkedDevices);

        public record CreateTripRequest(
            string TripRef,
            string DriverName,
            string VehicleReg,
            List<string>? PodNumbers);

        public record TripDto(
            Guid TripId,
            string TripRef,
            string DriverName,
            string VehicleReg,
            string Status,
            DateTimeOffset CreatedAt);

        [HttpGet("devices")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public IActionResult ListReadyDevices([FromQuery] Phase2Zone? zone, [FromQuery] string? serial)
        {
            var q = _phase2Db.Devices.Where(d => d.Stage == Phase2Stage.Dispatch && d.QaPassed == true && d.ScannedOutAt != null);
            if (zone.HasValue) q = q.Where(d => d.Zone == zone);
            if (!string.IsNullOrWhiteSpace(serial)) q = q.Where(d => d.Serial.Contains(serial));
            var list = q.OrderByDescending(d => d.UpdatedAt).Take(500).Select(d => new
            {
                id = d.Id,
                serial = d.Serial,
                zone = d.Zone.ToString(),
                updatedAt = d.UpdatedAt
            }).ToList();
            return Ok(list);
        }

        [HttpPost("trips")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public async Task<ActionResult<TripDto>> CreateTrip([FromBody] CreateTripRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.TripRef) || string.IsNullOrWhiteSpace(req.DriverName) || string.IsNullOrWhiteSpace(req.VehicleReg))
                return BadRequest("TripRef, DriverName, VehicleReg required.");

            var t = new Infrastructure.Data.DispatchTrip
            {
                TripRef = req.TripRef.Trim(),
                DriverName = req.DriverName.Trim(),
                VehicleReg = req.VehicleReg.Trim(),
                Status = "Scheduled",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _coreDb.DispatchTrips.Add(t);
            await _coreDb.SaveChangesAsync(ct);

            if (req.PodNumbers != null && req.PodNumbers.Count > 0)
            {
                var pods = _coreDb.DispatchPods.Where(p => req.PodNumbers.Contains(p.PodNumber)).ToList();
                foreach (var p in pods) p.TripId = t.TripId;
                await _coreDb.SaveChangesAsync(ct);
            }

            return Ok(new TripDto(t.TripId, t.TripRef, t.DriverName, t.VehicleReg, t.Status, t.CreatedAt));
        }

        [HttpGet("trips")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public IActionResult ListTrips()
        {
            var items = _coreDb.DispatchTrips
                .OrderByDescending(x => x.CreatedAt)
                .Take(200)
                .Select(x => new TripDto(x.TripId, x.TripRef, x.DriverName, x.VehicleReg, x.Status, x.CreatedAt))
                .ToList();
            return Ok(items);
        }

        [HttpGet("pods/awaiting-trip")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public IActionResult ListPodsAwaitingTrip()
        {
            var pods = _coreDb.DispatchPods
                .Where(p => p.Status == DispatchPodStatus.Ready)
                .OrderByDescending(p => p.CreatedAt)
                .Take(200)
                .Select(p => new { p.PodNumber, p.SchoolName, p.District, p.SourceReference, p.StockType, p.CreatedAt })
                .ToList();
            return Ok(pods);
        }

        [HttpGet("trips/{tripId:guid}")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public IActionResult GetTrip(Guid tripId)
        {
            var t = _coreDb.DispatchTrips.FirstOrDefault(x => x.TripId == tripId);
            if (t == null) return NotFound();
            var pods = _coreDb.DispatchPods.Where(p => p.TripId == tripId).Select(p => new { p.PodNumber, p.SchoolName, p.Status }).ToList();
            return Ok(new { trip = new TripDto(t.TripId, t.TripRef, t.DriverName, t.VehicleReg, t.Status, t.CreatedAt), pods });
        }

        [HttpPost("trips/{tripId:guid}/attach-pod/{podNumber}")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> AttachPod(Guid tripId, string podNumber, CancellationToken ct)
        {
            var t = _coreDb.DispatchTrips.FirstOrDefault(x => x.TripId == tripId);
            if (t == null) return NotFound();
            var p = _coreDb.DispatchPods.FirstOrDefault(x => x.PodNumber == podNumber);
            if (p == null) return NotFound();
            p.TripId = tripId;
            // Keep status unchanged or set to Ready
            p.Status = DispatchPodStatus.Ready;
            await _coreDb.SaveChangesAsync(ct);
            return Ok();
        }

        [HttpGet("trips/{tripId:guid}/sheet-pdf")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> GetTripSheetPdf(Guid tripId, CancellationToken ct)
        {
            var t = _coreDb.DispatchTrips.FirstOrDefault(x => x.TripId == tripId);
            if (t == null) return NotFound();
            var pods = _coreDb.DispatchPods.Where(p => p.TripId == tripId).Select(p => new { p.PodNumber, p.SchoolName }).ToList();
            var data = pods.Select(p => (p.PodNumber, p.SchoolName));
            var res = await _docs.CreateTripSheetAsync(t.TripRef, t.DriverName, t.VehicleReg, data, ct);
            return Ok(new { documentId = res.tripDocId, fileName = res.fileName });
        }

        [HttpGet("devices/{id:int}")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public IActionResult GetDeviceById([FromRoute] int id)
        {
            var d = _phase2Db.Devices.FirstOrDefault(x => x.Id == id);
            if (d == null) return NotFound();
            return Ok(new { id = d.Id, serial = d.Serial, zone = d.Zone.ToString(), stage = d.Stage.ToString(), updatedAt = d.UpdatedAt });
        }

        // Creates a POD and Delivery Note for a set of Phase 2 devices currently in Dispatch stage.
        // Marks devices as allocated to a school (Stage = SchoolAllocation) and records the scan-out event
        // via ScannedOutAt/ScannedOutByUserId, representing that devices have left ICT Center/Dispatch control.
        [HttpPost("pods")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public async Task<ActionResult<CreatePodResponse>> CreatePod([FromBody] CreatePodRequest req, CancellationToken ct)
        {
            // Support both DeviceIds (legacy) and DeviceSerials (collection slip workflow)
            if ((req.DeviceIds == null || req.DeviceIds.Count == 0) && 
                (req.DeviceSerials == null || req.DeviceSerials.Count == 0))
                return BadRequest("No devices selected. Provide either DeviceIds or DeviceSerials.");

            // Generate POD number similar to GRV numbering: POD-YYYY-#####
            var podNumber = await GeneratePodNumberAsync(ct);

            // Update devices' stage to SchoolAllocation (closest existing state)
            List<Phase2Device> devices;
            
            if (req.DeviceSerials != null && req.DeviceSerials.Count > 0)
            {
                // Collection slip workflow: look up by serial numbers
                devices = _phase2Db.Devices
                    .Where(d => req.DeviceSerials.Contains(d.Serial))
                    .ToList();
            }
            else
            {
                // Legacy workflow: look up by IDs
                devices = _phase2Db.Devices
                    .Where(d => req.DeviceIds!.Contains(d.Id))
                    .ToList();
            }

            if (!devices.Any())
                return BadRequest("No matching devices found for POD creation.");

            var now = DateTime.UtcNow;
            var currentUserId = User?.Identity?.Name;

            foreach (var d in devices)
            {
                d.Stage = Phase2Stage.SchoolAllocation;
                d.UpdatedAt = now;
                if (d.ScannedOutAt == null)
                {
                    d.ScannedOutAt = now;
                    d.ScannedOutByUserId = currentUserId;
                }
            }

            await _phase2Db.SaveChangesAsync(ct);

            // Generate and save two PDFs (POD + Delivery Note)
            var serials = devices.Select(d => d.Serial).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var (podDocId, dnDocId, podFileName, dnFileName) = await _docs.CreatePodAndDeliveryNoteAsync(
                podNumber,
                req.SchoolName,
                req.StockType,
                req.SourceReference,
                serials,
                ct);

            // Persist POD metadata to BOTH databases
            // 1. Core DB (legacy/existing system)
            var podRecord = new DispatchPod
            {
                PodNumber = podNumber,
                DeliveryNoteNumber = dnFileName,
                SchoolName = req.SchoolName,
                District = req.District,
                StockType = req.StockType,
                SourceReference = req.SourceReference,
                Status = DispatchPodStatus.Ready,
                PodDocumentId = podDocId,
                DeliveryNoteDocumentId = dnDocId,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = currentUserId
            };
            _coreDb.DispatchPods.Add(podRecord);
            await _coreDb.SaveChangesAsync(ct);

            // 2. Phase3 DB (dispatch workflow)
            var phase3Pod = new DispatchPOD
            {
                PODNumber = podNumber,
                DeliveryNoteNumber = dnFileName,
                SchoolName = req.SchoolName,
                District = req.District,
                EmisCode = req.Emis,
                StockType = req.StockType,
                SourceReference = req.SourceReference,
                Status = PODStatus.ReadyForDispatch, // Status 0 - ready to be scanned out
                PODDocumentId = podDocId,
                DeliveryNoteDocumentId = dnDocId,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = currentUserId
            };
            _phase3Db.DispatchPODs.Add(phase3Pod);
            await _phase3Db.SaveChangesAsync(ct);

            var res = new CreatePodResponse(
                podNumber,
                podDocId,
                dnDocId,
                podFileName,
                dnFileName,
                devices.Count);

            return Ok(res);
        }

        private async Task<string> GeneratePodNumberAsync(CancellationToken ct)
        {
            var year = DateTime.Now.Year;
            var prefix = $"POD-{year}-";

            // Count existing POD documents to derive next number; this is a lightweight approach
            // We rely on DispatchDocumentService saving with FileName starting with "POD-<number>"
            // If none exist, start at 1
            // Note: This could be replaced by a dedicated table later.
            int nextNumber = 1;

            // We cannot query DeviceDeskDbContext from here directly without adding it,
            // so we approximate by using Phase2 devices count for the year.
            // To keep deterministic, compute based on total devices already at SchoolAllocation this year.
            var existingCount = await Task.FromResult(
                _phase2Db.Devices
                    .Count(d => d.Stage == Phase2Stage.SchoolAllocation && d.UpdatedAt.Year == year));

            nextNumber = existingCount + 1;

            return $"{prefix}{nextNumber:D3}";
        }

        // ─────────────────────────────────────────────────────────────
        // Phase 3 · Step 1 — Ready list grouped by source
        // ─────────────────────────────────────────────────────────────

        [HttpGet("/api/phase3/dispatch/ready-list")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> GetDispatchReadyList(CancellationToken ct)
        {
            // 1) Get all Phase 2 devices at Dispatch stage, with their GRV link (via Receipt)
            var phase2Items = await _phase2Db.Devices
                .Where(d => d.Stage == Phase2Stage.Dispatch && d.ReceiptId != null)
                .Include(d => d.Receipt)
                .Select(d => new { Grv = d.Receipt!.GrvNumber, d.Zone, d.UpdatedAt })
                .ToListAsync(ct);

            if (phase2Items.Count == 0)
                return Ok(Array.Empty<object>());

            var grvNumbers = phase2Items.Select(x => x.Grv).Distinct().ToList();

            // 2) Load Phase 1 GRVs and their ReceivingBatch, including Order/CollectionSlip
            var grvs = await _phase1Db.GoodsReceivedNotes
                .Where(g => grvNumbers.Contains(g.GRVNumber))
                .Include(g => g.ReceivingBatch)
                    .ThenInclude(rb => rb.Order)
                .Include(g => g.ReceivingBatch)
                    .ThenInclude(rb => rb.CollectionSlip)
                .ToListAsync(ct);

            // Map GRV → source meta
            var grvMeta = new Dictionary<string, (string sourceType, string sourceNumber, string school)>();
            foreach (var g in grvs)
            {
                var batch = g.ReceivingBatch;
                string sourceTypeName;
                string sourceNumber;
                string schoolName;

                if (batch.SourceType == ReceivingSourceType.NewStock)
                {
                    sourceTypeName = "Order";
                    // Prefer explicit order number; fall back to GRVNumber if absent
                    sourceNumber = batch.Order?.OrderNumber ?? g.OrderNumber ?? g.GRVNumber;
                    // Warehouse or supplier name for origin
                    schoolName = g.SupplierName ?? batch.Order?.SupplierName ?? "Central Warehouse";
                }
                else
                {
                    sourceTypeName = "CollectionSlip";
                    sourceNumber = batch.CollectionSlip?.SlipNumber ?? g.GRVNumber;
                    schoolName = batch.CollectionSlip?.SchoolName ?? g.SupplierName ?? "Unknown School";
                }

                grvMeta[g.GRVNumber] = (sourceTypeName, sourceNumber, schoolName);
            }

            // 3) Group Phase 2 items by source (type+number+school)
            var grouped = phase2Items
                .Select(x =>
                {
                    var meta = grvMeta.TryGetValue(x.Grv, out var m)
                        ? m
                        : (x.Zone == Phase2Zone.NewStock ? "Order" : "CollectionSlip",
                           x.Grv,
                           x.Zone == Phase2Zone.NewStock ? "Central Warehouse" : "Unknown School");
                    return new
                    {
                        type = meta.Item1,
                        number = meta.Item2,
                        school = meta.Item3,
                        updatedAt = x.UpdatedAt
                    };
                })
                .GroupBy(x => new { x.type, x.number, x.school })
                .Select(g => new
                {
                    sourceType = g.Key.type,
                    sourceNumber = g.Key.number,
                    school = g.Key.school,
                    quantity = g.Count(),
                    lastUpdated = g.Max(i => i.updatedAt).ToString("yyyy-MM-dd")
                })
                .OrderByDescending(r => r.lastUpdated)
                .ThenBy(r => r.sourceNumber)
                .ToList();

            return Ok(grouped);
        }

        // ─────────────────────────────────────────────────────────────
        // Signed POD upload and retrieval
        // ─────────────────────────────────────────────────────────────

        [HttpGet("pods/{podNumber}")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public IActionResult GetPodDetails([FromRoute] string podNumber)
        {
            if (string.IsNullOrWhiteSpace(podNumber)) return BadRequest("Invalid POD number.");
            var p = _coreDb.DispatchPods.FirstOrDefault(x => x.PodNumber == podNumber);
            if (p == null) return NotFound();
            return Ok(new
            {
                id = p.Id,
                podNumber = p.PodNumber,
                deliveryNoteNumber = p.DeliveryNoteNumber,
                schoolName = p.SchoolName,
                district = p.District,
                stockType = p.StockType,
                sourceReference = p.SourceReference,
                status = p.Status.ToString(),
                tripId = p.TripId,
                createdAt = p.CreatedAt,
                createdBy = p.CreatedByUserId,
                podDocumentId = p.PodDocumentId,
                deliveryNoteDocumentId = p.DeliveryNoteDocumentId,
                signedPodDocumentId = p.SignedPodDocumentId
            });
        }

        [HttpPost("pods/{podNumber}/signed-pod")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> UploadSignedPod([FromRoute] string podNumber, IFormFile file, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(podNumber)) return BadRequest("Invalid POD number.");
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            var allowed = new[] { "application/pdf", "image/png", "image/jpeg" };
            var contentType = file.ContentType;
            if (!allowed.Contains(contentType))
                return BadRequest("Only PDF, PNG or JPEG files are allowed.");

            var ext = contentType switch
            {
                "application/pdf" => ".pdf",
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                _ => ""
            };

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var doc = new Document
            {
                DocType = "SignedPOD",
                FileName = $"SignedPOD-{podNumber}{ext}",
                ContentType = contentType,
                FileData = bytes
            };

            _coreDb.Documents.Add(doc);
            await _coreDb.SaveChangesAsync(ct);

            // Link to POD record and mark as Signed
            var pod = _coreDb.DispatchPods.FirstOrDefault(p => p.PodNumber == podNumber);
            if (pod != null)
            {
                pod.SignedPodDocumentId = doc.DocumentId;
                pod.SignedPodUploadedAt = DateTimeOffset.UtcNow;
                pod.SignedPodUploadedByUserId = User?.Identity?.Name;
                pod.Status = DispatchPodStatus.Signed;
                await _coreDb.SaveChangesAsync(ct);
            }

            return Ok(new { documentId = doc.DocumentId, fileName = doc.FileName, contentType = doc.ContentType, uploadedAt = doc.UploadedAt });
        }

        [HttpGet("pods/{podNumber}/signed-pod")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public IActionResult GetSignedPod([FromRoute] string podNumber)
        {
            if (string.IsNullOrWhiteSpace(podNumber)) return BadRequest("Invalid POD number.");

            var pod = _coreDb.DispatchPods.FirstOrDefault(p => p.PodNumber == podNumber);
            if (pod == null || pod.SignedPodDocumentId == null) return NotFound();

            var doc = _coreDb.Documents.FirstOrDefault(d => d.DocumentId == pod.SignedPodDocumentId);

            if (doc == null) return NotFound();

            return File(doc.FileData, doc.ContentType, doc.FileName);
        }

        [HttpGet("pods/{podNumber}/pod-pdf")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public IActionResult GetPodPdf([FromRoute] string podNumber)
        {
            var pod = _coreDb.DispatchPods.FirstOrDefault(p => p.PodNumber == podNumber);
            if (pod == null || pod.PodDocumentId == null) return NotFound();
            var doc = _coreDb.Documents.FirstOrDefault(d => d.DocumentId == pod.PodDocumentId);
            if (doc == null) return NotFound();
            return File(doc.FileData, doc.ContentType, doc.FileName);
        }

        [HttpGet("pods/{podNumber}/delivery-note-pdf")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public IActionResult GetDeliveryNotePdf([FromRoute] string podNumber)
        {
            var pod = _coreDb.DispatchPods.FirstOrDefault(p => p.PodNumber == podNumber);
            if (pod == null || pod.DeliveryNoteDocumentId == null) return NotFound();
            var doc = _coreDb.Documents.FirstOrDefault(d => d.DocumentId == pod.DeliveryNoteDocumentId);
            if (doc == null) return NotFound();
            return File(doc.FileData, doc.ContentType, doc.FileName);
        }

        // ─────────────────────────────────────────────────────────────
        // POD Generation & Regeneration
        // ─────────────────────────────────────────────────────────────

        [HttpPost("pods/{podNumber}/regenerate-documents")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> RegeneratePodDocuments([FromRoute] string podNumber, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(podNumber)) return BadRequest("Invalid POD number.");

            var pod = _coreDb.DispatchPods.FirstOrDefault(p => p.PodNumber == podNumber);
            if (pod == null) return NotFound("POD not found.");

            // Get devices linked to this POD via Phase2 devices that were marked SchoolAllocation
            // and match the school/source details
            var devices = _phase2Db.Devices
                .Where(d => d.Stage == Phase2Stage.SchoolAllocation)
                .ToList()
                .Where(d => 
                    // Match devices that would have been included in this POD
                    d.ScannedOutAt.HasValue && 
                    d.ScannedOutAt.Value.Date == pod.CreatedAt.Date
                )
                .ToList();

            if (!devices.Any())
                return BadRequest("No devices found for this POD.");

            var serials = devices.Select(d => d.Serial).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            
            // Generate new POD and Delivery Note PDFs
            var (podDocId, dnDocId, podFileName, dnFileName) = await _docs.CreatePodAndDeliveryNoteAsync(
                podNumber,
                pod.SchoolName,
                pod.StockType,
                pod.SourceReference ?? "",
                serials,
                ct);

            // Update POD record with new document IDs
            pod.PodDocumentId = podDocId;
            pod.DeliveryNoteDocumentId = dnDocId;
            await _coreDb.SaveChangesAsync(ct);

            return Ok(new
            {
                message = "POD documents regenerated successfully",
                podDocumentId = podDocId,
                podFileName,
                deliveryNoteDocumentId = dnDocId,
                deliveryNoteFileName = dnFileName,
                deviceCount = serials.Count
            });
        }

        [HttpPost("pods/generate-batch")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> GenerateBatchPods([FromBody] List<CreatePodRequest> podRequests, CancellationToken ct)
        {
            if (podRequests == null || !podRequests.Any())
                return BadRequest("No POD requests provided.");

            var results = new List<CreatePodResponse>();
            var errors = new List<string>();

            foreach (var req in podRequests)
            {
                try
                {
                    if (req.DeviceIds == null || !req.DeviceIds.Any())
                    {
                        errors.Add($"Skipped: No devices for {req.SchoolName}");
                        continue;
                    }

                    var podNumber = await GeneratePodNumberAsync(ct);
                    var devices = _phase2Db.Devices
                        .Where(d => req.DeviceIds.Contains(d.Id))
                        .ToList();

                    if (!devices.Any())
                    {
                        errors.Add($"Skipped: No matching devices for {req.SchoolName}");
                        continue;
                    }

                    var now = DateTime.UtcNow;
                    var currentUserId = User?.Identity?.Name;

                    foreach (var d in devices)
                    {
                        d.Stage = Phase2Stage.SchoolAllocation;
                        d.UpdatedAt = now;
                        if (d.ScannedOutAt == null)
                        {
                            d.ScannedOutAt = now;
                            d.ScannedOutByUserId = currentUserId;
                        }
                    }

                    await _phase2Db.SaveChangesAsync(ct);

                    var serials = devices.Select(d => d.Serial).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                    var (podDocId, dnDocId, podFileName, dnFileName) = await _docs.CreatePodAndDeliveryNoteAsync(
                        podNumber,
                        req.SchoolName,
                        req.StockType,
                        req.SourceReference,
                        serials,
                        ct);

                    var podRecord = new DispatchPod
                    {
                        PodNumber = podNumber,
                        DeliveryNoteNumber = dnFileName,
                        SchoolName = req.SchoolName,
                        District = req.District,
                        StockType = req.StockType,
                        SourceReference = req.SourceReference,
                        Status = DispatchPodStatus.Ready,
                        PodDocumentId = podDocId,
                        DeliveryNoteDocumentId = dnDocId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        CreatedByUserId = currentUserId
                    };
                    _coreDb.DispatchPods.Add(podRecord);
                    await _coreDb.SaveChangesAsync(ct);

                    results.Add(new CreatePodResponse(
                        podNumber,
                        podDocId,
                        dnDocId,
                        podFileName,
                        dnFileName,
                        devices.Count));
                }
                catch (Exception ex)
                {
                    errors.Add($"Error for {req.SchoolName}: {ex.Message}");
                }
            }

            return Ok(new
            {
                success = results.Count,
                total = podRequests.Count,
                results,
                errors
            });
        }

        [HttpPost("pods/{podNumber}/upload-signed-pod-v2")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Driver},{UserRoles.IctClerk},{UserRoles.Admin}")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
        public async Task<IActionResult> UploadSignedPodV2(
            [FromRoute] string podNumber, 
            [FromForm] IFormFile file,
            [FromForm] string? notes,
            [FromForm] bool? schoolSigned,
            [FromForm] string? signatoryName,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(podNumber)) return BadRequest("Invalid POD number.");
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            // Validate file size (additional check)
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest("File size exceeds 10MB limit.");

            // Validate file type
            var allowed = new[] { "application/pdf", "image/png", "image/jpeg", "image/jpg" };
            var contentType = file.ContentType.ToLower();
            if (!allowed.Contains(contentType))
                return BadRequest("Only PDF, PNG, or JPEG files are allowed.");

            var ext = contentType switch
            {
                "application/pdf" => ".pdf",
                "image/png" => ".png",
                "image/jpeg" or "image/jpg" => ".jpg",
                _ => ".bin"
            };

            // Validate filename doesn't contain path traversal
            var fileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains(".."))
                return BadRequest("Invalid filename.");

            // Read file data
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            // Validate file content (basic validation)
            if (bytes.Length == 0)
                return BadRequest("Empty file uploaded.");

            // Create document record
            var doc = new Document
            {
                DocType = "SignedPOD",
                FileName = $"SignedPOD-{podNumber}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}",
                ContentType = contentType,
                FileData = bytes
            };

            _coreDb.Documents.Add(doc);
            await _coreDb.SaveChangesAsync(ct);

            // Link to POD record and update metadata
            var pod = _coreDb.DispatchPods.FirstOrDefault(p => p.PodNumber == podNumber);
            if (pod == null)
                return NotFound("POD not found.");

            pod.SignedPodDocumentId = doc.DocumentId;
            pod.SignedPodUploadedAt = DateTimeOffset.UtcNow;
            pod.SignedPodUploadedByUserId = User?.Identity?.Name;
            pod.Status = DispatchPodStatus.Signed;
            await _coreDb.SaveChangesAsync(ct);

            return Ok(new
            {
                message = "Signed POD uploaded successfully",
                documentId = doc.DocumentId,
                fileName = doc.FileName,
                contentType = doc.ContentType,
                uploadedAt = doc.UploadedAt,
                fileSize = bytes.Length,
                podStatus = pod.Status.ToString()
            });
        }

        [HttpGet("pods/list")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public IActionResult ListAllPods([FromQuery] DispatchPodStatus? status, [FromQuery] string? schoolName, [FromQuery] int pageSize = 50)
        {
            var query = _coreDb.DispatchPods.AsQueryable();

            if (status.HasValue)
                query = query.Where(p => p.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(schoolName))
                query = query.Where(p => p.SchoolName.Contains(schoolName));

            var pods = query
                .OrderByDescending(p => p.CreatedAt)
                .Take(pageSize)
                .Select(p => new
                {
                    id = p.Id,
                    podNumber = p.PodNumber,
                    deliveryNoteNumber = p.DeliveryNoteNumber,
                    schoolName = p.SchoolName,
                    district = p.District,
                    stockType = p.StockType,
                    sourceReference = p.SourceReference,
                    status = p.Status.ToString(),
                    tripId = p.TripId,
                    createdAt = p.CreatedAt,
                    hasPodDocument = p.PodDocumentId.HasValue,
                    hasDeliveryNote = p.DeliveryNoteDocumentId.HasValue,
                    hasSignedPod = p.SignedPodDocumentId.HasValue,
                    signedPodUploadedAt = p.SignedPodUploadedAt
                })
                .ToList();

            return Ok(pods);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // COLLECTION SLIP → POD LINKAGE ENDPOINTS
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Validate if a Collection Slip is ready for dispatch
        /// </summary>
        [HttpGet("collection-slips/{rnrBatchId:guid}/validate-dispatch")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> ValidateCollectionSlipForDispatch(Guid rnrBatchId, CancellationToken ct)
        {
            var (isValid, error, batch) = await _collectionSlipPodService.ValidateCollectionSlipForDispatch(rnrBatchId, ct);
            
            if (!isValid)
                return BadRequest(new { isValid = false, error, collectionSlipNumber = batch?.CollectionSlipNumber });

            return Ok(new 
            { 
                isValid = true, 
                collectionSlipNumber = batch!.CollectionSlipNumber,
                schoolName = batch.SchoolName,
                totalDevices = batch.TotalQuantityScanned,
                status = batch.Status.ToString()
            });
        }

        /// <summary>
        /// Create or regenerate POD from Collection Slip data
        /// Enforces direct relationship - POD is always generated FROM Collection Slip, never manually created
        /// </summary>
        [HttpPost("collection-slips/{rnrBatchId:guid}/generate-pod")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> GeneratePodFromCollectionSlip(Guid rnrBatchId, CancellationToken ct)
        {
            var userId = User?.Identity?.Name ?? "system";

            try
            {
                // Create/update POD with Collection Slip data
                var (pod, isNew) = await _collectionSlipPodService.CreateOrUpdatePodFromCollectionSlip(rnrBatchId, userId, ct);

                // Get Collection Slip details for document generation
                var collectionSlipSummary = await _collectionSlipPodService.GetCollectionSlipSummary(rnrBatchId, ct);
                if (collectionSlipSummary == null)
                    return BadRequest("Collection Slip not found");

                // Get device serials from Phase2 (devices that came from this R&R batch via GRV)
                var grvNumber = collectionSlipSummary.GRVNumber;
                var deviceSerials = await GetDeviceSerialsFromCollectionSlip(grvNumber ?? "", ct);

                // Generate POD and Delivery Note documents
                var (podDocId, dnDocId, podFileName, dnFileName) = await _docs.CreatePodAndDeliveryNoteAsync(
                    pod.PodNumber,
                    pod.SchoolName,
                    pod.StockType,
                    pod.CollectionSlipNumber ?? pod.SourceReference ?? "",
                    deviceSerials,
                    ct);

                // Update POD with document IDs
                pod.PodDocumentId = podDocId;
                pod.DeliveryNoteDocumentId = dnDocId;
                await _coreDb.SaveChangesAsync(ct);

                return Ok(new
                {
                    success = true,
                    isNew = isNew,
                    podNumber = pod.PodNumber,
                    collectionSlipNumber = pod.CollectionSlipNumber,
                    schoolName = pod.SchoolName,
                    totalDevices = pod.TotalDevicesScanned,
                    podDocumentId = podDocId,
                    deliveryNoteDocumentId = dnDocId,
                    message = isNew 
                        ? $"POD {pod.PodNumber} created from Collection Slip" 
                        : $"POD {pod.PodNumber} regenerated from Collection Slip"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get Collection Slip details with POD linkage
        /// </summary>
        [HttpGet("collection-slips/{rnrBatchId:guid}")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> GetCollectionSlipWithPod(Guid rnrBatchId, CancellationToken ct)
        {
            if (_collectionSlipPodService == null)
                return StatusCode(503, new { error = "CollectionSlipPodService is not available." });
            
            var summary = await _collectionSlipPodService.GetCollectionSlipSummary(rnrBatchId, ct);
            if (summary == null)
                return NotFound("Collection slip not found");
            
            return Ok(summary);
        }

        /// <summary>
        /// List all Collection Slips ready for dispatch (verified and not yet dispatched)
        /// </summary>
        [HttpGet("collection-slips/ready-for-dispatch")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> ListCollectionSlipsReadyForDispatch(CancellationToken ct)
        {
            var readySlips = await _coreDb.RnrBatches
                .Where(b => b.Status >= RnrBatchStatus.Verified && b.Status < RnrBatchStatus.Completed)
                .OrderByDescending(b => b.ConfirmedAt ?? b.CreatedAt)
                .Select(b => new
                {
                    batchId = b.BatchId,
                    collectionSlipNumber = b.CollectionSlipNumber,
                    schoolName = b.SchoolName,
                    totalDevices = b.TotalQuantityScanned,
                    status = b.Status.ToString(),
                    verifiedAt = b.ConfirmedAt,
                    verifiedBy = b.ConfirmedBy,
                    grvNumber = b.GRVNumber,
                    hasPod = _coreDb.DispatchPods.Any(p => p.RnrBatchId == b.BatchId)
                })
                .Take(100)
                .ToListAsync(ct);

            return Ok(readySlips);
        }

        /// <summary>
        /// Get POD with full Collection Slip details for dispatch view
        /// Shows both "what was collected" and "what is being delivered" from same dataset
        /// </summary>
        [HttpGet("pods/{podNumber}/with-collection-slip")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.IctClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> GetPodWithCollectionSlip(string podNumber, CancellationToken ct)
        {
            if (_collectionSlipPodService == null)
                return StatusCode(503, new { error = "CollectionSlipPodService is not available." });
            
            var pod = await _coreDb.DispatchPods
                .FirstOrDefaultAsync(p => p.PodNumber == podNumber, ct);

            if (pod == null)
                return NotFound("POD not found");

            // Get collection slip summary if linked
            CollectionSlipSummary? collectionSlipSummary = null;
            if (pod.RnrBatchId.HasValue)
            {
                collectionSlipSummary = await _collectionSlipPodService.GetCollectionSlipSummary(pod.RnrBatchId.Value, ct);
            }
            
            // Check data integrity
            bool deviceCountsMatch = false;
            bool schoolNamesMatch = false;
            if (collectionSlipSummary != null && pod.IsLockedToCollectionSlip)
            {
                deviceCountsMatch = pod.TotalDevicesScanned == collectionSlipSummary.TotalDevicesScanned;
                schoolNamesMatch = string.Equals(pod.SchoolName, collectionSlipSummary.SchoolName, StringComparison.OrdinalIgnoreCase);
            }

            return Ok(new
            {
                pod = new
                {
                    id = pod.Id,
                    podNumber = pod.PodNumber,
                    deliveryNoteNumber = pod.DeliveryNoteNumber,
                    schoolName = pod.SchoolName,
                    emisCode = pod.EmisCode,
                    district = pod.District,
                    stockType = pod.StockType,
                    sourceReference = pod.SourceReference,
                    collectionSlipNumber = pod.CollectionSlipNumber,
                    totalDevicesExpected = pod.TotalDevicesExpected,
                    totalDevicesScanned = pod.TotalDevicesScanned,
                    isLockedToCollectionSlip = pod.IsLockedToCollectionSlip,
                    collectionSlipValidated = pod.CollectionSlipValidated,
                    status = pod.Status.ToString(),
                    tripId = pod.TripId,
                    podDocumentId = pod.PodDocumentId,
                    deliveryNoteDocumentId = pod.DeliveryNoteDocumentId,
                    signedPodDocumentId = pod.SignedPodDocumentId,
                    createdAt = pod.CreatedAt
                },
                collectionSlip = collectionSlipSummary,
                dataIntegrity = new
                {
                    hasCollectionSlipLink = pod.RnrBatchId.HasValue,
                    isLockedToSource = pod.IsLockedToCollectionSlip,
                    deviceCountsMatch = deviceCountsMatch,
                    schoolNamesMatch = schoolNamesMatch
                }
            });
        }

        /// <summary>
        /// Validate if POD edit would conflict with Collection Slip data
        /// </summary>
        [HttpPost("pods/{podId:guid}/validate-edit")]
        [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
        public async Task<IActionResult> ValidatePodEdit(
            Guid podId,
            [FromBody] ValidatePodEditRequest request,
            CancellationToken ct)
        {
            if (_collectionSlipPodService == null)
                return StatusCode(503, new { error = "CollectionSlipPodService is not available." });
            
            var errors = await _collectionSlipPodService.ValidatePodEditAgainstCollectionSlip(
                podId, 
                request.NewDeviceCount, 
                request.NewSchoolName, 
                ct);

            if (errors.Any())
                return BadRequest(new { isValid = false, errors });

            return Ok(new { isValid = true, message = "Edit is allowed" });
        }

        // Helper method to get device serials from GRV
        private async Task<List<string>> GetDeviceSerialsFromCollectionSlip(string? grvNumber, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(grvNumber))
                return new List<string>();

            // Find devices that came through this GRV
            var devices = await _phase2Db.Devices
                .Where(d => d.Receipt != null && d.Receipt.GrvNumber == grvNumber)
                .Select(d => d.Serial)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(1000)
                .ToListAsync(ct);

            return devices!;
        }

        public record ValidatePodEditRequest(int? NewDeviceCount, string? NewSchoolName);
    }
}