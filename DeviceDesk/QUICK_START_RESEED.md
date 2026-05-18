# Quick Start: Reseed Imported Devices

## What Changed?
You updated the CSV file: `Schools_Populated_Siyanda_Fixed_Dates_Cleaned (1).csv`

Now you need to update the database to match the new CSV data.

## 🚀 Fastest Way (2 Steps)

### Step 1: Start the Application
```powershell
cd "C:\Users\Teacher\Downloads\DeviceDesk (yoo) (3)\DeviceDesk"
dotnet run
```

Wait for the application to start. You'll see:
```
Now listening on: http://localhost:5000
```

### Step 2: Run the Reseed Script
Open a **new PowerShell window** and run:

```powershell
cd "C:\Users\Teacher\Downloads\DeviceDesk (yoo) (3)\DeviceDesk"
.\Scripts\reseed-imported-devices.ps1 -ClearExisting
```

When prompted:
- **Username**: `superadmin@local`
- **Password**: Your SuperAdmin password

## What Happens?
1. ✅ Clears all existing imported devices from database
2. ✅ Reads your updated CSV file (4049 rows)
3. ✅ Imports all devices with updated data
4. ✅ Links devices to schools using EMIS codes
5. ✅ Shows you the results

## Expected Output
```
========================================
✓ Reseed Completed Successfully!
========================================

Results:
  Imported: 4048
  Updated:  0
  Skipped:  1
  Total in Database: 4048
  Cleared Existing: True

Done!
```

## Verify It Worked

### Option 1: Check in Browser
1. Go to: http://localhost:5000/superadmin/all-devices.html
2. Login as SuperAdmin if needed
3. You should see your updated devices with new data

### Option 2: Check via SQL
```sql
SELECT COUNT(*) FROM SuperAdmin_ImportedDevices;
-- Should show ~4048 devices

SELECT TOP 10 * 
FROM SuperAdmin_ImportedDevices 
ORDER BY CreatedAt DESC;
-- Should show recently imported devices
```

## Two Reseed Options

### Option A: Clear and Reload (Recommended)
```powershell
.\Scripts\reseed-imported-devices.ps1 -ClearExisting
```
- **Deletes** all existing imported devices
- **Imports** all devices fresh from CSV
- **Use when**: You want a clean slate matching the CSV exactly

### Option B: Update/Merge (Keep existing + add new)
```powershell
.\Scripts\reseed-imported-devices.ps1
```
- **Keeps** existing devices
- **Updates** devices that already exist (by serial number)
- **Adds** new devices from CSV
- **Use when**: You want to preserve existing records and just update them

## Troubleshooting

### "Application not running"
Make sure `dotnet run` is running in another terminal window.

### "Login failed"
Check your SuperAdmin credentials. Default is usually:
- Email: `superadmin@local`
- Password: Check your seeded users or Program.cs

### "CSV file not found"
Verify the file exists at:
```
C:\Users\Teacher\Downloads\DeviceDesk (yoo) (3)\DeviceDesk\Data\Seeds\Schools_Populated_Siyanda_Fixed_Dates_Cleaned (1).csv
```

### Script won't run
If PowerShell blocks the script:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## That's It! 🎉
Your database will now match your updated CSV file.











