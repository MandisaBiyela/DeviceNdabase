using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase0.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using ExcelDataReader;

namespace DeviceDesk.Modules.Phase0.Services
{
    public class CsvImportService
    {
        private readonly DeviceDeskDbContext _db;
        private readonly RnrBatchService _rnrBatchService;
        
        public CsvImportService(DeviceDeskDbContext db, RnrBatchService rnrBatchService)
        {
            _db = db;
            _rnrBatchService = rnrBatchService;
        }

        public async Task<ImportResultDto> ImportAsync(IFormFile file, string source, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("File is required");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (ext == ".csv")
            {
                return await ImportCsvAsync(file, source, ct);
            }
            else if (ext == ".xlsx" || ext == ".xls")
            {
                return await ImportExcelAsync(file, source, ct);
            }
            else
            {
                throw new InvalidOperationException("Unsupported file type. Please upload CSV or Excel (.xlsx/.xls).");
            }
        }

        private async Task<ImportResultDto> ImportCsvAsync(IFormFile file, string source, CancellationToken ct)
        {
            var batch = new DeviceImportBatch { Source = source, FileName = file.FileName };
            _db.Batches.Add(batch);

            using var sr = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            string? line;
            bool headerRead = false;
            var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var seenInUpload = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var isNewSource = string.Equals(source, "NEW", StringComparison.OrdinalIgnoreCase);

            while ((line = await sr.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cols = line.Split(',', StringSplitOptions.TrimEntries);

                if (!headerRead)
                {
                    for (int i = 0; i < cols.Length; i++)
                        header[cols[i]] = i;

                    bool hasSerialOrImei = header.ContainsKey("Serial") || header.ContainsKey("IMEI");
                    bool hasNewOrderHeader = HasNewOrderHeader(header);

                    if (isNewSource)
                    {
                        // For NEW we accept EITHER device-style OR order-style headers
                        if (!hasSerialOrImei && !hasNewOrderHeader)
                        {
                            throw new InvalidOperationException(
                                "CSV header is invalid for NEW stock. " +
                                "Expected either device-style headers including 'Serial' or 'IMEI', " +
                                "OR order-style headers: OrderNumber,Brand,Model,DeviceType,Description,Quantity.");
                        }
                    }
                    else
                    {
                        // Legacy behaviour for non-NEW sources
                        if (!hasSerialOrImei)
                            throw new InvalidOperationException("CSV header must include 'Serial' or 'IMEI'");
                    }

                    headerRead = true;
                    continue;
                }

                // NEW source + order-style header → special processing
                if (isNewSource &&
                    HasNewOrderHeader(header) &&
                    !header.ContainsKey("Serial") &&
                    !header.ContainsKey("IMEI"))
                {
                    await ProcessNewOrderRow(cols, header, batch);
                }
                else
                {
                    // Default behaviour: device-style import using Serial/IMEI
                    await ProcessRow(cols, header, source, batch, seenInUpload, ct);
                }
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                
                // Route to appropriate batch creation based on source - CREATE BATCH EVEN WITHOUT OrderNumber!
                if (batch.Added > 0)  // Only need devices added, OrderNumber is optional
                {
                    if (string.Equals(source, "NEW", StringComparison.OrdinalIgnoreCase))
                    {
                        await CreateNewStockBatchFromImport(batch, ct);
                    }
                    else if (string.Equals(source, "RNR", StringComparison.OrdinalIgnoreCase))
                    {
                        await CreateRnrBatchFromImport(batch, ct);
                    }
                }
                
                return new ImportResultDto(batch.BatchId, batch.Added, batch.Duplicates, batch.Invalid, batch.Total);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CSV Import Error] {ex.InnerException?.Message ?? ex.Message}");
                Console.WriteLine($"[CSV Import Stack] {ex}");
                throw new InvalidOperationException($"Database save failed: {ex.InnerException?.Message ?? ex.Message}", ex);
            }
        }

        private async Task<ImportResultDto> ImportExcelAsync(IFormFile file, string source, CancellationToken ct)
        {
            var batch = new DeviceImportBatch { Source = source, FileName = file.FileName };
            _db.Batches.Add(batch);

            System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);

