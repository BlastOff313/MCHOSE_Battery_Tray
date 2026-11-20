$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$publish = Join-Path $root 'publish'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $publish, $dist -Force | Out-Null

Write-Host 'Publishing MCHOSE Battery Tray (.NET 10, win-x64)...' -ForegroundColor Cyan
dotnet publish '.\src\MCHOSE.BatteryTray.csproj' `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    --output $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
if (-not (Test-Path $compiler)) {
    $compiler = Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'
}
if (-not (Test-Path $compiler)) {
    throw 'Inno Setup 6 was not found. Install it from https://jrsoftware.org/isinfo.php and run build.ps1 again.'
}

Write-Host 'Building Windows installer...' -ForegroundColor Cyan
& $compiler '.\installer\MCHOSE-Battery-Tray.iss'
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }
Write-Host "Installer created: $dist\MCHOSE-Battery-Tray-Setup-10.0.0.exe" -ForegroundColor Green
