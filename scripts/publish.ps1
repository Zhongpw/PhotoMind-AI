[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

# PhotoMind AI - self-contained publish script.
# Produces a win-x64 self-contained app (no .NET runtime required on the target machine).
# Requires the win-x64 runtime packs in .nuget/offline (see .tools/download-runtime-packs.mjs).
$root = Split-Path -Parent $PSScriptRoot

$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:APPDATA = Join-Path $root '.home\AppData\Roaming'
$env:LOCALAPPDATA = Join-Path $root '.home\AppData\Local'
$env:TEMP = Join-Path $root '.home\Temp'
$env:TMP = $env:TEMP
$env:NUGET_PACKAGES = Join-Path $root '.nuget\packages'

$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$appProj = Join-Path $root 'src\PhotoMind.App\PhotoMind.App.csproj'

$base = @('/p:Configuration=Release', "/p:RuntimeIdentifier=$Runtime", '/p:SelfContained=true',
          '/p:RestoreConfigFile=nuget.offline.config', '/p:MSBuildEnableWorkloadResolver=false')

Write-Host "Restoring $Runtime runtime packs ..." -ForegroundColor Cyan
& $dotnet msbuild $appProj /t:Restore @base -m:1 -nodeReuse:false -v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "Restore FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Publishing self-contained $Runtime ..." -ForegroundColor Cyan
& $dotnet msbuild $appProj /t:Publish @base -m:1 -nodeReuse:false -v:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

$out = Join-Path $root "src\PhotoMind.App\bin\Release\net8.0-windows\$Runtime\publish"
Write-Host "Publish succeeded: $out" -ForegroundColor Green