            bool headerRead = false;
            var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var seenInUpload = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var isNewSource = string.Equals(source, "NEW", StringComparison.OrdinalIgnoreCase);

            do
            {
                while (reader.Read())
                {
                    var cols = new string[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                        cols[i] = reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty;

                    if (!headerRead)
                    {
                        for (int i = 0; i < cols.Length; i++)
                            header[cols[i]] = i;

                        bool hasSerialOrImei = header.ContainsKey("Serial") || header.ContainsKey("IMEI");
                        bool hasNewOrderHeader = HasNewOrderHeader(header);

                        if (isNewSource)
                        {
                            // For NEW we accept EITHER device-style OR order-style headers
                            if (!hasSerialOrImei && !hasNewOrderHeader)
                            {
                                throw new InvalidOperationException(
                                    "Excel header is invalid for NEW stock. " +
                                    "Expected either device-style headers including 'Serial' or 'IMEI', " +
                                    "OR order-style headers: OrderNumber,Brand,Model,DeviceType,Description,Quantity.");
                            }
                        }
                        else
                        {
                            if (!hasSerialOrImei)
                                throw new InvalidOperationException("Excel header must include 'Serial' or 'IMEI'");
                        }

                        headerRead = true;
                        continue;
                    }

                    // NEW source + order-style header → special processing
                    if (isNewSource &&
                        HasNewOrderHeader(header) &&
                        !header.ContainsKey("Serial") &&
                        !header.ContainsKey("IMEI"))
                    {
                        await ProcessNewOrderRow(cols, header, batch);
                    }
                    else
                    {
                        // Default behaviour: device-style import using Serial/IMEI
                        await ProcessRow(cols, header, source, batch, seenInUpload, ct);
                    }
                }

                // Only process first sheet by default
                break;
            } while (reader.NextResult());

            try
            {
                await _db.SaveChangesAsync(ct);
                
                // Route to appropriate batch creation based on source
                if (!string.IsNullOrWhiteSpace(batch.OrderNumber) && batch.Added > 0)
                {
                    if (string.Equals(source, "NEW", StringComparison.OrdinalIgnoreCase))
                    {
                        await CreateNewStockBatchFromImport(batch, ct);
                    }
                    else if (string.Equals(source, "RNR", StringComparison.OrdinalIgnoreCase))
                    {
                        await CreateRnrBatchFromImport(batch, ct);
                    }
                }
                
                return new ImportResultDto(batch.BatchId, batch.Added, batch.Duplicates, batch.Invalid, batch.Total);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Excel Import Error] {ex.InnerException?.Message ?? ex.Message}");
                Console.WriteLine($"[Excel Import Stack] {ex}");
                throw new InvalidOperationException($"Database save failed: {ex.InnerException?.Message ?? ex.Message}", ex);
            }
        }

        private async Task ProcessRow(string[] cols, Dictionary<string,int> header, string source, DeviceImportBatch batch, HashSet<string> seenInUpload, CancellationToken ct)
        {
            // Resolve key (Serial preferred, else IMEI)
            string? rawSerial = Get(cols, header, "Serial");
            string? rawImei = Get(cols, header, "IMEI");
            string? key = string.IsNullOrWhiteSpace(rawSerial) ? rawImei : rawSerial;
            key = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
            if (key == null) { batch.Invalid++; batch.Total++; return; }

            // In-upload dedupe
            if (!seenInUpload.Add(key)) { batch.Duplicates++; batch.Total++; return; }

            // DB exists check
            bool exists = await _db.Devices.AnyAsync(d => d.SerialNumber == key || d.IMEI == key, ct);
            if (exists) { batch.Duplicates++; batch.Total++; return; }

            // Single device per unique key (ignore Qty when key present)
            var dev = new Device
            {
                Id = Guid.NewGuid(),
                Source = source,
                Brand = Get(cols, header, "Brand"),
                Model = Get(cols, header, "Model"),
                BatchId = batch.BatchId
            };

            var emis = Get(cols, header, "EMIS");
            if (!string.IsNullOrWhiteSpace(emis))
            {
                var school = await _db.Schools.FirstOrDefaultAsync(s => s.EmisCode == emis.Trim(), ct);
                dev.SchoolId = school?.SchoolId;
            }

            if (IsImei(key)) dev.IMEI = key; else dev.SerialNumber = key;

            _db.Devices.Add(dev);

            batch.Added += 1;
            batch.Total += 1;
        }

