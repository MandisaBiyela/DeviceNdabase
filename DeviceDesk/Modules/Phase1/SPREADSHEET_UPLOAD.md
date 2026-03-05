# Phase 1 - Spreadsheet Upload Feature

## Overview
The spreadsheet upload feature allows users to bulk import device serial numbers along with invoice/collection slip documents during the New Stock receiving process.

---

## Features

### 1. **Spreadsheet Upload**
- Upload Excel (.xlsx, .xls) or CSV files
- Bulk import device serial numbers with optional metadata
- Automatic parsing and validation
- Error reporting for invalid rows

### 2. **Multiple Document Types**
- **Invoice** - Supplier invoice
- **Collection Slip** - School collection document
- **Purchase Order** - PO document
- **Delivery Note** - Delivery documentation
- **Packing List** - Item packing details
- **Warranty** - Warranty certificates
- **Certificate** - Other certificates
- **Other** - Miscellaneous documents

### 3. **Document Management**
- Upload multiple documents per batch
- View uploaded documents
- Download documents
- Delete documents

---

## Spreadsheet Format

### Required Columns
- **Serial** (required) - Device serial number

### Optional Columns
- **Brand** - Device brand/manufacturer
- **Model** - Device model number
- **Description** - Additional description

### Example CSV Format
```csv
Serial,Brand,Model,Description
SN-001,HP,EliteBook 840,14-inch laptop
SN-002,Dell,Latitude 5420,Business laptop
SN-003,Lenovo,ThinkPad T14,Professional laptop
```

### Example Excel Format
| Serial | Brand  | Model         | Description        |
|--------|--------|---------------|--------------------|
| SN-001 | HP     | EliteBook 840 | 14-inch laptop     |
| SN-002 | Dell   | Latitude 5420 | Business laptop    |
| SN-003 | Lenovo | ThinkPad T14  | Professional laptop|

---

## Workflow

### Step 1: Create Receiving Batch
1. Navigate to **Phase 1 Dashboard**
2. Click **New Batch** → **New Stock**
3. Select an Order/Invoice
4. Fill in additional information
5. Click **Create Receiving Batch**

### Step 2: Upload Documents & Spreadsheet
1. You'll be redirected to the **Upload Documents** page
2. **Upload Spreadsheet (Optional)**:
   - Click **Download Template** to get a sample CSV file
   - Fill in your device data
   - Click **Upload Spreadsheet**
   - Review parsed results
   - View device list to verify
3. **Upload Documents**:
   - Drag & drop or browse for files
   - Select document type (Invoice, Collection Slip, etc.)
   - Upload multiple documents as needed
4. Click **Continue to Verification**

### Step 3: Device Scanning
- Proceed with manual scanning or use spreadsheet data
- Verify device counts
- Complete reconciliation

---

## API Endpoints

### Upload Spreadsheet
```http
POST /api/phase1/receiving/batches/{batchId}/spreadsheet
Content-Type: multipart/form-data

file: [spreadsheet file]
```

**Response:**
```json
{
  "message": "Spreadsheet parsed successfully",
  "documentId": 123,
  "fileName": "devices.xlsx",
  "totalRows": 100,
  "validRows": 98,
  "devices": [
    {
      "serial": "SN-001",
      "brand": "HP",
      "model": "EliteBook 840",
      "description": "14-inch laptop",
      "rowNumber": 2
    }
  ],
  "errors": [
    "Row 5: Serial number is required.",
    "Row 12: Serial number is required."
  ]
}
```

### Upload Document
```http
POST /api/phase1/receiving/batches/{batchId}/documents?docType=INVOICE
Content-Type: multipart/form-data

file: [document file]
```

**Response:**
```json
{
  "documentId": 124,
  "fileName": "invoice-2025-001.pdf",
  "docType": "INVOICE",
  "message": "Document uploaded successfully"
}
```

### Get Batch Documents
```http
GET /api/phase1/receiving/batches/{batchId}/documents
```

**Response:**
```json
[
  {
    "documentId": 123,
    "fileName": "devices.xlsx",
    "docType": "SPREADSHEET",
    "fileSizeBytes": 15360,
    "uploadedAt": "2025-01-15T10:30:00Z"
  },
  {
    "documentId": 124,
    "fileName": "invoice-2025-001.pdf",
    "docType": "INVOICE",
    "fileSizeBytes": 245760,
    "uploadedAt": "2025-01-15T10:31:00Z"
  }
]
```

### Download Document
```http
GET /api/phase1/receiving/documents/{documentId}/download
```

### Delete Document
```http
DELETE /api/phase1/receiving/documents/{documentId}
```

---

## Error Handling

### Spreadsheet Parsing Errors
- **Empty file**: "Empty spreadsheet. No data found."
- **Missing Serial column**: "Required column 'Serial' not found in spreadsheet header."
- **Empty serial**: "Row X: Serial number is required."
- **Invalid format**: "Unsupported file format. Please upload .xlsx, .xls, or .csv files."

### Upload Errors
- **No file**: "No file uploaded. Please select a file."
- **Invalid extension**: "Invalid file format. Please upload .xlsx, .xls, or .csv files."
- **Batch not found**: "Receiving batch not found."

---

## Technical Implementation

### Backend Components

#### SpreadsheetParserService
- Parses Excel and CSV files
- Validates data structure
- Returns device list with errors

#### ReceivingController
- `/batches/{batchId}/spreadsheet` - Upload and parse spreadsheet
- `/batches/{batchId}/documents` - Upload documents
- `/documents/{documentId}/download` - Download document
- `/documents/{documentId}` - Delete document

### Frontend Components

#### receiving-upload.html
- Spreadsheet upload section with template download
- Document upload zone with drag & drop
- Document type selector
- Uploaded files list

#### receiving-upload.js
- Handles spreadsheet upload and parsing
- Displays parsed results
- Shows device list in modal
- Manages document uploads

---

## Dependencies

### NuGet Packages
- **ClosedXML** - Excel file parsing (already included)
- **Microsoft.AspNetCore.Http** - File upload handling

### Frontend Libraries
- **Bootstrap 5** - UI framework
- **Bootstrap Icons** - Icons

---

## Benefits

1. **Efficiency**: Bulk import hundreds of devices in seconds
2. **Accuracy**: Reduce manual entry errors
3. **Flexibility**: Support both Excel and CSV formats
4. **Validation**: Immediate feedback on data quality
5. **Documentation**: Keep invoice and spreadsheet together
6. **Audit Trail**: All documents stored with batch

---

## Best Practices

1. **Use Template**: Download and use the provided CSV template
2. **Validate Data**: Review parsed results before proceeding
3. **Upload Documents**: Always upload invoice/collection slip with spreadsheet
4. **Check Errors**: Address any parsing errors before continuing
5. **Backup**: Keep original spreadsheet as backup

---

## Future Enhancements

- [ ] Support for additional columns (Asset Tag, Location, etc.)
- [ ] Duplicate serial number detection
- [ ] Batch edit capabilities
- [ ] Export parsed data back to spreadsheet
- [ ] Integration with barcode scanners
- [ ] Real-time validation against inventory

---

## Support

For issues or questions:
1. Check the error messages in the UI
2. Review the spreadsheet format requirements
3. Download and use the template
4. Contact system administrator
