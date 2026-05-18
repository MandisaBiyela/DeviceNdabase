# Reseed Imported Devices Instructions

## Overview
You've updated the CSV file `Schools_Populated_Siyanda_Fixed_Dates_Cleaned (1).csv` and need to update the database to match the new data.

## What Was Added
1. **API Endpoint**: `POST /api/superadmin/imported-devices/reseed`
   - Query parameter: `clearExisting` (true/false)
   - Requires SuperAdmin authentication
   - Clears existing data if requested and reloads from CSV

2. **PowerShell Script**: `Scripts/reseed-imported-devices.ps1`
   - Interactive script to call the reseed API
   - Handles login and session management
   - Displays progress and results

## Method 1: Using PowerShell Script (Recommended)

### Option A: Update Existing Records (Merge)
This will update existing records and add new ones without clearing the database:

```powershell
cd DeviceDesk
.\Scripts\reseed-imported-devices.ps1
```

### Option B: Clear and Reload (Fresh Start)
This will delete all existing imported devices and reload from the CSV:

```powershell
cd DeviceDesk
.\Scripts\reseed-imported-devices.ps1 -ClearExisting
```

### With Credentials (Non-Interactive)
```powershell
.\Scripts\reseed-imported-devices.ps1 -Username "superadmin@local" -Password "your-password" -ClearExisting
```

## Method 2: Using API Directly (cURL or Postman)

### Step 1: Login
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@local","password":"your-password"}' \
  -c cookies.txt
```

### Step 2: Call Reseed Endpoint

**Option A: Update/Merge (keeps existing data)**
```bash
curl -X POST "http://localhost:5000/api/superadmin/imported-devices/reseed?clearExisting=false" \
  -b cookies.txt
```

**Option B: Clear and Reload**
```bash
curl -X POST "http://localhost:5000/api/superadmin/imported-devices/reseed?clearExisting=true" \
  -b cookies.txt
```

## Method 3: Restart Application (Auto-Seed on Startup)

The application automatically seeds imported devices on startup if the table is empty. To trigger this:

1. **Clear the table** (using SQL or the API with `clearExisting=true`)
2. **Restart the application**

However, this only works if the table is empty. If you want to update existing data, use Method 1 or 2.

## Expected Response

Success response:
```json
{
  "success": true,
  "message": "Reseeding completed",
  "imported": 150,
  "updated": 3900,
  "skipped": 0,
  "totalInDatabase": 4050,
  "clearedExisting": false
}
```

## What Happens During Reseed?

### With `clearExisting=false` (Default - Merge Mode)
1. Reads the CSV file
2. For each row:
   - If serial number doesn't exist: **Creates new record**
   - If serial number exists: **Updates the record** with new data
3. Skips duplicates within the CSV file itself

### With `clearExisting=true` (Fresh Start Mode)
1. **Deletes all existing imported devices**
2. Reads the CSV file
3. Creates new records for all rows
4. Skips duplicates within the CSV file itself

## Data Mapping

The CSV columns are mapped as follows:
- Column 0: **EMIS Code** → Looks up SchoolId in Schools table
- Column 1: **District**
- Column 2: **CMC** (not stored)
- Column 3: **Circuit**
- Column 4: **School Name** (from CSV, fallback if EMIS not found)
- Column 5: **District** (duplicate, ignored)
- Column 6: **POD Number**
- Column 7: **Date Received** (parsed as local SAST time)
- Column 8: **Item Description**
- Column 9: **Serial Number** (unique key)

## Verification

After reseeding, verify the data:

### Check Total Count
```sql
SELECT COUNT(*) FROM SuperAdmin_ImportedDevices;
```

### Check Recent Imports
```sql
SELECT TOP 10 * 
FROM SuperAdmin_ImportedDevices 
ORDER BY CreatedAt DESC;
```

### Check Specific School
```sql
SELECT * 
FROM SuperAdmin_ImportedDevices 
WHERE EmisCode = '154512.0';
```

### Via API
Navigate to: `http://localhost:5000/superadmin/all-devices.html`
- Check the device count badge
- Search for specific serials or schools
- Verify dates and POD numbers

## Troubleshooting

### Issue: "CSV file not found"
**Solution**: Verify the CSV file exists at:
```
DeviceDesk/Data/Seeds/Schools_Populated_Siyanda_Fixed_Dates_Cleaned (1).csv
```

### Issue: "401 Unauthorized"
**Solution**: 
- Ensure you're logged in as SuperAdmin
- Check that your session/cookie is valid
- Re-login and try again

### Issue: "School not found for EMIS code"
**Solution**: 
- The Schools table might not have that EMIS code
- The device will still be imported but SchoolId will be NULL
- SchoolName will fallback to the value from CSV column 4

### Issue: Duplicates in CSV
**Solution**: 
- The seeder automatically skips duplicate serial numbers within the CSV
- Check the `skipped` count in the response
- Review the CSV for duplicate serials

## Quick Start (Most Common Use Case)

If you just want to update the database with your new CSV data:

```powershell
# 1. Navigate to the DeviceDesk folder
cd "C:\Users\Teacher\Downloads\DeviceDesk (yoo) (3)\DeviceDesk"

# 2. Run the script to reload everything fresh
.\Scripts\reseed-imported-devices.ps1 -ClearExisting

# 3. Enter your SuperAdmin credentials when prompted
```

That's it! The database will now match your updated CSV file.











