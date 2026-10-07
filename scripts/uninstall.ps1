$ErrorActionPreference = "Stop"
$serviceName = "Security-Windows-Event-Forwarder"
$installDirectory = Join-Path $env:ProgramFiles "Security-Windows-Event-Forwarder"

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne "Stopped") {
        Stop-Service $serviceName -Force
    }
    sc.exe delete $serviceName | Out-Null
}

if (Test-Path $installDirectory) {
    Remove-Item $installDirectory -Recurse -Force
}

Write-Host "Service removed. Queue, configuration, bookmark, and logs were preserved under $env:ProgramData\Security-Windows-Event-Forwarder."
