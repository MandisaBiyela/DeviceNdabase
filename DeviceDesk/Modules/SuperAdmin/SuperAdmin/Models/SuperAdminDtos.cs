using System;
using System.Text.Json.Serialization;

namespace DeviceDesk.Modules.SuperAdmin.Models;

public class DashboardStatsDto
{
    [JsonPropertyName("totalDevices")]
    public int TotalDevices { get; set; }
    public int Phase0Batches { get; set; }
    public int Phase0Items { get; set; }
    [JsonPropertyName("phase0Devices")]
    public int Phase0Devices { get; set; }
    public int Phase1Batches { get; set; }
    [JsonPropertyName("phase1Devices")]
    public int Phase1Devices { get; set; }
    [JsonPropertyName("phase2Devices")]
    public int Phase2Devices { get; set; }
    [JsonPropertyName("phase3Pods")]
    public int Phase3Pods { get; set; }
    public int Phase3Trips { get; set; }
    [JsonPropertyName("totalGRVs")]
    public int TotalGRVs { get; set; }
    [JsonPropertyName("totalSchools")]
    public int TotalSchools { get; set; }
    [JsonPropertyName("phase2ByStage")]
    public Dictionary<string, int> Phase2ByStage { get; set; } = new();
    [JsonPropertyName("phase2ByZone")]
    public Dictionary<string, int> Phase2ByZone { get; set; } = new();
    public Dictionary<string, int> PODsByStatus { get; set; } = new();
    public Dictionary<string, int> TripsByStatus { get; set; } = new();
    public int DisposalPending { get; set; }
}

public class Phase0StatsDto
{
    public int NewStockBatches { get; set; }
    public int RnrBatches { get; set; }
    public int TotalBatches { get; set; }
    public int ItemsExpected { get; set; }
}

public class Phase1StatsDto
{
    public int TotalBatches { get; set; }
    public Dictionary<string, int> BatchesByStatus { get; set; } = new();
    public int TotalGRVs { get; set; }
    public int DevicesReceived { get; set; }
    public int VarianceCount { get; set; }
}

public class Phase2StatsDto
{
    public int TotalDevices { get; set; }
    public Dictionary<string, int> DevicesByStage { get; set; } = new();
    public Dictionary<string, int> DevicesByZone { get; set; } = new();
    public int PreAssessmentPassed { get; set; }
    public int PreAssessmentFailed { get; set; }
    public int QAPassed { get; set; }
    public int QAFailed { get; set; }
    public int QAPending { get; set; }
    public int DisposalPending { get; set; }
    public int DisposalApproved { get; set; }
    public int UnderWarranty { get; set; }
    public int Repairable { get; set; }
}

public class Phase3StatsDto
{
    public int TotalPODs { get; set; }
    public Dictionary<string, int> PODsByStatus { get; set; } = new();
    public int TotalTrips { get; set; }
    public Dictionary<string, int> TripsByStatus { get; set; } = new();
    public int Delivered { get; set; }
    public int Exceptions { get; set; }
}

public class SchoolStatsDto
{
    public int TotalSchools { get; set; }
    public int SchoolsWithDevices { get; set; }
    public Dictionary<string, int> DevicesBySchool { get; set; } = new();
}

public class DriverVehicleStatsDto
{
    public int TotalDrivers { get; set; }
    public int TotalVehicles { get; set; }
    public int ActiveTrips { get; set; }
    public Dictionary<string, int> TripsByDriver { get; set; } = new();
    public Dictionary<string, int> TripsByVehicle { get; set; } = new();
}

public class ManagementSummaryDto
{
    public string SystemHealth { get; set; } = string.Empty;
    public string WarehouseStatus { get; set; } = string.Empty;
    public string DeliveryPerformance { get; set; } = string.Empty;
    public string SchoolAllocation { get; set; } = string.Empty;
    public string DriverVehicleActivity { get; set; } = string.Empty;
}

// Phase 2 Dashboard Stats DTOs
public class Phase2DashboardStatsDto
{
    public int TotalDevicesProcessed { get; set; }
    public int TotalDevicesPassed { get; set; }
    public int TotalDevicesFailed { get; set; }
    public double PassRate { get; set; }
    public double FailRate { get; set; }

