param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("emulator", "device")]
    [string]$Profile
)

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$appFolder = Join-Path $repoRoot "FOOD_MAP"
$sourcePath = Join-Path $appFolder (".env." + $Profile)
$targetPath = Join-Path $appFolder ".env"

if (-not (Test-Path $sourcePath)) {
    throw "Profile file not found: $sourcePath"
}

Copy-Item -Path $sourcePath -Destination $targetPath -Force

Write-Host "Switched .env profile to '$Profile'"
Write-Host "Source: $sourcePath"
Write-Host "Target: $targetPath"
