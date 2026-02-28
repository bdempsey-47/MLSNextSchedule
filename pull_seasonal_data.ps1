# Pull seasonal match data from Modular11
# Downloads data for:
# - Fall 2025 (7/1/2025 - 12/31/2025) for both Homegrown and Academy
# - Spring 2026 (1/1/2026 - 6/30/2026) for both Homegrown and Academy

$seedataIsFatal = $true
$VerificationPath = "C:\Projects\MLSNextSchedule\MLSNext.Verification"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "   MLS Next - Seasonal Data Pull from Modular11" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# Function to update local.settings.json
function Update-LocalSettings {
    param(
        [string]$TournamentId,
        [string]$StartDate,
        [string]$EndDate,
        [string]$Season
    )
    
    Write-Host "`n>>> Pulling $Season data..." -ForegroundColor Yellow
    Write-Host "  Tournament ID: $TournamentId ($(if($TournamentId -eq '12') { 'Homegrown' } else { 'Academy' }))"
    Write-Host "  Date Range: $StartDate to $EndDate"
    
    $settingsPath = Join-Path $VerificationPath "local.settings.json"
    $settings = Get-Content $settingsPath | ConvertFrom-Json
    
    $settings.Modular11.TournamentId = $TournamentId
    $settings.Modular11.StartDate = $StartDate
    $settings.Modular11.EndDate = $EndDate
    
    $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath
    
    # Run the verification program
    Push-Location $VerificationPath
    try {
        dotnet run 2>&1 | Select-Object -Last 30
    } finally {
        Pop-Location
    }
    
    Write-Host "  [OK] Completed" -ForegroundColor Green
}

# Pull Fall 2025 Homegrown (Tournament 12)
Update-LocalSettings -TournamentId "12" `
    -StartDate "2025-07-01 00:00:01" `
    -EndDate "2025-12-31 23:59:59" `
    -Season "Fall 2025 - Homegrown"

# Pull Fall 2025 Academy (Tournament 35)
Update-LocalSettings -TournamentId "35" `
    -StartDate "2025-07-01 00:00:01" `
    -EndDate "2025-12-31 23:59:59" `
    -Season "Fall 2025 - Academy"

# Pull Spring 2026 Homegrown (Tournament 12)
Update-LocalSettings -TournamentId "12" `
    -StartDate "2026-01-01 00:00:01" `
    -EndDate "2026-06-30 23:59:59" `
    -Season "Spring 2026 - Homegrown"

# Pull Spring 2026 Academy (Tournament 35)
Update-LocalSettings -TournamentId "35" `
    -StartDate "2026-01-01 00:00:01" `
    -EndDate "2026-06-30 23:59:59" `
    -Season "Spring 2026 - Academy"

Write-Host "`n========================================================" -ForegroundColor Green
Write-Host "   SUCCESS: All seasonal data pulled and upserted!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