        private static bool HasNewOrderHeader(Dictionary<string, int> header)
        {
            // Check if we have the minimum required columns for order-style import
            return header.ContainsKey("DeviceType") && header.ContainsKey("Quantity");
        }

        private async Task ProcessNewOrderRow(string[] cols, Dictionary<string, int> header, DeviceImportBatch batch)
        {
            // Extract order-style data
            var orderNumber = Get(cols, header, "OrderNumber");
            var brand = Get(cols, header, "Brand");
            var model = Get(cols, header, "Model");
            var deviceType = Get(cols, header, "DeviceType");
            var description = Get(cols, header, "Description");
            var quantityStr = Get(cols, header, "Quantity");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(deviceType))
            {
                batch.Invalid++;
                batch.Total++;
                return;
            }

            var quantity = ParseQty(quantityStr);
            if (quantity <= 0)
            {
                batch.Invalid++;
                batch.Total++;
                return;
            }

            // Check if this order line already exists in the database
            var existingCount = await _db.Devices
                .Where(d => d.OrderNumber == orderNumber && 
                           d.Brand == brand && 
                           d.Model == model && 
                           d.DeviceType == deviceType)
                .CountAsync();

            if (existingCount > 0)
            {
                // This order line already exists, mark as duplicates
                batch.Duplicates += quantity;
                batch.Total += quantity;
                return;
            }

            // For order-style imports, create multiple device entries based on quantity
            // Each device will have a generated serial placeholder
            for (int i = 0; i < quantity; i++)
            {
                // Generate a truly unique placeholder serial using GUID
                var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                var placeholderSerial = $"PENDING-{orderNumber}-{deviceType}-{uniqueId}";
                
                var dev = new Device
                {
                    Id = Guid.NewGuid(),
                    Source = "NEW",
                    Brand = brand,
                    Model = model,
                    DeviceType = deviceType,
                    Description = description,
                    OrderNumber = orderNumber,
                    BatchId = batch.BatchId,
                    // Generate a unique placeholder serial for tracking
                    SerialNumber = placeholderSerial
                };

                _db.Devices.Add(dev);
                batch.Added++;
                batch.Total++;
            }
            
            // Update batch with order number if present
            if (!string.IsNullOrWhiteSpace(orderNumber))
            {
                batch.OrderNumber = orderNumber;
            }

