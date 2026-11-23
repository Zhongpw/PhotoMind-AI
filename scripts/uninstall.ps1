# PhotoMind AI uninstall script
# Removes program files and shortcuts; user data is only deleted after explicit confirmation.
# Never deletes the user's original photos (they always remain in place).
param(
    [string]$Destination = ""
)

$ErrorActionPreference = "Stop"

if (-not $Destination) {
    $Destination = Join-Path $env:LOCALAPPDATA "Programs\PhotoMind AI"
}
$dataDir = Join-Path $env:LOCALAPPDATA "PhotoMind AI"

# Remove shortcuts
$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$lnk = Join-Path $startMenu "PhotoMind AI.lnk"
if (Test-Path $lnk) { Remove-Item $lnk -Force }
$desktop = [Environment]::GetFolderPath("Desktop")
$desktopLnk = Join-Path $desktop "PhotoMind AI.lnk"
if (Test-Path $desktopLnk) { Remove-Item $desktopLnk -Force }

# Remove program files
if (Test-Path $Destination) {
    Remove-Item $Destination -Recurse -Force
}

# User data: delete only after explicit confirmation (database/config/cache; not original photos)
if (Test-Path $dataDir) {
    $ans = Read-Host "Delete local data (database/config/cache) at $dataDir ? This does NOT touch your original photos. Type YES to delete, anything else to keep"
    if ($ans -eq "YES") {
        Remove-Item $dataDir -Recurse -Force
        Write-Host "Local data removed."
    }
    else {
        Write-Host "Local data kept at: $dataDir"
    }
}

Write-Host "Uninstalled."
