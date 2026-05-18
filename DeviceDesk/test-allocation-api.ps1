# Device Allocation API Test Script
# PowerShell script to test the device allocation endpoints

$baseUrl = "http://localhost:5000"  # Adjust port if needed
$apiBase = "$baseUrl/api/phase1"

Write-Host "=== Device Allocation API Test ===" -ForegroundColor Cyan
Write-Host ""

# Test variables - UPDATE THESE WITH REAL VALUES
$batchId = "00000000-0000-0000-0000-000000000000"  # Replace with actual batch ID
$deviceId = "00000000-0000-0000-0000-000000000000"  # Replace with actual device ID

Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Base URL: $baseUrl"
Write-Host "  Batch ID: $batchId"
Write-Host "  Device ID: $deviceId"
Write-Host ""

# Function to make API calls
function Invoke-ApiTest {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body = $null
    )
    
    try {
        $params = @{
            Method = $Method
            Uri = $Url
            Headers = @{
                "Content-Type" = "application/json"
            }
            UseBasicParsing = $true
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        
        $response = Invoke-RestMethod @params
        return @{
            Success = $true
            Data = $response
        }
    }
    catch {
        return @{
            Success = $false
            Error = $_.Exception.Message
            StatusCode = $_.Exception.Response.StatusCode.value__
        }
    }
}

# Test 1: Allocate Single Device to Student
Write-Host "Test 1: Allocate Device to Student" -ForegroundColor Green
$studentAllocation = @{
    deviceId = $deviceId
    allocationType = 1
    studentName = "Test Student"
    studentIdNumber = "20240001"
}

$result = Invoke-ApiTest -Method "POST" -Url "$apiBase/rnr/batches/$batchId/allocate-device" -Body $studentAllocation

if ($result.Success) {
    Write-Host "  ✅ SUCCESS: $($result.Data.message)" -ForegroundColor Green
} else {
    Write-Host "  ❌ FAILED: $($result.Error)" -ForegroundColor Red
}
Write-Host ""

# Test 2: Switch to Teacher Allocation
Write-Host "Test 2: Switch Device to Teacher" -ForegroundColor Green
$teacherAllocation = @{
    deviceId = $deviceId
    allocationType = 2
    teacherName = "Test Teacher"
    teacherPersalNumber = "1234567"
}

$result = Invoke-ApiTest -Method "POST" -Url "$apiBase/rnr/batches/$batchId/allocate-device" -Body $teacherAllocation

if ($result.Success) {
    Write-Host "  ✅ SUCCESS: $($result.Data.message)" -ForegroundColor Green
} else {
    Write-Host "  ❌ FAILED: $($result.Error)" -ForegroundColor Red
}
Write-Host ""

# Test 3: Get Allocations
Write-Host "Test 3: Retrieve All Allocations" -ForegroundColor Green
$result = Invoke-ApiTest -Method "GET" -Url "$apiBase/rnr/batches/$batchId/allocations"

if ($result.Success) {
    Write-Host "  ✅ SUCCESS: Retrieved allocations" -ForegroundColor Green
    $result.Data | ForEach-Object {
        $type = switch ($_.allocationType) {
            0 { "None" }
            1 { "Student" }
            2 { "Teacher" }
        }
        Write-Host "    Device: $($_.deviceId)" -ForegroundColor Cyan
        Write-Host "    Type: $type" -ForegroundColor Cyan
        if ($_.allocationType -eq 1) {
            Write-Host "    Student: $($_.studentName) (ID: $($_.studentIdNumber))" -ForegroundColor Cyan
        }
        elseif ($_.allocationType -eq 2) {
            Write-Host "    Teacher: $($_.teacherName) (Persal: $($_.teacherPersalNumber))" -ForegroundColor Cyan
        }
    }
} else {
    Write-Host "  ❌ FAILED: $($result.Error)" -ForegroundColor Red
}
Write-Host ""

# Test 4: Bulk Allocation
Write-Host "Test 4: Bulk Allocation" -ForegroundColor Green
$bulkRequest = @{
    batchId = $batchId
    allocations = @(
        @{
            deviceId = $deviceId
            allocationType = 1
            studentName = "Bulk Student 1"
            studentIdNumber = "20240010"
        }
    )
}

$result = Invoke-ApiTest -Method "POST" -Url "$apiBase/rnr/batches/$batchId/allocate-bulk" -Body $bulkRequest

if ($result.Success) {
    Write-Host "  ✅ SUCCESS: $($result.Data.message)" -ForegroundColor Green
} else {
    Write-Host "  ❌ FAILED: $($result.Error)" -ForegroundColor Red
}
Write-Host ""

# Test 5: New Stock Endpoints
Write-Host "Test 5: New Stock Allocation (same pattern)" -ForegroundColor Green
$result = Invoke-ApiTest -Method "POST" -Url "$apiBase/newstock/batches/$batchId/allocate-device" -Body $studentAllocation

if ($result.Success) {
    Write-Host "  ✅ SUCCESS: New Stock endpoint works" -ForegroundColor Green
} else {
    Write-Host "  ❌ FAILED: $($result.Error)" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "All API endpoints have been tested." -ForegroundColor Yellow
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Update `$batchId` and `$deviceId` with real values from your database"
Write-Host "2. Verify database records in Devices table (check AllocationType, StudentName, etc.)"
Write-Host "3. Test UI integration on /phase1/rnr-verification.html"
Write-Host ""

