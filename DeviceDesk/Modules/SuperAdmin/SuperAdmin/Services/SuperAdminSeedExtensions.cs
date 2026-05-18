using System.Globalization;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.SuperAdmin.Data;
using DeviceDesk.Modules.SuperAdmin.Models;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeviceDesk.Modules.SuperAdmin.Services;

public static class SuperAdminSeedExtensions
{
    /// <summary>
    /// Seed ImportedDevices from a CSV file for SuperAdmin visibility.
    /// </summary>
    public static async Task SeedImportedDevicesFromCsvAsync(
        this IServiceProvider services,
        string csvPath,
        bool forceReseed = false)
    {
        using var scope = services.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("SuperAdminImportSeed");
        
        var phase0Db = scope.ServiceProvider.GetRequiredService<DeviceDeskDbContext>();
        var superAdminDb = scope.ServiceProvider.GetRequiredService<SuperAdminDbContext>();
        var phase2Db = scope.ServiceProvider.GetRequiredService<Phase2DbContext>();

        if (!File.Exists(csvPath))
        {
            logger.LogWarning("Imported devices seed file not found at {Path}", csvPath);
            return;
        }

        // Skip if data already exists (unless force reseed)
        if (!forceReseed && await superAdminDb.ImportedDevices.AnyAsync())
        {
            logger.LogInformation("SuperAdmin_ImportedDevices already has data – skipping CSV seed. Use forceReseed=true to reload.");
            return;
        }

        // Clear existing data if force reseeding
        if (forceReseed)
        {
            // 1) Capture existing imported serials BEFORE clearing
            var existingImported = await superAdminDb.ImportedDevices
                .AsNoTracking()
                .ToListAsync();

            var importedSerials = existingImported
                .Where(d => !string.IsNullOrWhiteSpace(d.Serial))
                .Select(d => d.Serial)
                .Distinct()
                .ToList();

            var existingCount = existingImported.Count;
            logger.LogInformation(
                "Force reseed enabled. Clearing {Count} existing records from ImportedDevices...",
                existingCount);

            // 2) Clear ImportedDevices
            superAdminDb.ImportedDevices.RemoveRange(superAdminDb.ImportedDevices);
            await superAdminDb.SaveChangesAsync();

            // 3) Also clear Phase2Devices & core Devices that were created from previous imports
            if (importedSerials.Any())
            {
                var phase2ToRemove = await phase2Db.Devices
                    .Where(d => importedSerials.Contains(d.Serial))
                    .ToListAsync();

                if (phase2ToRemove.Any())
                {
                    logger.LogInformation("Clearing {Count} Phase2Device records created from previous imports...", phase2ToRemove.Count);
                    phase2Db.Devices.RemoveRange(phase2ToRemove);
                    await phase2Db.SaveChangesAsync();
                }

                var phase0ToRemove = await phase0Db.Devices
                    .Where(d => d.SerialNumber != null && importedSerials.Contains(d.SerialNumber))
                    .ToListAsync();

                if (phase0ToRemove.Any())
                {
                    logger.LogInformation("Clearing {Count} core Device records created from previous imports...", phase0ToRemove.Count);
                    phase0Db.Devices.RemoveRange(phase0ToRemove);
                    await phase0Db.SaveChangesAsync();
                }
            }
        }

        var lines = await File.ReadAllLinesAsync(csvPath);
        if (lines.Length <= 1)
        {
            logger.LogWarning("CSV file is empty or has no data rows.");
            return;
        }

        int imported = 0;
        int skipped = 0;
        int phase0Created = 0;
        int phase2Created = 0;
        var devicesToAdd = new List<ImportedDevice>();
        var coreDevicesToAdd = new List<Device>();
        var phase2DevicesToAdd = new List<Phase2Device>();

        // Skip header row (line 0)
        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    skipped++;
                    continue;
                }

                var columns = ParseCsvLine(line);
                if (columns.Length < 10)
                {
                    skipped++;
                    continue;
                }

                // Column mapping from CSV:
                // 0: EMIS, 1: District, 2: CMC, 3: Circuit, 4: School Name
                // 5: District (duplicate), 6: POD Number, 7: Date Received
                // 8: Item Description, 9: Serial Number

                var emisCode = NormalizeEmisCode(GetColumn(columns, 0));
                var district = NormalizeDistrictName(GetColumn(columns, 1));
                var circuit = GetColumn(columns, 3);
                var schoolNameFromFile = GetColumn(columns, 4);
                var podNumber = GetColumn(columns, 6);
                var dateReceivedStr = GetColumn(columns, 7);
                var itemDescription = GetColumn(columns, 8);
                var serial = GetColumn(columns, 9);

                if (string.IsNullOrWhiteSpace(serial))
                {
                    skipped++;
                    continue;
                }

                // Skip duplicates by Serial
                if (devicesToAdd.Any(d => d.Serial == serial))
                {
                    skipped++;
                    continue;
                }

                // Look up school by EMIS
                long? schoolId = null;
                string? schoolName = null;

                if (!string.IsNullOrWhiteSpace(emisCode))
                {
                    var school = await phase0Db.Schools
                        .Where(s => s.EmisCode == emisCode)
                        .Select(s => new { s.SchoolId, s.Name })
                        .FirstOrDefaultAsync();

                    if (school != null)
                    {
                        schoolId = school.SchoolId;
                        schoolName = school.Name;
                    }
                    else
                    {
                        // Fallback: use the name from CSV
                        schoolName = string.IsNullOrWhiteSpace(schoolNameFromFile)
                            ? null
                            : schoolNameFromFile;
                    }
                }

