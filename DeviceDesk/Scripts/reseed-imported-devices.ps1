# Reseed Imported Devices Script
# This script calls the SuperAdmin API to reseed imported devices from the CSV file

param(
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$ClearExisting = $false,
    [string]$Username = "",
    [string]$Password = ""
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Reseed Imported Devices" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if credentials are provided
if ([string]::IsNullOrEmpty($Username) -or [string]::IsNullOrEmpty($Password)) {
    Write-Host "Please provide SuperAdmin credentials:" -ForegroundColor Yellow
    $Username = Read-Host "Username (email)"
    $SecurePassword = Read-Host "Password" -AsSecureString
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword)
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
}

# Create a session to preserve cookies
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

Write-Host "Step 1: Logging in as SuperAdmin..." -ForegroundColor Yellow

# Login
$loginBody = @{
    email = $Username
    password = $Password
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" `
        -Method Post `
        -Body $loginBody `
        -ContentType "application/json" `
        -WebSession $session `
        -ErrorAction Stop

    Write-Host "✓ Login successful" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "✗ Login failed: $_" -ForegroundColor Red
    exit 1
}

# Call reseed endpoint
Write-Host "Step 2: Reseeding imported devices..." -ForegroundColor Yellow
Write-Host "  Clear existing: $ClearExisting" -ForegroundColor Gray

try {
    $reseedUrl = "$BaseUrl/api/superadmin/imported-devices/reseed?clearExisting=$($ClearExisting.ToString().ToLower())"
    
    $reseedResponse = Invoke-RestMethod -Uri $reseedUrl `
        -Method Post `
        -WebSession $session `
        -ErrorAction Stop

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "✓ Reseed Completed Successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Results:" -ForegroundColor Cyan
    Write-Host "  Imported: $($reseedResponse.imported)" -ForegroundColor White
    Write-Host "  Updated:  $($reseedResponse.updated)" -ForegroundColor White
    Write-Host "  Skipped:  $($reseedResponse.skipped)" -ForegroundColor White
    Write-Host "  Total in Database: $($reseedResponse.totalInDatabase)" -ForegroundColor White
    Write-Host "  Cleared Existing: $($reseedResponse.clearedExisting)" -ForegroundColor White
    Write-Host ""
    Write-Host "Message: $($reseedResponse.message)" -ForegroundColor Gray
}
catch {
    Write-Host ""
    Write-Host "✗ Reseed failed: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Response Body:" -ForegroundColor Yellow
    Write-Host $_.ErrorDetails.Message -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green











