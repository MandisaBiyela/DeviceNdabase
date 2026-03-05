using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeviceDesk.Modules.Phase2.Controllers
{
    [Authorize] // later: Roles = Phase2Roles.ICTTechnician
    [ApiController]
    [Route("api/phase2/technician")]
    public class TechnicianDevicesController : ControllerBase
    {
        // DTO used by my-work-technician.js
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

        public class DisposeRequest
        {
            public string? Reason { get; set; }
        }

        [HttpGet("my-work")]
        public ActionResult<IEnumerable<MyWorkItemDto>> GetMyWork()
        {
            var demo = new[]
            {
                new MyWorkItemDto
                {
                    DeviceId = Guid.NewGuid(),
                    Brand = "Lenovo",
                    Model = "ThinkPad T480",
                    SerialNumber = "SN-A1B2C3",
                    CurrentStatus = "Awaiting Repair",
                    Priority = "High",
                    AssignedOn = DateTimeOffset.UtcNow.AddHours(-8)
                },
                new MyWorkItemDto
                {
                    DeviceId = Guid.NewGuid(),
                    Brand = "Acer",
                    Model = "Aspire 5",
                    SerialNumber = "SN-D4E5F6",
                    CurrentStatus = "In Repair",
                    Priority = "Normal",
                    AssignedOn = DateTimeOffset.UtcNow.AddDays(-2)
                }
            };

            return Ok(demo);
        }

        [HttpGet("devices/{deviceId:guid}/details")]
        public ActionResult<object> GetDeviceDetails(Guid deviceId)
        {
            var dto = new
            {
                deviceId,
                brand = "Lenovo",
                model = "ThinkPad T480",
                serialNumber = "SN-A1B2C3",
                currentStatus = "Awaiting Repair",
                assignedOn = DateTimeOffset.UtcNow.AddHours(-8),
                notes = new[]
                {
                    new {
                        id = Guid.NewGuid(),
                        noteType = "Technician",
                        summary = "Diagnosed keyboard issue",
                        details = "Keyboard replacement required.",
                        createdBy = "Technician B",
                        createdAt = DateTimeOffset.UtcNow.AddHours(-1)
                    }
                }
            };
            return Ok(dto);
        }

        [HttpPost("devices/{deviceId:guid}/dispose")]
        public ActionResult<object> DisposeDevice(Guid deviceId, [FromBody] DisposeRequest? request)
        {
            // Stub: mark as disposed and echo back request reason
            var result = new { deviceId, disposed = true, reason = request?.Reason ?? "N/A" };
            return Ok(result);
        }
    }
}