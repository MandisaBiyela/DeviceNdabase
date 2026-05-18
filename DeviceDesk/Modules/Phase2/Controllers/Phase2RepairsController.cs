using DeviceDesk.Modules.Phase2.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceDesk.Modules.Phase2.Controllers;

[Route("phase2/repairs")]
public class Phase2RepairsController : Controller
{
    private readonly RepairReportService _reportService;
    
    public Phase2RepairsController(RepairReportService reportService)
    {
        _reportService = reportService;
    }
    
    [HttpGet("{id}/report")]
    public async Task<IActionResult> Report(int id)
    {
        var vm = await _reportService.GetReportAsync(id);
        return View("~/Modules/Phase2/UI/Views/repair-report.cshtml", vm);
    }
}

