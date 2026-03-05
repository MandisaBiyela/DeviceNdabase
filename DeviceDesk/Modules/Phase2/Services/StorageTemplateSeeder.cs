using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Infrastructure.Data.Enums;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Services;

public static class StorageTemplateSeeder
{
    /// <summary>
    /// Generate SchoolStorageTemplate records for all schools and all device categories.
    /// Creates templates for each school for each category (Laptop, Desktop, Printer, Monitor, VRHeadset, Other).
    /// </summary>
    public static async Task<int> GenerateForAllSchoolsAsync(
        DeviceDeskDbContext coreDb,
        Phase2DbContext phase2Db,
        CancellationToken cancellationToken = default)
    {
        // Get all schools from the database
        var schools = await coreDb.Schools
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (!schools.Any())
        {
            Console.WriteLine("[StorageTemplateSeeder] No schools found in database.");
            return 0;
        }

        Console.WriteLine($"[StorageTemplateSeeder] Found {schools.Count} schools. Generating storage templates for all categories...");

        // Get all device categories (excluding Unknown)
        var categories = Enum.GetValues<DeviceCategory>()
            .Where(c => c != DeviceCategory.Unknown)
            .ToList();

        // Get existing templates
        var existingTemplates = await phase2Db.SchoolStorageTemplates
            .AsNoTracking()
            .Select(t => new { t.SchoolId, t.Category })
            .ToListAsync(cancellationToken);

        var existingKeys = new HashSet<(long SchoolId, DeviceCategory Category)>(
            existingTemplates.Select(t => (t.SchoolId, t.Category)));

        int created = 0;
        int skipped = 0;
        int errors = 0;

        // Process in batches to avoid memory issues
        const int batchSize = 100;
        for (int i = 0; i < schools.Count; i += batchSize)
        {
            var batch = schools.Skip(i).Take(batchSize).ToList();

            foreach (var school in batch)
            {
                foreach (var category in categories)
                {
                    try
                    {
                        var key = (school.SchoolId, category);
                        if (existingKeys.Contains(key))
                        {
                            skipped++;
                            continue;
                        }

                        // Create template for this school and category
                        var template = new SchoolStorageTemplate
                        {
                            SchoolId = school.SchoolId,
                            Category = category,
                            Building = "ICT Centre Main",
                            Room = "Room 1",
                            RackPattern = "Rack {n:00}",
                            ShelfPattern = "Shelf {n:00}",
                            BinPattern = "Bin {n:00}",
                            MaxRacks = 10,
                            MaxShelvesPerRack = 10,
                            MaxBinsPerShelf = 10,
                            IsActive = true,
                            CreatedAt = DateTimeOffset.UtcNow
                        };

                        phase2Db.SchoolStorageTemplates.Add(template);
                        existingKeys.Add(key); // Track in memory to avoid duplicates in same batch
                        created++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StorageTemplateSeeder] Error creating template for school {school.SchoolId} ({school.Name}), category {category}: {ex.Message}");
                        errors++;
                    }
                }
            }

            // Save batch
            try
            {
                await phase2Db.SaveChangesAsync(cancellationToken);
                if ((i / batchSize + 1) % 10 == 0 || i + batchSize >= schools.Count)
                {
                    Console.WriteLine($"[StorageTemplateSeeder] Progress: {Math.Min(i + batchSize, schools.Count)}/{schools.Count} schools processed. Created: {created}, Skipped: {skipped}, Errors: {errors}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StorageTemplateSeeder] Error saving batch: {ex.Message}");
                errors += batch.Count * categories.Count;
            }
        }

        Console.WriteLine($"[StorageTemplateSeeder] COMPLETE: {created} templates created, {skipped} skipped (already exist), {errors} errors.");
        return created;
    }

    /// <summary>
    /// Generate storage templates for specific school IDs.
    /// </summary>
    public static async Task<int> GenerateForSchoolsAsync(
        DeviceDeskDbContext coreDb,
        Phase2DbContext phase2Db,
        IEnumerable<long> schoolIds,
        CancellationToken cancellationToken = default)
    {
        var schoolIdList = schoolIds.ToList();
        if (!schoolIdList.Any())
        {
            return 0;
        }

        // Get schools
        var schools = await coreDb.Schools
            .AsNoTracking()
            .Where(s => schoolIdList.Contains(s.SchoolId))
            .ToListAsync(cancellationToken);

        // Get all device categories (excluding Unknown)
        var categories = Enum.GetValues<DeviceCategory>()
            .Where(c => c != DeviceCategory.Unknown)
            .ToList();

        // Get existing templates for these schools
        var existingTemplates = await phase2Db.SchoolStorageTemplates
            .AsNoTracking()
            .Where(t => schoolIdList.Contains(t.SchoolId))
            .Select(t => new { t.SchoolId, t.Category })
            .ToListAsync(cancellationToken);

        var existingKeys = new HashSet<(long SchoolId, DeviceCategory Category)>(
            existingTemplates.Select(t => (t.SchoolId, t.Category)));

        int created = 0;
        foreach (var school in schools)
        {
            foreach (var category in categories)
            {
                var key = (school.SchoolId, category);
                if (existingKeys.Contains(key))
                    continue;

                var template = new SchoolStorageTemplate
                {
                    SchoolId = school.SchoolId,
                    Category = category,
                    Building = "ICT Centre Main",
                    Room = "Room 1",
                    RackPattern = "Rack {n:00}",
                    ShelfPattern = "Shelf {n:00}",
                    BinPattern = "Bin {n:00}",
                    MaxRacks = 10,
                    MaxShelvesPerRack = 10,
                    MaxBinsPerShelf = 10,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                phase2Db.SchoolStorageTemplates.Add(template);
                existingKeys.Add(key);
                created++;
            }
        }

        await phase2Db.SaveChangesAsync(cancellationToken);
        return created;
    }
}

