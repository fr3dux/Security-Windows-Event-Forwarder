param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$version = "v0.4.0"
$artifact = Join-Path $root "artifacts\releases\$version\win-x64"

dotnet test (Join-Path $root "Security-Windows-Event-Forwarder.sln") -c $Configuration
dotnet publish (Join-Path $root "src\Security.WindowsEventForwarder\Security.WindowsEventForwarder.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $artifact

Copy-Item (Join-Path $root "scripts\install.ps1") $artifact
Copy-Item (Join-Path $root "scripts\uninstall.ps1") $artifact
Copy-Item (Join-Path $root "scripts\setup-wec.ps1") $artifact
$package = Join-Path (Split-Path $artifact -Parent) "Security-Windows-Event-Forwarder-$version-win-x64.zip"
if (Test-Path $package) {
    Remove-Item $package -Force
}
Compress-Archive -Path $artifact -DestinationPath $package
Write-Host "Windows artifact created at $artifact"
Write-Host "Release package created at $package"