                // Parse "Date Received" as local (SAST, UTC+2)
                DateTime? dateReceived = null;
                if (!string.IsNullOrWhiteSpace(dateReceivedStr) &&
                    DateTime.TryParse(
                        dateReceivedStr,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal,
                        out var parsed))
                {
                    dateReceived = parsed;
                }

                var importedDevice = new ImportedDevice
                {
                    Serial = serial,
                    SchoolId = schoolId,
                    SchoolName = schoolName,
                    EmisCode = emisCode,
                    District = district,
                    Circuit = circuit,
                    ItemDescription = itemDescription,
                    PodNumber = podNumber,
                    DateReceived = dateReceived,
                    CreatedAt = DateTime.UtcNow
                };

                devicesToAdd.Add(importedDevice);
                
                // Create corresponding core Device record (Phase 0)
                var coreDevice = new Device
                {
                    Id = Guid.NewGuid(),
                    SerialNumber = serial,
                    SchoolId = schoolId,
                    SchoolName = schoolName,
                    Model = itemDescription, // Use item description as model
                    Source = "CSV_Import",
                    AllocationType = AllocationType.None,
                    ImportedAt = DateTimeOffset.UtcNow
                };
                coreDevicesToAdd.Add(coreDevice);
                
                // Create corresponding Phase2Device record
                var phase2Device = new Phase2Device
                {
                    Serial = serial,
                    Zone = Phase2Zone.NewStock, // Default to NewStock for imported devices
                    Stage = Phase2Stage.Received, // Start at Received stage
                    SchoolId = schoolId.HasValue ? (int?)schoolId.Value : null,
                    SchoolName = schoolName,
                    ReceivingDate = dateReceived ?? DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                phase2DevicesToAdd.Add(phase2Device);
                
                imported++;
            }
            catch (Exception ex)
            {
                skipped++;
                logger.LogError(ex, "Error importing device from CSV line {LineNumber}", i + 1);
            }
        }

        // Batch insert - do this in order to maintain referential integrity
        if (devicesToAdd.Any())
        {
            // 1. First add core Devices (Phase 0) - check for existing first
            var existingCoreSerials = await phase0Db.Devices
                .Where(d => d.SerialNumber != null && coreDevicesToAdd.Select(cd => cd.SerialNumber).Contains(d.SerialNumber))
                .Select(d => d.SerialNumber)
                .ToListAsync();
            
            var newCoreDevices = coreDevicesToAdd.Where(cd => !existingCoreSerials.Contains(cd.SerialNumber)).ToList();
            if (newCoreDevices.Any())
            {
                await phase0Db.Devices.AddRangeAsync(newCoreDevices);
                await phase0Db.SaveChangesAsync();
                phase0Created = newCoreDevices.Count;
                logger.LogInformation("Created {Count} core Device records", phase0Created);
            }
            
            // 2. Then add Phase2Devices - check for existing first
            var existingPhase2Serials = await phase2Db.Devices
                .Where(d => phase2DevicesToAdd.Select(p2 => p2.Serial).Contains(d.Serial))
                .Select(d => d.Serial)
                .ToListAsync();
            
            var newPhase2Devices = phase2DevicesToAdd.Where(p2 => !existingPhase2Serials.Contains(p2.Serial)).ToList();
            if (newPhase2Devices.Any())
            {
                await phase2Db.Devices.AddRangeAsync(newPhase2Devices);
                await phase2Db.SaveChangesAsync();
                phase2Created = newPhase2Devices.Count;
                logger.LogInformation("Created {Count} Phase2Device records", phase2Created);
            }
            
            // 3. Finally add ImportedDevices for SuperAdmin tracking
            await superAdminDb.ImportedDevices.AddRangeAsync(devicesToAdd);
            await superAdminDb.SaveChangesAsync();
        }

        logger.LogInformation(
            "Imported devices seed completed. ImportedDevices: {Imported}, Core Devices: {Phase0}, Phase2 Devices: {Phase2}, Skipped: {Skipped}",
            imported, phase0Created, phase2Created, skipped);
    }

    private static string[] ParseCsvLine(string line)
    {
        // Simple CSV parser that handles quoted fields
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

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

    private static string GetColumn(string[] columns, int index)
    {
        if (index < 0 || index >= columns.Length)
            return string.Empty;

        return columns[index].Trim().Trim('"');
    }

    private static string NormalizeEmisCode(string emisCode)
    {
        if (string.IsNullOrWhiteSpace(emisCode))
            return emisCode;

        emisCode = emisCode.Trim();

        // Remove .0 decimal if present (e.g., "154512.0" -> "154512")
        if (emisCode.EndsWith(".0"))
        {
            emisCode = emisCode.Substring(0, emisCode.Length - 2);
        }

        return emisCode;
    }

    private static string NormalizeDistrictName(string district)
    {
        if (string.IsNullOrWhiteSpace(district))
            return district;

        // Trim whitespace
        district = district.Trim();

        // Capitalize first letter of each word
        var words = district.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }

        return string.Join(" ", words);
    }
}

