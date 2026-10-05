param(
    [Parameter(Mandatory = $true)][string]$DarktraceHost,
    [int]$DarktracePort = 1514,
    [ValidateSet("Tcp", "Udp")][string]$Protocol = "Tcp",
    [Parameter(Mandatory = $true)][string]$SourceAddress,
    [ValidateSet("Configured", "ResolveEventComputer")][string]$SourceAddressMode = "Configured",
    [string]$Channel = "Security",
    [string]$Tag = "WIN_AD_GROUP_CHANGE"
)

$ErrorActionPreference = "Stop"
$serviceName = "DarktraceEventForwarder"
$installDirectory = Join-Path $env:ProgramFiles "DarktraceEventForwarder"
$dataDirectory = Join-Path $env:ProgramData "DarktraceEventForwarder"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell window."
}

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    throw "Service $serviceName is already installed."
}

New-Item -ItemType Directory -Force -Path $installDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $dataDirectory | Out-Null
icacls.exe $dataDirectory /inheritance:r /grant:r `
    "*S-1-5-18:(OI)(CI)F" `
    "*S-1-5-32-544:(OI)(CI)F" | Out-Null
Copy-Item (Join-Path $PSScriptRoot "DarktraceEventForwarder.exe") $installDirectory
Copy-Item (Join-Path $PSScriptRoot "agentsettings.json") $installDirectory

$settings = @{
    Agent = @{
        SourceAddress = $SourceAddress
        SourceAddressMode = $SourceAddressMode
        Channel = $Channel
        Tag = $Tag
        ReadExistingEventsOnFirstStart = $false
        EventIds = @(4728, 4732, 4756, 4729, 4733, 4757)
        SourceAddressOverrides = @{}
    }
    Syslog = @{
        Host = $DarktraceHost
        Port = $DarktracePort
        Protocol = $Protocol
        ConnectTimeoutSeconds = 10
    }
    Storage = @{
        MaximumQueueSizeMB = 100
        RetryInitialSeconds = 2
        RetryMaximumSeconds = 300
    }
}
$settings | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $dataDirectory "agentsettings.json") -Encoding UTF8

$binary = Join-Path $installDirectory "DarktraceEventForwarder.exe"
sc.exe create $serviceName binPath= "`"$binary`"" start= auto obj= LocalSystem DisplayName= "Darktrace Event Forwarder" | Out-Null
sc.exe description $serviceName "Forwards selected Windows Security events to Darktrace Custom Telemetry." | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
Start-Service $serviceName

Write-Host "Darktrace Event Forwarder installed and started."
Write-Host "Log: $dataDirectory\logs\agent.log"
