using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeviceDesk.Modules.Phase2.Controllers
{
    [Authorize] // later: Roles = Phase2Roles.ICTInspector
    [ApiController]
    [Route("api/phase2/inspector")]
    public class InspectorDevicesController : ControllerBase
    {
        // DTO used by my-work-inspector.js
        public class MyWorkItemDto
        {
            public Guid DeviceId { get; set; }
            public string Brand { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string SerialNumber { get; set; } = string.Empty;
            public string CurrentStatus { get; set; } = string.Empty;
            public string Priority { get; set; } = string.Empty;
            public DateTimeOffset? AssignedOn { get; set; }
        }

        // TEMP stub so UI works
        [HttpGet("my-work")]
        public ActionResult<IEnumerable<MyWorkItemDto>> GetMyWork()
        {
            var demo = new[]
            {
                new MyWorkItemDto
                {
                    DeviceId = Guid.NewGuid(),
                    Brand = "HP",
                    Model = "ProBook 450 G7",
                    SerialNumber = "SN123456",
                    CurrentStatus = "Waiting Inspection",
                    Priority = "Normal",
                    AssignedOn = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new MyWorkItemDto
                {
                    DeviceId = Guid.NewGuid(),
                    Brand = "Dell",
                    Model = "Latitude 5420",
                    SerialNumber = "SN654321",
                    CurrentStatus = "Waiting Inspection",
                    Priority = "High",
                    AssignedOn = DateTimeOffset.UtcNow.AddHours(-5)
                }
            };

            return Ok(demo);
        }

        // details endpoint used by View Device modal (stub)
        [HttpGet("devices/{deviceId:guid}/details")]
        public ActionResult<object> GetDeviceDetails(Guid deviceId)
        {
            // later: query DB; for now return dummy but valid JSON
            var dto = new
            {
                deviceId,
                assetTag = "AST-001",
                brand = "HP",
                model = "ProBook 450 G7",
                serialNumber = "SN123456",
                currentStatus = "Waiting Inspection",
                assignedOn = DateTimeOffset.UtcNow.AddDays(-1),
                notes = new[]
                {
                    new {
                        id = Guid.NewGuid(),
                        noteType = "PreAssessment",
                        summary = "Visual inspection completed",
                        details = "Minor scratches, otherwise OK.",
                        createdBy = "Technician A",
                        createdAt = DateTimeOffset.UtcNow.AddHours(-2)
                    }
                }
            };

            return Ok(dto);
        }
    }
}