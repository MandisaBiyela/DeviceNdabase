using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Infrastructure.Data;
using System.Globalization;

namespace DeviceDesk.Modules.SuperAdmin.Controllers;

[ApiController]
[Route("api/superadmin/csv-import")]
public class CsvImportController : ControllerBase
{
    private readonly Phase2DbContext _phase2Db;
    private readonly DeviceDeskDbContext _phase0Db;
    private readonly ILogger<CsvImportController> _logger;

    public CsvImportController(
        Phase2DbContext phase2Db,
        DeviceDeskDbContext phase0Db,
        ILogger<CsvImportController> logger)
    {
        _phase2Db = phase2Db;
        _phase0Db = phase0Db;
        _logger = logger;
    }

    [HttpPost("devices")]
    public async Task<IActionResult> ImportDevicesFromCsv([FromQuery] string? csvPath = null)
    {
        try
        {
            if (string.IsNullOrEmpty(csvPath))
            {
                csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Seeds", "Schools_Populated_Siyanda_Fixed_Dates_Cleaned.csv");
            }

            if (!System.IO.File.Exists(csvPath))
            {
                return BadRequest(new { error = $"CSV file not found: {csvPath}" });
            }

            var lines = await System.IO.File.ReadAllLinesAsync(csvPath);
            if (lines.Length <= 1)
            {
                return BadRequest(new { error = "CSV file is empty or has no data rows" });
            }

            _logger.LogInformation($"Starting device import from: {csvPath}");
            _logger.LogInformation($"Found {lines.Length - 1} data rows");

            // Parse header
            var header = lines[0].Split(',');
            var idxEmis = Array.FindIndex(header, h => h.Trim().Equals("EMIS", StringComparison.OrdinalIgnoreCase));
            var idxDistrict = Array.FindIndex(header, h => h.Trim().Equals("District", StringComparison.OrdinalIgnoreCase));
            var idxSchoolName = Array.FindIndex(header, h => h.Trim().Contains("School", StringComparison.OrdinalIgnoreCase));
            var idxPodNumber = Array.FindIndex(header, h => h.Trim().Contains("POD", StringComparison.OrdinalIgnoreCase));
            var idxDateReceived = Array.FindIndex(header, h => h.Trim().Contains("Date", StringComparison.OrdinalIgnoreCase));
            var idxItemDesc = Array.FindIndex(header, h => h.Trim().Contains("Item", StringComparison.OrdinalIgnoreCase));
            var idxSerial = Array.FindIndex(header, h => h.Trim().Contains("Serial", StringComparison.OrdinalIgnoreCase));

            if (idxEmis < 0 || idxSerial < 0)
            {
                return BadRequest(new { error = "CSV must contain EMIS and Serial Number columns" });
            }

            var imported = 0;
            var skipped = 0;
            var errors = 0;

            // Get all schools for quick lookup
            var schools = await _phase0Db.Schools.ToDictionaryAsync(s => s.EmisCode, s => s);
            _logger.LogInformation($"Loaded {schools.Count} schools for lookup");

            // Process each line
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = ParseCsvLine(line);
                    if (parts.Length <= Math.Max(idxEmis, idxSerial)) continue;

                    var emisCode = parts[idxEmis].Trim();
                    var serialNumber = parts[idxSerial].Trim();

                    if (string.IsNullOrEmpty(emisCode) || string.IsNullOrEmpty(serialNumber))
                    {
                        skipped++;
                        continue;
                    }

                    // Check if device already exists
                    var exists = await _phase2Db.Devices.AnyAsync(d => d.Serial == serialNumber);
                    if (exists)
                    {
                        skipped++;
                        continue;
                    }

                    // Look up school
                    schools.TryGetValue(emisCode, out var school);
                    var schoolId = school?.SchoolId;
                    var schoolName = school?.Name ?? (idxSchoolName >= 0 && idxSchoolName < parts.Length ? parts[idxSchoolName].Trim() : null);

                    // Parse date received
                    DateTime? dateReceived = null;
                    if (idxDateReceived >= 0 && idxDateReceived < parts.Length)
                    {
                        var dateStr = parts[idxDateReceived].Trim();
                        if (DateTime.TryParse(dateStr, out var parsedDate))
                        {
                            dateReceived = parsedDate;
                        }
                    }

                    // Get device description
                    var itemDescription = idxItemDesc >= 0 && idxItemDesc < parts.Length ? parts[idxItemDesc].Trim() : "Device";

                    // Create Phase2Device
                    var device = new Phase2Device
                    {
                        Serial = serialNumber,
                        SchoolId = schoolId.HasValue ? (int)schoolId.Value : null,
                        SchoolName = schoolName,
                        Stage = Phase2Stage.AwaitingDispatch,
                        Zone = Phase2Zone.NewStock,
                        CreatedAt = dateReceived ?? DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        // Simulate that 75% of devices have been QA'd and passed
                        QaPassed = (i % 4 != 0) ? true : (bool?)null,
                        PreAssessmentPassed = true,
                        UnderWarranty = true,
                        Repairable = true,
                        PreAssessmentNotes = itemDescription  // Store item description in notes
                    };

                    _phase2Db.Devices.Add(device);
                    imported++;

                    // Batch save every 100 records
                    if (imported % 100 == 0)
                    {
                        await _phase2Db.SaveChangesAsync();
                        _logger.LogInformation($"Imported {imported} devices so far...");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing line {i}: {ex.Message}");
                    errors++;
                }
            }

            // Final save
            await _phase2Db.SaveChangesAsync();

            _logger.LogInformation($"Import complete: {imported} imported, {skipped} skipped, {errors} errors");

            return Ok(new
            {
                success = true,
                imported = imported,
                skipped = skipped,
                errors = errors,
                totalProcessed = imported + skipped + errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing devices from CSV");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    [HttpPost("schools")]
    public async Task<IActionResult> ImportSchoolsFromCsv([FromQuery] string? csvPath = null)
    {
        try
        {
            if (string.IsNullOrEmpty(csvPath))
            {
                csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Seeds", "schools_emis.csv");
            }

            if (!System.IO.File.Exists(csvPath))
            {
                return BadRequest(new { error = $"CSV file not found: {csvPath}" });
            }

            var lines = await System.IO.File.ReadAllLinesAsync(csvPath);
            if (lines.Length <= 1)
            {
                return BadRequest(new { error = "CSV file is empty" });
            }

            var imported = 0;
            var updated = 0;

            // Parse header
            var header = lines[0].Split(',');
            var idxEmis = Array.FindIndex(header, h => h.Trim().Equals("EMIS", StringComparison.OrdinalIgnoreCase));
            var idxDistrict = Array.FindIndex(header, h => h.Trim().Equals("District", StringComparison.OrdinalIgnoreCase));
            var idxCmc = Array.FindIndex(header, h => h.Trim().Equals("CMC", StringComparison.OrdinalIgnoreCase));
            var idxCircuit = Array.FindIndex(header, h => h.Trim().Equals("Circuit", StringComparison.OrdinalIgnoreCase));
            var idxNatEmis = Array.FindIndex(header, h => h.Trim().Equals("NATEMIS", StringComparison.OrdinalIgnoreCase));
            var idxName = Array.FindIndex(header, h => h.Trim().Contains("School Name", StringComparison.OrdinalIgnoreCase));

            // Process each line
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = ParseCsvLine(lines[i]);
                if (parts.Length <= Math.Max(idxEmis, idxName)) continue;

                var emisCode = parts[idxEmis].Trim();
                var schoolName = parts[idxName].Trim();

                if (string.IsNullOrEmpty(emisCode) || string.IsNullOrEmpty(schoolName)) continue;

                var existing = await _phase0Db.Schools.FirstOrDefaultAsync(s => s.EmisCode == emisCode);

                if (existing != null)
                {
                    // Update existing
                    if (idxDistrict >= 0 && idxDistrict < parts.Length)
                        existing.District = parts[idxDistrict].Trim();
                    if (idxCmc >= 0 && idxCmc < parts.Length)
                        existing.Cmc = parts[idxCmc].Trim();
                    if (idxCircuit >= 0 && idxCircuit < parts.Length)
                        existing.Circuit = parts[idxCircuit].Trim();
                    if (idxNatEmis >= 0 && idxNatEmis < parts.Length)
                        existing.NatEmis = parts[idxNatEmis].Trim();

                    updated++;
                }
                else
                {
                    // Create new
                    var school = new School
                    {
                        EmisCode = emisCode,
                        Name = schoolName,
                        District = idxDistrict >= 0 && idxDistrict < parts.Length ? parts[idxDistrict].Trim() : null,
                        Cmc = idxCmc >= 0 && idxCmc < parts.Length ? parts[idxCmc].Trim() : null,
                        Circuit = idxCircuit >= 0 && idxCircuit < parts.Length ? parts[idxCircuit].Trim() : null,
                        NatEmis = idxNatEmis >= 0 && idxNatEmis < parts.Length ? parts[idxNatEmis].Trim() : null
                    };

                    _phase0Db.Schools.Add(school);
                    imported++;
                }

                if ((imported + updated) % 100 == 0)
                {
                    await _phase0Db.SaveChangesAsync();
                }
            }

            await _phase0Db.SaveChangesAsync();

            return Ok(new { imported, updated, total = imported + updated });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing schools");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetImportStatus()
    {
        try
        {
            var schoolCount = await _phase0Db.Schools.CountAsync();
            var deviceCount = await _phase2Db.Devices.CountAsync();
            var processedCount = await _phase2Db.Devices.CountAsync(d => d.QaPassed != null);

            return Ok(new
            {
                schools = schoolCount,
                totalDevices = deviceCount,
                devicesProcessed = processedCount,
                devicesPassedQa = await _phase2Db.Devices.CountAsync(d => d.QaPassed == true),
                devicesFailedQa = await _phase2Db.Devices.CountAsync(d => d.QaPassed == false),
                devicesPendingQa = await _phase2Db.Devices.CountAsync(d => d.QaPassed == null)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("provincial-device-types")]
    public async Task<IActionResult> GetProvincialDeviceTypes()
    {
        try
        {
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Seeds", "provincial_device_types.csv");

            if (!System.IO.File.Exists(csvPath))
            {
                return Ok(new { provinces = new Dictionary<string, object>() });
            }

            var lines = await System.IO.File.ReadAllLinesAsync(csvPath);
            if (lines.Length <= 1)
            {
                return Ok(new { provinces = new Dictionary<string, object>() });
            }

            // Parse CSV: Province, Project/Device Type, Quantity
            var provinceData = new Dictionary<string, Dictionary<string, int>>();

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 3) continue;

                var province = parts[0].Trim();
                var deviceType = parts[1].Trim();
                var quantity = int.TryParse(parts[2].Trim(), out var qty) ? qty : 0;

                if (!provinceData.ContainsKey(province))
                {
                    provinceData[province] = new Dictionary<string, int>();
                }

                provinceData[province][deviceType] = quantity;
            }

            // Calculate totals per province
            var result = provinceData.Select(p => new
            {
                province = p.Key,
                deviceTypes = p.Value,
                totalDevices = p.Value.Values.Sum(),
                deviceTypeCount = p.Value.Count
            }).OrderByDescending(p => p.totalDevices).ToList();

            return Ok(new { provinces = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading provincial device types");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}

