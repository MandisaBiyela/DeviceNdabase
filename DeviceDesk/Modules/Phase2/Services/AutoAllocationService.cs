using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Infrastructure.Data.Enums;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services;

public class AutoAllocationService
{
    private readonly Phase2DbContext _phase2Db;
    private readonly DeviceDeskDbContext _coreDb;

    public AutoAllocationService(Phase2DbContext phase2Db, DeviceDeskDbContext coreDb)
    {
        _phase2Db = phase2Db;
        _coreDb = coreDb;
    }

    /// <summary>
    /// Get suggested storage allocation for a device based on its school and category.
    /// </summary>
    public async Task<SuggestedAllocationDto?> GetSuggestedAllocationAsync(
        int phase2DeviceId,
        CancellationToken ct = default)
    {
        var device = await _phase2Db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == phase2DeviceId, ct);

        if (device == null)
            return null;

        // Get device category from core Device table
        var coreDevice = await _coreDb.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SerialNumber == device.Serial, ct);

        var category = coreDevice?.Category ?? DeviceCategory.Unknown;

        // Get school ID
        var schoolId = device.SchoolId ?? (coreDevice?.SchoolId ?? null);
        if (!schoolId.HasValue)
            return null;

        return await CalculateNextLocationAsync(schoolId.Value, category, ct);
    }

    /// <summary>
    /// Calculate the next available storage location for a school and category.
    /// </summary>
    public async Task<SuggestedAllocationDto?> CalculateNextLocationAsync(
        long schoolId,
        DeviceCategory category,
        CancellationToken ct = default)
    {
        // Get template for this school and category
        // First try category-specific template (if category is known)
        SchoolStorageTemplate? template = null;
        if (category != DeviceCategory.Unknown)
        {
            template = await _phase2Db.SchoolStorageTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.SchoolId == schoolId && t.Category == category && t.IsActive, ct);
        }

        // If no category-specific template found, try any active template for the school
        if (template == null)
        {
            template = await _phase2Db.SchoolStorageTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.SchoolId == schoolId && t.IsActive, ct);
        }

        if (template == null)
            return null;

        // Get all occupied slots for this school and building/room
        // If category is Unknown, don't filter by category to allow fallback template usage
        var occupiedSlotsQuery = _phase2Db.StorageSlotOccupancies
            .AsNoTracking()
            .Where(o => o.SchoolId == schoolId && 
                       o.IsOccupied &&
                       o.Building == template.Building &&
                       o.Room == template.Room);

        // If category is known, also filter by category for more accurate slot tracking
        if (category != DeviceCategory.Unknown)
        {
            occupiedSlotsQuery = occupiedSlotsQuery.Where(o => o.Category == category);
        }

        var occupiedSlots = await occupiedSlotsQuery
            .Select(o => new OccupiedSlot { Rack = o.Rack, Shelf = o.Shelf, Bin = o.Bin })
            .ToListAsync(ct);

        // Find next available slot
        var (rack, shelf, bin) = FindNextAvailableSlot(template, occupiedSlots);

        if (rack == null)
            return null; // No available slots

        return new SuggestedAllocationDto
        {
            Building = template.Building,
            Room = template.Room,
            Rack = rack,
            Shelf = shelf ?? string.Empty,
            Bin = bin ?? string.Empty,
            Category = category
        };
    }

    /// <summary>
    /// Find the next available slot based on template and occupied slots.
    /// </summary>
    private (string? Rack, string? Shelf, string? Bin) FindNextAvailableSlot(
        SchoolStorageTemplate template,
        List<OccupiedSlot> occupiedSlots)
    {
        // Create a set of occupied slot keys for fast lookup
        var occupiedSet = new HashSet<string>(
            occupiedSlots.Select(o => $"{o.Rack}|{o.Shelf}|{o.Bin}"));

        // Try each rack, shelf, and bin combination
        for (int rackNum = 1; rackNum <= template.MaxRacks; rackNum++)
        {
            var rack = FormatPattern(template.RackPattern, rackNum);
            
            for (int shelfNum = 1; shelfNum <= template.MaxShelvesPerRack; shelfNum++)
            {
                var shelf = FormatPattern(template.ShelfPattern, shelfNum);
                
                for (int binNum = 1; binNum <= template.MaxBinsPerShelf; binNum++)
                {
                    var bin = FormatPattern(template.BinPattern, binNum);
                    
                    var key = $"{rack}|{shelf}|{bin}";
                    if (!occupiedSet.Contains(key))
                    {
                        return (rack, shelf, bin);
                    }
                }
            }
        }

        return (null, null, null); // No available slots
    }

    /// <summary>
    /// Format a pattern string by replacing {n:00} with the number.
    /// Examples: "Rack {n:00}" with n=1 becomes "Rack 01"
    /// </summary>
    private string FormatPattern(string pattern, int number)
    {
        // Match {n:00} or {n:0} patterns
        var regex = new Regex(@"\{n:(\d+)\}");
        var match = regex.Match(pattern);
        
        if (match.Success)
        {
            var format = match.Groups[1].Value;
            var paddedNumber = number.ToString($"D{format.Length}");
            return regex.Replace(pattern, paddedNumber);
        }
        
        // Fallback: replace {n} with number
        return pattern.Replace("{n}", number.ToString());
    }

    /// <summary>
    /// Mark a storage slot as occupied.
    /// </summary>
    public async Task MarkSlotOccupiedAsync(
        int phase2DeviceId,
        long schoolId,
        DeviceCategory category,
        string building,
        string room,
        string rack,
        string shelf,
        string bin,
        CancellationToken ct = default)
    {
        // Check if slot is already occupied
        var existing = await _phase2Db.StorageSlotOccupancies
            .FirstOrDefaultAsync(o => 
                o.SchoolId == schoolId &&
                o.Category == category &&
                o.Building == building &&
                o.Room == room &&
                o.Rack == rack &&
                o.Shelf == shelf &&
                o.Bin == bin &&
                o.IsOccupied, ct);

        if (existing != null)
        {
            // Slot is already occupied by another device
            throw new InvalidOperationException(
                $"Storage slot {building} > {room} > {rack} > {shelf} > {bin} is already occupied.");
        }

        // Mark any previous occupancy for this device as vacated
        var previousOccupancies = await _phase2Db.StorageSlotOccupancies
            .Where(o => o.Phase2DeviceId == phase2DeviceId && o.IsOccupied)
            .ToListAsync(ct);

        foreach (var prev in previousOccupancies)
        {
            prev.IsOccupied = false;
            prev.VacatedAt = DateTimeOffset.UtcNow;
        }

        // Create new occupancy record
        var occupancy = new StorageSlotOccupancy
        {
            SchoolId = schoolId,
            Category = category,
            Building = building,
            Room = room,
            Rack = rack,
            Shelf = shelf,
            Bin = bin,
            Phase2DeviceId = phase2DeviceId,
            IsOccupied = true,
            OccupiedAt = DateTimeOffset.UtcNow
        };

        _phase2Db.StorageSlotOccupancies.Add(occupancy);
        await _phase2Db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Mark a storage slot as available (when device moves or leaves).
    /// </summary>
    public async Task MarkSlotAvailableAsync(
        int phase2DeviceId,
        CancellationToken ct = default)
    {
        var occupancies = await _phase2Db.StorageSlotOccupancies
            .Where(o => o.Phase2DeviceId == phase2DeviceId && o.IsOccupied)
            .ToListAsync(ct);

        foreach (var occupancy in occupancies)
        {
            occupancy.IsOccupied = false;
            occupancy.VacatedAt = DateTimeOffset.UtcNow;
        }

        await _phase2Db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// DTO for suggested allocation.
/// </summary>
public class SuggestedAllocationDto
{
    public string Building { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public DeviceCategory Category { get; set; }
}

/// <summary>
/// Helper class for occupied slot data.
/// </summary>
internal class OccupiedSlot
{
    public string Rack { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
}

