using ClosedXML.Excel;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DeviceDesk.Modules.Phase1.Services
{
    public class SpreadsheetParserService
    {
        private readonly ILogger<SpreadsheetParserService> _logger;

        public SpreadsheetParserService(ILogger<SpreadsheetParserService> logger)
        {
            _logger = logger;
        }
        public class DeviceRow
        {
            public string? Serial { get; set; }
            public string? Brand { get; set; }
            public string? Model { get; set; }
            public string? Description { get; set; }
            public string? OrderNumber { get; set; }
            public string? DeviceType { get; set; }
            public int? Quantity { get; set; }
            public int RowNumber { get; set; }
        }

        public class ParseResult
        {
            public List<DeviceRow> Devices { get; set; } = new();
            public List<string> Errors { get; set; } = new();
            public int TotalRows { get; set; }
            public int ValidRows { get; set; }
        }

        /// <summary>
        /// Parse Excel or CSV file to extract device information
        /// Expected columns: Serial, Brand (optional), Model (optional), Description (optional)
        /// </summary>
        public async Task<ParseResult> ParseSpreadsheetAsync(Stream fileStream, string fileName, CancellationToken ct = default)
        {
            var result = new ParseResult();

            try
            {
                if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    result = await ParseCsvAsync(fileStream, ct);
                }
                else if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || 
                         fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    result = ParseExcel(fileStream);
                }
                else
                {
                    result.Errors.Add("Unsupported file format. Please upload .xlsx, .xls, or .csv files.");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error parsing file: {ex.Message}");
            }

            return result;
        }

        private ParseResult ParseExcel(Stream fileStream)
        {
            var result = new ParseResult();
            _logger.LogInformation("Starting Excel file parsing...");

            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet(1); // First sheet

            // Find header row (look for "Serial" column)
            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                _logger.LogWarning("Empty spreadsheet - no rows found");
                result.Errors.Add("Empty spreadsheet. No data found.");
                return result;
            }

            // Map column indices
            int serialCol = -1, brandCol = -1, modelCol = -1, descCol = -1, orderCol = -1, typeCol = -1, qtyCol = -1;
            
            foreach (var cell in headerRow.CellsUsed())
            {
                var header = cell.GetString().Trim().ToLower().Replace(" ", "");
                if (header.Contains("serial")) serialCol = cell.Address.ColumnNumber;
                else if (header.Contains("brand")) brandCol = cell.Address.ColumnNumber;
                else if (header.Contains("model")) modelCol = cell.Address.ColumnNumber;
                else if (header.Contains("description") || header.Contains("desc")) descCol = cell.Address.ColumnNumber;
                else if (header.Contains("order")) orderCol = cell.Address.ColumnNumber;
                else if (header.Contains("type") || header.Contains("devicetype")) typeCol = cell.Address.ColumnNumber;
                else if (header.Contains("quantity") || header.Contains("qty")) qtyCol = cell.Address.ColumnNumber;
            }

            // For new stock batches, Serial is optional if we have DeviceType and Quantity
            bool isNewStockBatch = (typeCol > 0 && qtyCol > 0);
            
            _logger.LogInformation("Column mapping: Serial={Serial}, Brand={Brand}, Model={Model}, DeviceType={DeviceType}, Quantity={Quantity}, Order={Order}", 
                serialCol, brandCol, modelCol, typeCol, qtyCol, orderCol);
            _logger.LogInformation("Detected batch type: {BatchType}", isNewStockBatch ? "New Stock" : "Traditional RnR");
            
            if (serialCol == -1 && !isNewStockBatch)
            {
                _logger.LogError("Missing required columns - Serial not found and not a New Stock batch");
                result.Errors.Add("Required column 'Serial' not found. For new stock batches, include 'DeviceType' and 'Quantity' columns.");
                return result;
            }

            // Parse data rows
            var dataRows = worksheet.RowsUsed().Skip(1); // Skip header
            result.TotalRows = dataRows.Count();
            _logger.LogInformation("Found {TotalRows} data rows to process", result.TotalRows);

            foreach (var row in dataRows)
            {
                var rowNum = row.RowNumber();
                var serial = serialCol > 0 ? row.Cell(serialCol).GetString().Trim() : string.Empty;
                var deviceType = typeCol > 0 ? row.Cell(typeCol).GetString().Trim() : null;
                var quantityStr = qtyCol > 0 ? row.Cell(qtyCol).GetString().Trim() : null;
                int? quantity = null;

                if (!string.IsNullOrWhiteSpace(quantityStr) && int.TryParse(quantityStr, out int qty))
                {
                    quantity = qty;
                }

                var brand = brandCol > 0 ? row.Cell(brandCol).GetString().Trim() : null;
                var model = modelCol > 0 ? row.Cell(modelCol).GetString().Trim() : null;
                var description = descCol > 0 ? row.Cell(descCol).GetString().Trim() : null;
                var orderNumber = orderCol > 0 ? row.Cell(orderCol).GetString().Trim() : null;

                // For new stock batches, validate that row has at least some meaningful data
                if (isNewStockBatch)
                {
                    // Check if row has any value at all
                    var hasAnyValue = 
                        !string.IsNullOrWhiteSpace(brand) ||
                        !string.IsNullOrWhiteSpace(model) ||
                        !string.IsNullOrWhiteSpace(description) ||
                        !string.IsNullOrWhiteSpace(orderNumber);

                    if (!hasAnyValue)
                    {
                        // Skip completely empty row
                        continue;
                    }

                    // Validate required fields for new stock
                    if (string.IsNullOrWhiteSpace(deviceType))
                    {
                        result.Errors.Add($"Row {rowNum}: DeviceType is required for new stock batches.");
                        continue;
                    }
                    if (!quantity.HasValue || quantity.Value <= 0)
                    {
                        result.Errors.Add($"Row {rowNum}: Quantity must be greater than 0.");
                        continue;
                    }
                }
                else if (string.IsNullOrWhiteSpace(serial))
                {
                    // Traditional batch requires serial
                    result.Errors.Add($"Row {rowNum}: Serial number is required.");
                    continue;
                }

                var device = new DeviceRow
                {
                    Serial = string.IsNullOrWhiteSpace(serial) ? null : serial,
                    Brand = brand,
                    Model = model,
                    Description = description,
                    OrderNumber = orderNumber,
                    DeviceType = deviceType,
                    Quantity = quantity,
                    RowNumber = rowNum
                };

                result.Devices.Add(device);
                result.ValidRows++;
            }

            _logger.LogInformation("Excel parsing complete: {ValidRows} valid rows out of {TotalRows} total rows, {ErrorCount} errors", 
                result.ValidRows, result.TotalRows, result.Errors.Count);
            
            if (result.ValidRows == 0 && result.TotalRows > 0)
            {
                _logger.LogWarning("No valid items found in CSV. All {TotalRows} rows failed validation. Errors: {Errors}", 
                    result.TotalRows, string.Join("; ", result.Errors));
            }

            return result;
        }

        private async Task<ParseResult> ParseCsvAsync(Stream fileStream, CancellationToken ct)
        {
            var result = new ParseResult();
            _logger.LogInformation("Starting CSV file parsing...");

            using var reader = new StreamReader(fileStream, Encoding.UTF8);
            
            // Read header
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                _logger.LogWarning("Empty CSV file - no header found");
                result.Errors.Add("Empty CSV file. No data found.");
                return result;
            }
            
            _logger.LogInformation("CSV Header: {Header}", headerLine);

            var headers = headerLine.Split(',').Select(h => h.Trim().Trim('"')).ToArray();
            
            // Map column indices
            int serialCol = -1, brandCol = -1, modelCol = -1, descCol = -1, orderCol = -1, typeCol = -1, qtyCol = -1;
            
            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i].ToLower().Replace(" ", "");
                if (header.Contains("serial")) serialCol = i;
                else if (header.Contains("brand")) brandCol = i;
                else if (header.Contains("model")) modelCol = i;
                else if (header.Contains("description") || header.Contains("desc")) descCol = i;
                else if (header.Contains("order")) orderCol = i;
                else if (header.Contains("type") || header.Contains("devicetype")) typeCol = i;
                else if (header.Contains("quantity") || header.Contains("qty")) qtyCol = i;
            }

            // For new stock batches, Serial is optional if we have DeviceType and Quantity
            bool isNewStockBatch = (typeCol >= 0 && qtyCol >= 0);
            
            _logger.LogInformation("CSV Column mapping: Serial={Serial}, Brand={Brand}, Model={Model}, DeviceType={DeviceType}, Quantity={Quantity}, Order={Order}", 
                serialCol, brandCol, modelCol, typeCol, qtyCol, orderCol);
            _logger.LogInformation("Detected batch type: {BatchType}", isNewStockBatch ? "New Stock" : "Traditional RnR");
            
            if (serialCol == -1 && !isNewStockBatch)
            {
                _logger.LogError("Missing required columns - Serial not found and not a New Stock batch");
                result.Errors.Add("Required column 'Serial' not found. For new stock batches, include 'DeviceType' and 'Quantity' columns.");
                return result;
            }

            // Parse data rows
            int rowNum = 2; // Start from row 2 (after header)
            string? line;
            
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = line.Split(',').Select(v => v.Trim().Trim('"')).ToArray();
                
                var serial = serialCol >= 0 && values.Length > serialCol ? values[serialCol].Trim() : string.Empty;
                var deviceType = typeCol >= 0 && values.Length > typeCol ? values[typeCol].Trim() : null;
                var quantityStr = qtyCol >= 0 && values.Length > qtyCol ? values[qtyCol].Trim() : null;
                var brand = brandCol >= 0 && values.Length > brandCol ? values[brandCol].Trim() : null;
                var model = modelCol >= 0 && values.Length > modelCol ? values[modelCol].Trim() : null;
                var description = descCol >= 0 && values.Length > descCol ? values[descCol].Trim() : null;
                var orderNumber = orderCol >= 0 && values.Length > orderCol ? values[orderCol].Trim() : null;
                int? quantity = null;

                if (!string.IsNullOrWhiteSpace(quantityStr) && int.TryParse(quantityStr, out int qty))
                {
                    quantity = qty;
                }

                // For new stock batches, validate that row has at least some meaningful data
                if (isNewStockBatch)
                {
                    // Check if row has any value at all
                    var hasAnyValue = 
                        !string.IsNullOrWhiteSpace(brand) ||
                        !string.IsNullOrWhiteSpace(model) ||
                        !string.IsNullOrWhiteSpace(description) ||
                        !string.IsNullOrWhiteSpace(orderNumber);

                    if (!hasAnyValue)
                    {
                        // Skip completely empty row
                        rowNum++;
                        continue;
                    }

                    // Validate required fields for new stock
                    if (string.IsNullOrWhiteSpace(deviceType))
                    {
                        result.Errors.Add($"Row {rowNum}: DeviceType is required for new stock batches.");
                        rowNum++;
                        continue;
                    }
                    if (!quantity.HasValue || quantity.Value <= 0)
                    {
                        result.Errors.Add($"Row {rowNum}: Quantity must be greater than 0.");
                        rowNum++;
                        continue;
                    }
                }
                else if (string.IsNullOrWhiteSpace(serial))
                {
                    // Traditional batch requires serial
                    result.Errors.Add($"Row {rowNum}: Serial number is required.");
                    rowNum++;
                    continue;
                }

                var device = new DeviceRow
                {
                    Serial = string.IsNullOrWhiteSpace(serial) ? null : serial,
                    Brand = brand,
                    Model = model,
                    Description = description,
                    OrderNumber = orderNumber,
                    DeviceType = deviceType,
                    Quantity = quantity,
                    RowNumber = rowNum
                };

                result.Devices.Add(device);
                result.ValidRows++;
                result.TotalRows++;
                rowNum++;
            }

            _logger.LogInformation("CSV parsing complete: {ValidRows} valid rows out of {TotalRows} total rows, {ErrorCount} errors", 
                result.ValidRows, result.TotalRows, result.Errors.Count);
            
            if (result.ValidRows == 0 && result.TotalRows > 0)
            {
                _logger.LogWarning("No valid items found in CSV. All {TotalRows} rows failed validation. Errors: {Errors}", 
                    result.TotalRows, string.Join("; ", result.Errors));
            }

            return result;
        }
    }
}