            await Task.CompletedTask;
        }

        private static string? Get(string[] arr, Dictionary<string, int> header, string name) =>
            header.TryGetValue(name, out var idx) && idx < arr.Length ? arr[idx] : null;
        private static bool IsImei(string s) => s.All(char.IsDigit) && s.Length >= 10;
        private static int ParseQty(string? s) => (int.TryParse(s, out var n) && n > 0) ? n : 1;
        
        /// <summary>
        /// Creates a NewStockBatch from a DeviceImportBatch for Phase 0 → Phase 1 integration
        /// Also creates OrderModelList entries for model-driven scanning
        /// </summary>
        private async Task CreateNewStockBatchFromImport(DeviceImportBatch importBatch, CancellationToken ct)
        {
            try
            {
                // Get all devices from this import batch
                var devices = await _db.Devices
                    .Where(d => d.BatchId == importBatch.BatchId)
                    .ToListAsync(ct);
                
                if (devices.Count == 0) return;
                
                // Group by OrderNumber, Brand, Model, DeviceType to create batch items
                var groups = devices
                    .GroupBy(d => new { d.OrderNumber, d.Brand, d.Model, d.DeviceType, d.Description })
                    .ToList();
                
                // Generate batch number
                var batchCount = await _db.NewStockBatches.CountAsync(ct);
                var batchNumber = $"NSB-{DateTime.UtcNow:yyyyMMdd}-{(batchCount + 1):D4}";
                
                // Create NewStockBatch
                var newStockBatch = new NewStockBatch
                {
                    BatchNumber = batchNumber,
                    SupplierName = importBatch.OrderNumber ?? "Imported Order",
                    InvoiceNumber = importBatch.OrderNumber,
                    TotalQuantityExpected = importBatch.Added,
                    TotalQuantityScanned = 0,
                    Status = NewStockBatchStatus.PendingScan,
                    CreatedBy = "orders.clerk@local", // From import
                    CreatedAt = DateTime.UtcNow,
                    Items = new List<NewStockBatchItem>()
                };
                
                // Create batch items from grouped devices
                foreach (var group in groups)
                {
                    newStockBatch.Items.Add(new NewStockBatchItem
                    {
                        Brand = group.Key.Brand,
                        Model = group.Key.Model,
                        DeviceType = group.Key.DeviceType,
                        Description = group.Key.Description,
                        QuantityExpected = group.Count(),
                        QuantityScanned = 0,
                        Zone = "New Stock"
                    });
                }
                
                _db.NewStockBatches.Add(newStockBatch);
                await _db.SaveChangesAsync(ct);
                
                // Create OrderModelList entries for model-driven scanning
                foreach (var group in groups)
                {
                    // Construct ModelName from Brand, Model, DeviceType
                    var modelName = $"{group.Key.Brand} {group.Key.Model} {group.Key.DeviceType}".Trim();
                    
                    var orderModel = new OrderModelList
                    {
                        OrderID = newStockBatch.BatchId,
                        ModelName = modelName,
                        ExpectedQty = group.Count(),
                        CountedQty = 0,
                        Status = "Open"
                    };
                    
                    _db.OrderModelLists.Add(orderModel);
                }
                
                await _db.SaveChangesAsync(ct);
                
                Console.WriteLine($"[NewStockBatch] Created batch {batchNumber} from import {importBatch.BatchId} with {groups.Count} models");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NewStockBatch Error] Failed to create NewStockBatch: {ex.Message}");
                // Don't throw - the import already succeeded
            }
        }
        
        /// <summary>
        /// Creates an RnrBatch from a DeviceImportBatch for R&R workflow (Phase 0 → Phase 1)
        /// </summary>
        private async Task CreateRnrBatchFromImport(DeviceImportBatch importBatch, CancellationToken ct)
        {
            try
            {
                // Get all devices from this import batch
                var devices = await _db.Devices
                    .Where(d => d.BatchId == importBatch.BatchId)
                    .ToListAsync(ct);
                
                if (devices.Count == 0) return;
                
                // Group by Brand, Model, DeviceType, Description to create batch items
                var groupedItems = devices
                    .GroupBy(d => new { d.Brand, d.Model, d.DeviceType, d.Description })
                    .Select(g => (
                        Brand: g.Key.Brand,
                        Model: g.Key.Model,
                        DeviceType: g.Key.DeviceType,
                        Description: g.Key.Description,
                        Quantity: g.Count()
                    ))
                    .ToList();
                
                // Extract collection slip number from order number or filename
                var collectionSlipNumber = importBatch.OrderNumber ?? 
                    Path.GetFileNameWithoutExtension(importBatch.FileName) ?? 
                    $"RNR-{DateTime.UtcNow:yyyyMMddHHmmss}";
                
                // Extract school info from first device (if available)
                var firstDevice = devices.First();
                long? schoolIdLong = firstDevice.SchoolId;
                string? schoolName = null;
                
                if (schoolIdLong.HasValue)
                {
                    var school = await _db.Schools.FirstOrDefaultAsync(s => s.SchoolId == schoolIdLong.Value, ct);
                    schoolName = school?.Name; // Use 'Name' property, not 'SchoolName'
                }
                
                // Call RnrBatchService to create the batch
                var batchId = await _rnrBatchService.CreateBatchFromImportAsync(
                    importBatch.BatchId,
                    collectionSlipNumber,
                    schoolIdLong,
                    schoolName,
                    groupedItems,
                    ct
                );
                
                Console.WriteLine($"[RnrBatch] Created R&R batch {batchId} from import {importBatch.BatchId} with collection slip {collectionSlipNumber}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RnrBatch Error] Failed to create RnrBatch: {ex.Message}");
                // Don't throw - the import already succeeded
            }
        }
    }
}