    public Dictionary<string, int> StageCounts { get; set; } = new();
    public Dictionary<string, int> ZoneCounts { get; set; } = new();

    public List<DailyCountPoint> DailyProcessed { get; set; } = new();
}

public class DailyCountPoint
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class Phase2ManagementSummaryDto
{
    public string SummaryText { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class DeviceListItemDto
{
    public int Id { get; set; }
    public string Serial { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string? SchoolName { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// User Management DTOs
public class SuperAdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLogin { get; set; }
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? Department { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string? Password { get; set; }
    public bool RequirePasswordReset { get; set; } = true;
}

public class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? Department { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
}

public class ChangePasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
    public bool RequirePasswordChangeOnNextLogin { get; set; } = false;
}

public class RoleDto
{
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public string? Description { get; set; }
    public string? Dashboard { get; set; }
}

// Unified Audit Log DTO
public class UnifiedAuditLogDto
{
    public int? Id { get; set; }
    public Guid? SystemLogId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public int? DeviceId { get; set; }
    public string? DeviceSerial { get; set; }
    public string? Details { get; set; }
    public string? MetaJson { get; set; }
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty; // "System" or "Phase2"
}

// Imported Devices DTOs
public class ImportedDeviceListItemDto
{
    public int Id { get; set; }
    public string Serial { get; set; } = string.Empty;
    public long? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string? EmisCode { get; set; }
    public string? District { get; set; }
    public string? Circuit { get; set; }
    public string? ItemDescription { get; set; }
    public string? PodNumber { get; set; }
    public DateTime? DateReceived { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Filters + paging for the Imported devices page
/// </summary>
public class ImportedDeviceFilterDto
{
    public string? Serial { get; set; }
    public string? School { get; set; }
    public string? District { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class ImportedDevicesResultDto
{
    public List<ImportedDeviceListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// School Devices View DTOs
public class CategoryCountDto
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class SchoolDevicesSummaryDto
{
    public long? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string? EmisCode { get; set; }
    public string? District { get; set; }
    public string? Circuit { get; set; }
    
    public int WorkflowCount { get; set; }
    public int ImportedCount { get; set; }
    
    public int TotalCount => WorkflowCount + ImportedCount;
    
    public List<CategoryCountDto> Categories { get; set; } = new();
}

public class SchoolDeviceListItemDto
{
    public string Serial { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;   // "Workflow" or "Imported"
    public string? Stage { get; set; }                   // For Phase2 devices
    public string? Zone { get; set; }                    // For Phase2 devices
    public DateTimeOffset CreatedAt { get; set; }
}

public class SchoolDevicesDetailDto
{
    public SchoolDevicesSummaryDto Summary { get; set; } = new();
    public List<SchoolDeviceListItemDto> Devices { get; set; } = new();
}

// Provincial Analytics DTOs
public class ProvincialAnalyticsRecordDto
{
    public string Province { get; set; } = string.Empty;
    public string ProjectDeviceType { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class DistrictAnalyticsCardDto
{
    public string District { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public int TotalSchools { get; set; }
    public int TotalDevices { get; set; }
    public int ProcessedDevices { get; set; }
    
    public double ProcessingRatePercent => TotalDevices == 0
        ? 0
        : (ProcessedDevices * 100.0 / TotalDevices);
}

public class ProvinceAnalyticsCardDto
{
    public string Province { get; set; } = string.Empty;
    public int TotalSchools { get; set; }
    public int TotalDistricts { get; set; }
    public int TotalDevices { get; set; }
    public int ProcessedDevices { get; set; }
    public List<DistrictAnalyticsCardDto> Districts { get; set; } = new();
    
    public double ProcessingRatePercent => TotalDevices == 0
        ? 0
        : (ProcessedDevices * 100.0 / TotalDevices);
}

public class ProvincialAnalyticsSummaryDto
{
    public int TotalDistricts { get; set; }
    public int TotalSchools { get; set; }
    public int TotalDevices { get; set; }
    public int DevicesProcessed { get; set; }
}

public class ProvincialAnalyticsResultDto
{
    public ProvincialAnalyticsSummaryDto Summary { get; set; } = new();
    public List<ProvinceAnalyticsCardDto> Provinces { get; set; } = new();
    public List<DistrictAnalyticsCardDto> Districts { get; set; } = new();
}

