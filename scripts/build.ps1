param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$version = "v0.3.0"
$artifact = Join-Path $root "artifacts\releases\$version\win-x64"

dotnet test (Join-Path $root "DarktraceEventForwarder.sln") -c $Configuration
dotnet publish (Join-Path $root "src\Darktrace.WindowsEventForwarder\Darktrace.WindowsEventForwarder.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $artifact

Copy-Item (Join-Path $root "scripts\install.ps1") $artifact
Copy-Item (Join-Path $root "scripts\uninstall.ps1") $artifact
Copy-Item (Join-Path $root "scripts\setup-wec.ps1") $artifact
Write-Host "Windows artifact created at $artifact"
