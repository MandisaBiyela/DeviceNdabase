# Script to generate storage templates for all schools
# Run this after applying the database migration

$apiUrl = "http://localhost:5000/api/phase2/storage-templates/generate-all"

Write-Host "Generating storage templates for all schools..." -ForegroundColor Cyan
Write-Host "API Endpoint: $apiUrl" -ForegroundColor Gray

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method POST -ContentType "application/json"
    
    if ($response.success) {
        Write-Host "`n✓ Success!" -ForegroundColor Green
        Write-Host "  Templates created: $($response.created)" -ForegroundColor Green
        Write-Host "  Message: $($response.message)" -ForegroundColor Green
    } else {
        Write-Host "`n✗ Failed" -ForegroundColor Red
        Write-Host "  Error: $($response.message)" -ForegroundColor Red
    }
} catch {
    Write-Host "`n✗ Error calling API" -ForegroundColor Red
    Write-Host "  Status: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    Write-Host "  Message: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nMake sure:" -ForegroundColor Yellow
    Write-Host "  1. The application is running" -ForegroundColor Yellow
    Write-Host "  2. You are logged in as IctAllocator or Admin" -ForegroundColor Yellow
    Write-Host "  3. The database migration has been applied" -ForegroundColor Yellow
}

