param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sdk = @(
    (Join-Path $root '.tools\dotnet\dotnet.exe'),
    (Get-Command dotnet -ErrorAction SilentlyContinue).Source,
    'C:\Program Files\dotnet\dotnet.exe'
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $sdk) { throw 'The .NET 10 SDK was not found. Install it with: winget install Microsoft.DotNet.SDK.10' }
$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 was not found. Install it from https://jrsoftware.org/isdl.php or run: winget install --id JRSoftware.InnoSetup --exact' }

$env:DOTNET_CLI_HOME = Join-Path $root '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $root '.tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$payload = Join-Path $root 'artifacts\installer-payload'
New-Item -ItemType Directory -Force $payload | Out-Null

Push-Location $root
try {
    & $sdk test PrayerBar.sln -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. Setup was not created.' }
    # The preceding solution test restores without a runtime identifier, so publish
    # must restore its win-x64 runtime graph before bundling the single EXE.
    & $sdk publish src/PrayerBar.App/PrayerBar.App.csproj -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $payload
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath README.md -Destination (Join-Path $payload 'README.md') -Force
    Copy-Item -LiteralPath docs\VERIFICATION.md -Destination (Join-Path $payload 'VERIFICATION.md') -Force
    & $iscc (Join-Path $root 'installer\PrayerBar.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    Get-Item (Join-Path $root 'artifacts\installer\PrayerBar-Setup-1.0.1-win-x64.exe')
} finally { Pop-Location }
