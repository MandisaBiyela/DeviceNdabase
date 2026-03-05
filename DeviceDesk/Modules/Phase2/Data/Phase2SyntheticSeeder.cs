using DeviceDesk.Modules.Phase2.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Data;

public static class Phase2SyntheticSeeder
{
    /// <summary>
    /// Inflates Phase2Devices to a target total with a given QA pass rate.
    /// </summary>
    public static async Task SeedSyntheticDevicesAsync(
        Phase2DbContext db,
        int targetTotal = 375_000,
        double successRate = 0.97, // 97% pass, 3% fail (tune as you like)
        CancellationToken ct = default)
    {
        var currentCount = await db.Devices.CountAsync(ct);
        if (currentCount >= targetTotal)
        {
            Console.WriteLine($"[Phase2SyntheticSeeder] Already have {currentCount} devices. No synthetic seeding needed.");
            return;
        }

        // Use existing devices (real imports) as templates, or create synthetic templates if table is empty
        var templates = await db.Devices
            .OrderBy(d => d.Id)
            .Take(1_000) // enough variety; change if you want
            .ToListAsync(ct);

        // Initialize Random once for the entire method
        var rand = new Random();
        
        if (!templates.Any())
        {
            Console.WriteLine("[Phase2SyntheticSeeder] No existing devices found. Creating synthetic templates...");
            
            // Create synthetic template devices with realistic defaults
            var syntheticTemplates = new List<Phase2Device>();
            var zones = Enum.GetValues<Phase2Zone>();
            var stages = new[] { Phase2Stage.Received, Phase2Stage.PreAssessment, Phase2Stage.DetailedInspection, 
                                Phase2Stage.HardwareDept, Phase2Stage.SoftwareDept, Phase2Stage.QualityAssessment };
            var attentionLevels = Enum.GetValues<AttentionRequired>();
            var repairCategories = new[] { "Board Repair", "Screen Replacement", "Battery Replacement", "Software Update", "Keyboard Repair", "Charger Port" };
            
            // Create 100 synthetic templates with variety
            for (int i = 0; i < 100; i++)
            {
                var templateCreatedAt = DateTime.UtcNow.AddDays(-rand.Next(30, 730));
                var templateUpdatedAt = templateCreatedAt.AddDays(rand.Next(0, 60));
                
                var template = new Phase2Device
                {
                    Serial = $"TEMPLATE-{i + 1:D6}",
                    Zone = zones[rand.Next(zones.Length)],
                    Stage = stages[rand.Next(stages.Length)],
                    
                    IctClerkId = $"clerk-{rand.Next(1, 10)}",
                    ReceivingDate = templateCreatedAt,
                    VerificationStatus = true,
                    PreAssessmentPassed = rand.NextDouble() < 0.8, // 80% pass pre-assessment
                    PreAssessmentInspectorId = $"inspector-{rand.Next(1, 5)}",
                    AttentionRequired = attentionLevels[rand.Next(attentionLevels.Length)],
                    PreAssessmentNotes = rand.NextDouble() < 0.3 ? $"Template notes {i + 1}" : null,
                    
                    UnderWarranty = rand.NextDouble() < 0.4,
                    Repairable = rand.NextDouble() < 0.85,
                    TechnicianId = $"tech-{rand.Next(1, 8)}",
                    InspectionDate = templateCreatedAt.AddDays(rand.Next(0, 10)),
                    RepairCategory = repairCategories[rand.Next(repairCategories.Length)],
                    DisposalRequested = rand.NextDouble() < 0.1,
                    
                    QaPassed = rand.NextDouble() < 0.97, // 97% pass rate for templates
                    QaInspectorId = $"qa-inspector-{rand.Next(1, 4)}",
                    ReworkCount = rand.Next(0, 3),
                    
                    ReceiptId = null,
                    SchoolId = null, // Will be set randomly when creating devices
                    SchoolName = null,
                    
                    ScannedOutAt = rand.NextDouble() < 0.7 ? templateUpdatedAt.AddDays(rand.Next(1, 30)) : null,
                    ScannedOutByUserId = rand.NextDouble() < 0.7 ? $"user-{rand.Next(1, 5)}" : null,
                    DispatchStatus = null,
                    
                    CreatedAt = templateCreatedAt,
                    UpdatedAt = templateUpdatedAt
                };
                
                syntheticTemplates.Add(template);
            }
            
            // Save templates to database so we can use them
            await db.Devices.AddRangeAsync(syntheticTemplates, ct);
            await db.SaveChangesAsync(ct);
            
            // Reload from DB to get IDs
            templates = await db.Devices
                .OrderBy(d => d.Id)
                .Take(100)
                .ToListAsync(ct);
            
            Console.WriteLine($"[Phase2SyntheticSeeder] Created {templates.Count} synthetic template devices.");
            
            // Recalculate current count after creating templates
            currentCount = await db.Devices.CountAsync(ct);
        }

        Console.WriteLine($"[Phase2SyntheticSeeder] Starting from {currentCount} devices, target = {targetTotal}.");
        Console.WriteLine($"[Phase2SyntheticSeeder] Using {templates.Count} template devices.");

        var batchSize = 2_000;
        var toCreate = targetTotal - currentCount;
        var created = 0;

        // Simple distribution tracking
        int passCount = 0;
        int failCount = 0;

        // Helper to generate a clearly fake but unique serial
        string BuildFakeSerial(string baseSerial, int sequence)
        {
            // keep the start of the real serial so it still "looks right"
            var prefix = string.IsNullOrWhiteSpace(baseSerial)
                ? "SYNTH"
                : baseSerial.Length <= 12
                    ? baseSerial
                    : baseSerial.Substring(0, 12);

            // sequence ensures uniqueness
            return $"{prefix}-S{sequence:D6}";
        }

        while (created < toCreate)
        {
            var batch = new List<Phase2Device>(batchSize);

            foreach (var tpl in templates)
            {
                if (created >= toCreate) break;

                var globalIndex = currentCount + created + 1;

                // Random timeline over ~2 years
                var createdAt = DateTime.UtcNow.AddDays(-rand.Next(30, 730));
                var updatedAt = createdAt.AddDays(rand.Next(0, 60));

                var isSuccess = rand.NextDouble() < successRate;
                if (isSuccess) passCount++; else failCount++;

                var device = new Phase2Device
                {
                    Serial = BuildFakeSerial(tpl.Serial, globalIndex),

                    Zone = tpl.Zone,
                    // Set stage based on QA outcome
                    Stage = isSuccess 
                        ? Phase2Stage.AwaitingDispatch 
                        : Phase2Stage.HardwareDept, // Failed devices go back for rework

                    IctClerkId = tpl.IctClerkId,
                    ReceivingDate = tpl.ReceivingDate ?? createdAt,
                    VerificationStatus = true,
                    PreAssessmentPassed = true,
                    PreAssessmentInspectorId = tpl.PreAssessmentInspectorId,
                    AttentionRequired = tpl.AttentionRequired,
                    PreAssessmentNotes = tpl.PreAssessmentNotes,

                    UnderWarranty = tpl.UnderWarranty ?? rand.NextDouble() < 0.4,
                    Repairable = tpl.Repairable ?? isSuccess,
                    TechnicianId = tpl.TechnicianId,
                    InspectionDate = tpl.InspectionDate ?? createdAt.AddDays(rand.Next(0, 10)),
                    RepairCategory = string.IsNullOrWhiteSpace(tpl.RepairCategory)
                        ? (isSuccess ? "Board Repair" : "Scrap/Non-repairable")
                        : tpl.RepairCategory,
                    DisposalRequested = !isSuccess && (tpl.DisposalRequested ?? (rand.NextDouble() < 0.25)),

                    // QA outcome is what you want for the dashboard
                    QaPassed = isSuccess,
                    QaInspectorId = tpl.QaInspectorId,
                    ReworkCount = isSuccess ? rand.Next(0, 2) : rand.Next(1, 4),

                    ReceiptId = tpl.ReceiptId,
                    // Assign random school data if template doesn't have it
                    SchoolId = tpl.SchoolId ?? (rand.NextDouble() < 0.9 ? rand.Next(1, 1000) : null),
                    SchoolName = tpl.SchoolName ?? (rand.NextDouble() < 0.9 ? $"Synthetic School {rand.Next(1, 500)}" : null),

                    ScannedOutAt = isSuccess
                        ? updatedAt.AddDays(rand.Next(1, 30))
                        : null,
                    ScannedOutByUserId = tpl.ScannedOutByUserId,
                    DispatchStatus = tpl.DispatchStatus,

                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt
                };

                batch.Add(device);
                created++;

                if (batch.Count >= batchSize)
                    break;
            }

            if (batch.Count > 0)
            {
                await db.Devices.AddRangeAsync(batch, ct);
                await db.SaveChangesAsync(ct);

                Console.WriteLine(
                    $"[Phase2SyntheticSeeder] Seeded {created}/{toCreate} synthetic devices " +
                    $"(Pass: {passCount}, Fail: {failCount}).");
            }
            else
            {
                // No more devices to create or batch is empty - exit loop
                break;
            }
        }

        Console.WriteLine(
            $"[Phase2SyntheticSeeder] COMPLETE. Added {created} synthetic devices. " +
            $"Pass: {passCount}, Fail: {failCount}. Total in DB ~ {targetTotal}.");
    }
}

