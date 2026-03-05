using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Infrastructure.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Infrastructure.Seeding;

public static class StorageLocationSeeder
{
    /// <summary>
    /// Generate StorageLocation records for all schools in the database.
    /// Creates one default storage location per school if it doesn't already exist.
    /// </summary>
    public static async Task<int> GenerateForAllSchoolsAsync(
        DeviceDeskDbContext db,
        CancellationToken cancellationToken = default)
    {
        // Get all schools from the database
        var schools = await db.Schools
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (!schools.Any())
        {
            Console.WriteLine("[StorageLocationSeeder] No schools found in database.");
            return 0;
        }

        Console.WriteLine($"[StorageLocationSeeder] Found {schools.Count} schools. Generating storage locations...");

        // Get existing storage locations for schools
        var existingLocations = await db.StorageLocations
            .Where(sl => sl.SchoolId != null)
            .Select(sl => sl.SchoolId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var existingSchoolIds = new HashSet<long>(existingLocations);

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
                try
                {
                    // Skip if storage location already exists for this school
                    if (existingSchoolIds.Contains(school.SchoolId))
                    {
                        skipped++;
                        continue;
                    }

                    // Create default storage location for this school
                    var location = new StorageLocation
                    {
                        SchoolId = school.SchoolId,
                        Category = DeviceCategory.Unknown, // Default category, can be changed later
                        Area = StorageArea.Phase2IctCenter, // ICT Centre storage area
                        Name = $"{school.Name} - ICT Storage",
                        LocationCode = $"SCHOOL-{school.SchoolId}", // Unique code per school
                        IsActive = true,
                        IsDispatchReadyZone = false
                    };

                    db.StorageLocations.Add(location);
                    existingSchoolIds.Add(school.SchoolId); // Track in memory to avoid duplicates in same batch
                    created++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StorageLocationSeeder] Error creating storage location for school {school.SchoolId} ({school.Name}): {ex.Message}");
                    errors++;
                }
            }

            // Save batch
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                Console.WriteLine($"[StorageLocationSeeder] Progress: {Math.Min(i + batchSize, schools.Count)}/{schools.Count} schools processed. Created: {created}, Skipped: {skipped}, Errors: {errors}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StorageLocationSeeder] Error saving batch: {ex.Message}");
                errors += batch.Count(b => !existingSchoolIds.Contains(b.SchoolId));
            }
        }

        Console.WriteLine($"[StorageLocationSeeder] COMPLETE: {created} created, {skipped} skipped (already exist), {errors} errors.");
        return created;
    }

    /// <summary>
    /// Generate storage locations for specific school IDs.
    /// </summary>
    public static async Task<int> GenerateForSchoolsAsync(
        DeviceDeskDbContext db,
        IEnumerable<long> schoolIds,
        CancellationToken cancellationToken = default)
    {
        var schoolIdList = schoolIds.ToList();
        if (!schoolIdList.Any())
        {
            return 0;
        }

        // Get schools
        var schools = await db.Schools
            .AsNoTracking()
            .Where(s => schoolIdList.Contains(s.SchoolId))
            .ToListAsync(cancellationToken);

        // Get existing storage locations for these schools
        var existingLocations = await db.StorageLocations
            .Where(sl => sl.SchoolId != null && schoolIdList.Contains(sl.SchoolId.Value))
            .Select(sl => sl.SchoolId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var existingSchoolIds = new HashSet<long>(existingLocations);

        int created = 0;
        foreach (var school in schools)
        {
            if (existingSchoolIds.Contains(school.SchoolId))
                continue;

            var location = new StorageLocation
            {
                SchoolId = school.SchoolId,
                Category = DeviceCategory.Unknown,
                Area = StorageArea.Phase2IctCenter,
                Name = $"{school.Name} - ICT Storage",
                LocationCode = $"SCHOOL-{school.SchoolId}",
                IsActive = true,
                IsDispatchReadyZone = false
            };

            db.StorageLocations.Add(location);
            created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return created;
    }
}

