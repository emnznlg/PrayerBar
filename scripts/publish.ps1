param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sdk = Join-Path $root '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $root '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $root '.tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $root
try {
    & $sdk test PrayerBar.sln -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. Publish stopped.' }
    & $sdk publish src/PrayerBar.App/PrayerBar.App.csproj -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath README.md -Destination artifacts/win-x64/README.md
    New-Item -ItemType Directory -Force artifacts/win-x64/docs | Out-Null
    Copy-Item -LiteralPath docs/VERIFICATION.md -Destination artifacts/win-x64/docs/VERIFICATION.md
    Compress-Archive -LiteralPath artifacts/win-x64/PrayerBar.exe,artifacts/win-x64/README.md,artifacts/win-x64/docs -DestinationPath artifacts/PrayerBar-1.0.0-win-x64.zip -Force
    Get-FileHash -LiteralPath artifacts/win-x64/PrayerBar.exe,artifacts/PrayerBar-1.0.0-win-x64.zip -Algorithm SHA256
} finally { Pop-Location }
