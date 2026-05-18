using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services;

public class RepairReportService
{
    private readonly Phase2DbContext _phase2Db;
    private readonly DeviceDeskDbContext _mainDb;
    
    public RepairReportService(Phase2DbContext phase2Db, DeviceDeskDbContext mainDb)
    {
        _phase2Db = phase2Db;
        _mainDb = mainDb;
    }
    
    public async Task<Phase2RepairReportViewModel> GetReportAsync(int repairId)
    {
        var repair = await _phase2Db.RepairRequests
            .Include(r => r.Parts)
            .Include(r => r.Device)
            .FirstOrDefaultAsync(r => r.Id == repairId);
        
        if (repair == null)
            throw new InvalidOperationException("Repair request not found.");
        
        // Look up device details from Phase 1
        var phase1Device = await _mainDb.Devices
            .FirstOrDefaultAsync(d => d.SerialNumber == repair.DeviceSerial);
        
        // Look up school details separately if device has a school ID
        School? school = null;
        if (phase1Device?.SchoolId.HasValue == true)
        {
            school = await _mainDb.Schools.FirstOrDefaultAsync(s => s.SchoolId == phase1Device.SchoolId.Value);
        }
        
        decimal? partsSubtotal = repair.Parts.Any()
            ? repair.Parts.Where(p => p.UnitCost.HasValue).Sum(p => p.UnitCost!.Value * p.Quantity)
            : null;
        
        decimal? labour = null; // Could be calculated from EstimatedLabourHours * hourly rate
        decimal? vat = partsSubtotal.HasValue || labour.HasValue
            ? ((partsSubtotal ?? 0) + (labour ?? 0)) * 0.15m
            : null;
        decimal? grand = (partsSubtotal ?? 0) + (labour ?? 0) + (vat ?? 0);
        
        return new Phase2RepairReportViewModel
        {
            RepairNumber = $"RPR-{repair.Id:000000}",
            ReportDate = repair.CreatedAtUtc,
            TechnicianName = repair.CreatedByUserId,
            
            SchoolName = school?.Name ?? phase1Device?.SchoolName ?? repair.Device?.SchoolName ?? "Unknown",
            Emis = school?.EmisCode ?? "N/A",
            District = school?.District ?? "N/A",
            ProjectName = phase1Device?.Source ?? "N/A", // Using Source (RNR/NEW) as project identifier
            
            DeviceSerial = repair.DeviceSerial,
            ItemDescription = phase1Device?.Description ?? phase1Device?.Model ?? "N/A",
            AssetTag = null, // Not available in current schema
            PodNumber = phase1Device?.OrderNumber, // Using OrderNumber as pod reference
            
            Category = repair.Category.ToString(),
            Priority = repair.Priority ?? "Normal",
            IsUnderWarranty = repair.IsUnderWarranty,
            WarrantyRoute = repair.WarrantyRoute,
            EstimatedLabourHours = repair.EstimatedLabourHours,
            Status = repair.Status.ToString(),
            
            Symptoms = repair.SymptomDescription ?? "",
            Findings = repair.TechnicianFindings ?? "",
            HardwareChecklistSummary = repair.HardwareChecklistSummary,
            
            Parts = repair.Parts.Select(p => new Phase2RepairReportPartViewModel
            {
                PartName = p.PartName,
                PartNumber = p.PartNumber,
                Quantity = p.Quantity,
                UnitCost = p.UnitCost,
                LineTotal = p.UnitCost.HasValue ? p.UnitCost.Value * p.Quantity : null
            }).ToList(),
            
            PartsSubtotal = partsSubtotal,
            LabourTotal = labour,
            VatRate = 15m,
            VatAmount = vat,
            GrandTotal = grand,
            
            RecommendedAction = repair.RecommendedAction
        };
    }
}

