# Get Azure SQL access token
Write-Host "Getting Azure SQL access token..." -ForegroundColor Cyan
$token = az account get-access-token --resource https://database.windows.net --query accessToken -o tsv

if (-not $token) {
    Write-Host "ERROR: Failed to get access token. Make sure you're logged in with: az login" -ForegroundColor Red
    exit 1
}

Write-Host "Token acquired successfully (length: $($token.Length) chars)" -ForegroundColor Green
Write-Host ""

# Run ingestion with token
Write-Host "Running data ingestion to Azure SQL..." -ForegroundColor Cyan
Write-Host ""

cd YSS.Verification
dotnet run -- $token

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Data ingestion completed successfully!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "❌ Data ingestion failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit 1
}
