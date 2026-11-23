# PhotoMind AI install script (portable self-contained distribution)
# Copies the published Release app to a target folder and creates a Start Menu shortcut.
# Does NOT create or modify any user photos; user data lives in a separate %LOCALAPPDATA%\PhotoMind AI folder.
param(
    [string]$Source = "",
    [string]$Destination = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if (-not $Source) {
    $Source = Join-Path $root "src\PhotoMind.App\bin\Release\net8.0-windows\win-x64\publish"
}
if (-not (Test-Path (Join-Path $Source "PhotoMind.App.exe"))) {
    throw "Publish output not found: $Source (run the Release self-contained publish first)"
}
if (-not $Destination) {
    $Destination = Join-Path $env:LOCALAPPDATA "Programs\PhotoMind AI"
}

Write-Host "Installing to: $Destination"
if (Test-Path $Destination) {
    Remove-Item $Destination -Recurse -Force
}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force

$exe = Join-Path $Destination "PhotoMind.App.exe"

$shell = New-Object -ComObject WScript.Shell
$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$lnk = Join-Path $startMenu "PhotoMind AI.lnk"
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.WorkingDirectory = $Destination
$sc.Description = "PhotoMind AI"
$sc.Save()

Write-Host "Installed. Launch: $exe"
