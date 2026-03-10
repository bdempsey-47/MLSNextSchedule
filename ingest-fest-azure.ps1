# ingest-fest-azure.ps1
# Ingest FEST event matches from Modular11's authenticated events endpoint.
#
# Usage:
#   .\ingest-fest-azure.ps1
#   (prompts for FEST URL and session token)
#
# Or non-interactive:
#   .\ingest-fest-azure.ps1 -FestUrl "<url>" -SessionToken "<token>"
#
# How to get the required values:
#   1. Open the FEST schedule page on Modular11 in Chrome/Edge
#   2. Open DevTools (F12) → Network tab → filter by "get_matches"
#   3. Click any get_matches XHR request → copy the full Request URL
#   4. Copy the "_token" value from the Request Headers (same as x-csrf-token)

param(
    [string]$FestUrl = "",
    [string]$SessionToken = ""
)

if (-not $FestUrl) {
    $FestUrl = Read-Host "Paste the FEST get_matches URL (open_page=0)"
}
if (-not $SessionToken) {
    $SessionToken = Read-Host "Paste the Modular11 session token (_token value)"
}

Write-Host "Getting Azure SQL access token..."
$AzureToken = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv

if (-not $AzureToken) {
    Write-Host "ERROR: Could not get Azure access token. Run 'az login' first." -ForegroundColor Red
    exit 1
}

Write-Host "Starting FEST ingestion..." -ForegroundColor Cyan
dotnet run --project YSS.Verification -- --fest $FestUrl $SessionToken $AzureToken
