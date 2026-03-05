using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Modules.Phase0.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Infrastructure.Data;

public static class SchoolsSeeder
{
    public static async Task SeedFromCsvAsync(
        DeviceDeskDbContext db,
        string csvPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"[SchoolsSeeder] CSV file not found: {csvPath}");
            return;
        }

        var lines = await File.ReadAllLinesAsync(csvPath, Encoding.UTF8, cancellationToken);
        if (lines.Length <= 1)
        {
            Console.WriteLine("[SchoolsSeeder] CSV file is empty or has no data rows.");
            return;
        }

        Console.WriteLine($"[SchoolsSeeder] Found {lines.Length - 1} data rows in CSV (excluding header).");

        var header = ParseCsvLine(lines[0]);
        var idxEmis = Array.FindIndex(header, h => string.Equals(h.Trim(), "EMIS", StringComparison.OrdinalIgnoreCase));
        var idxDistrict = Array.FindIndex(header, h => string.Equals(h.Trim(), "District", StringComparison.OrdinalIgnoreCase));
        var idxCmc = Array.FindIndex(header, h => string.Equals(h.Trim(), "CMC", StringComparison.OrdinalIgnoreCase));
        var idxCircuit = Array.FindIndex(header, h => string.Equals(h.Trim(), "Circuit", StringComparison.OrdinalIgnoreCase));
        var idxNatEmis = Array.FindIndex(header, h => string.Equals(h.Trim(), "NATEMIS", StringComparison.OrdinalIgnoreCase));
        var idxName = Array.FindIndex(header, h => string.Equals(h.Trim(), "School Name", StringComparison.OrdinalIgnoreCase));

        if (idxEmis < 0 || idxName < 0)
        {
            Console.WriteLine($"[SchoolsSeeder] ERROR: CSV must contain 'EMIS' and 'School Name' columns. Found headers: {string.Join(", ", header)}");
            throw new InvalidOperationException("CSV must contain at least 'EMIS' and 'School Name' columns.");
        }

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var errors = 0;

        // Process in batches to avoid memory issues and provide progress
        const int batchSize = 100;
        var dataLines = lines.Skip(1).ToList();
        var totalLines = dataLines.Count;

        Console.WriteLine($"[SchoolsSeeder] Processing {totalLines} rows in batches of {batchSize}...");

        for (int i = 0; i < dataLines.Count; i += batchSize)
        {
            var batch = dataLines.Skip(i).Take(batchSize).ToList();
            var batchCreated = 0;
            var batchUpdated = 0;
            var batchSkipped = 0;
            var batchErrors = 0;

            foreach (var line in batch)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    batchSkipped++;
                    continue;
                }

                try
                {
                    var columns = ParseCsvLine(line);
                    var emisCode = Get(columns, idxEmis);
                    if (string.IsNullOrWhiteSpace(emisCode))
                    {
                        batchSkipped++;
                        continue;
                    }

                    var name = Get(columns, idxName);
                    var district = Get(columns, idxDistrict);
                    var cmc = Get(columns, idxCmc);
                    var circuit = Get(columns, idxCircuit);
                    var natEmis = Get(columns, idxNatEmis);

                    var existing = await db.Schools.FirstOrDefaultAsync(s => s.EmisCode == emisCode, cancellationToken);
                    if (existing == null)
                    {
                        db.Schools.Add(new School
                        {
                            EmisCode = emisCode,
                            Name = name,
                            District = district,
                            Cmc = cmc,
                            Circuit = circuit,
                            NatEmis = natEmis
                        });
                        batchCreated++;
                    }
                    else
                    {
                        // Update only if fields are empty
                        if (string.IsNullOrWhiteSpace(existing.Name) && !string.IsNullOrWhiteSpace(name))
                            existing.Name = name;
                        if (string.IsNullOrWhiteSpace(existing.District) && !string.IsNullOrWhiteSpace(district))
                            existing.District = district;
                        if (string.IsNullOrWhiteSpace(existing.Cmc) && !string.IsNullOrWhiteSpace(cmc))
                            existing.Cmc = cmc;
                        if (string.IsNullOrWhiteSpace(existing.Circuit) && !string.IsNullOrWhiteSpace(circuit))
                            existing.Circuit = circuit;
                        if (string.IsNullOrWhiteSpace(existing.NatEmis) && !string.IsNullOrWhiteSpace(natEmis))
                            existing.NatEmis = natEmis;
                        batchUpdated++;
                    }
                }
                catch (Exception ex)
                {
                    batchErrors++;
                    Console.WriteLine($"[SchoolsSeeder] Error processing line {i + batch.IndexOf(line) + 2}: {ex.Message}");
                }
            }

            // Save batch
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                created += batchCreated;
                updated += batchUpdated;
                skipped += batchSkipped;
                errors += batchErrors;

                if ((i / batchSize + 1) % 10 == 0 || i + batchSize >= totalLines)
                {
                    Console.WriteLine($"[SchoolsSeeder] Progress: {Math.Min(i + batchSize, totalLines)}/{totalLines} rows processed. Created: {created}, Updated: {updated}, Skipped: {skipped}, Errors: {errors}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SchoolsSeeder] ERROR saving batch: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"[SchoolsSeeder] Inner exception: {ex.InnerException.Message}");
                errors += batch.Count;
            }
        }

        Console.WriteLine($"[SchoolsSeeder] COMPLETE: {created} created, {updated} updated, {skipped} skipped, {errors} errors.");
    }

    private static string[] ParseCsvLine(string line)
    {
        // Simple CSV parser that handles quoted fields
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    current.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    // Toggle quote state
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // End of field
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        // Add last field
        result.Add(current.ToString());
        return result.ToArray();
    }

    private static string Get(string[] columns, int index)
    {
        if (index < 0 || index >= columns.Length) return string.Empty;
        return columns[index]?.Trim() ?? string.Empty;
    }
}

