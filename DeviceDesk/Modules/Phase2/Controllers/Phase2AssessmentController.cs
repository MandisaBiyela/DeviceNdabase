using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase2.Services;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase2.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/assessment")]
public class Phase2AssessmentController : ControllerBase
{
    private readonly AssessmentService _service;
    private readonly Phase2DbContext _db;
    public Phase2AssessmentController(AssessmentService service, Phase2DbContext db) { _service = service; _db = db; }

    public record PreAssessRequest(int DeviceId, bool Passed, AttentionRequired AttentionRequired, string? Notes);
    public record DetailedInspectRequest(int DeviceId, bool UnderWarranty, bool? Repairable, InspectionCategory Category, string? Notes, string? DocumentRef, Phase2Stage? Destination);

    [HttpPost("pre")]
    [Authorize(Roles = UserRoles.IctInspector)]
    public async Task<IActionResult> Pre([FromBody] PreAssessRequest req)
    {
        var inspectorId =
            User.FindFirst(ClaimTypes.Email)?.Value ??
            User.FindFirst("email")?.Value ??
            User.Identity?.Name ?? "unknown";

        await _service.PreAssessmentAsync(req.DeviceId, req.Passed, req.AttentionRequired, inspectorId, req.Notes);
        return Ok();
    }

    [HttpPost("detailed")]
    [Authorize(Roles = UserRoles.IctTechnician)]
    public async Task<IActionResult> Detailed([FromBody] DetailedInspectRequest req)
    {
        var technicianId =
            User.FindFirst(ClaimTypes.Email)?.Value ??
            User.FindFirst("email")?.Value ??
            User.Identity?.Name ?? "unknown";

        await _service.DetailedInspectionAsync(req.DeviceId, technicianId, req.UnderWarranty, req.Repairable, req.Category, req.Notes, req.DocumentRef, req.Destination);
        return Ok();
    }

    // GET /api/phase2/assessment/detailed/{deviceId}
    [HttpGet("detailed/{deviceId:int}")]
    [Authorize(Roles = $"{UserRoles.IctTechnician},{UserRoles.IctInspector}")]
    public async Task<IActionResult> GetDetailed([FromRoute] int deviceId)
    {
        var d = await _db.Devices.FindAsync(deviceId);
        if (d == null) return NotFound();

        var dto = new DetailedInspectionDto
        {
            Id = d.Id,
            Serial = d.Serial,
            Zone = d.Zone,
            Stage = d.Stage,
            AttentionRequired = d.AttentionRequired,
            PreAssessmentPassed = d.PreAssessmentPassed,
            PreAssessmentNotes = d.PreAssessmentNotes,
            PreAssessmentInspectorId = d.PreAssessmentInspectorId,
            UnderWarranty = d.UnderWarranty,
            Repairable = d.Repairable,
            TechnicianId = d.TechnicianId,
            InspectionDate = d.InspectionDate,
            RepairCategory = d.RepairCategory,
            DisposalRequested = d.DisposalRequested,
            QaPassed = d.QaPassed,
            QaInspectorId = d.QaInspectorId,
            ReworkCount = d.ReworkCount,
            UpdatedAt = d.UpdatedAt
        };

        return Ok(dto);
    }
}
