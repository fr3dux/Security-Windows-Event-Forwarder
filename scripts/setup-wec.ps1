param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^S-1-5-21-[0-9-]+$')]
    [string]$AllowedSourceGroupSid,

    [string]$SubscriptionId = "Darktrace-Privileged-Group-Changes"
)

$ErrorActionPreference = "Stop"
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell window."
}

wecutil.exe gs $SubscriptionId 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    throw "Subscription '$SubscriptionId' already exists. This script will not overwrite it."
}

winrm.cmd quickconfig -quiet
wecutil.exe qc /q
wevtutil.exe sl ForwardedEvents /ms:1073741824

$dataDirectory = Join-Path $env:ProgramData "DarktraceEventForwarder"
New-Item -ItemType Directory -Force -Path $dataDirectory | Out-Null
$subscriptionPath = Join-Path $dataDirectory "$SubscriptionId.xml"
$allowedSources = "O:NSG:NSD:(A;;GA;;;$AllowedSourceGroupSid)(A;;GA;;;NS)"

$xml = @"
<Subscription xmlns="http://schemas.microsoft.com/2006/03/windows/events/subscription">
  <SubscriptionId>$SubscriptionId</SubscriptionId>
  <SubscriptionType>SourceInitiated</SubscriptionType>
  <Description>Collects account lifecycle, privileged membership, and interactive logon events for Darktrace.</Description>
  <Enabled>true</Enabled>
  <Uri>http://schemas.microsoft.com/wbem/wsman/1/windows/EventLog</Uri>
  <ConfigurationMode>Custom</ConfigurationMode>
  <Delivery Mode="Push">
    <Batching>
      <MaxItems>1</MaxItems>
      <MaxLatencyTime>1000</MaxLatencyTime>
    </Batching>
    <PushSettings>
      <Heartbeat Interval="60000"/>
    </PushSettings>
  </Delivery>
  <Query><![CDATA[
    <QueryList>
      <Query Id="0" Path="Security">
        <Select Path="Security">*[System[(EventID=4720 or EventID=4728 or EventID=4732 or EventID=4756 or EventID=4729 or EventID=4733 or EventID=4757)]]</Select>
        <Select Path="Security">*[System[(EventID=4624)]] and *[EventData[Data[@Name='LogonType']='2' or Data[@Name='LogonType']='10']]</Select>
      </Query>
    </QueryList>
  ]]></Query>
  <ReadExistingEvents>false</ReadExistingEvents>
  <TransportName>http</TransportName>
  <ContentFormat>RenderedText</ContentFormat>
  <Locale Language="en-US"/>
  <LogFile>ForwardedEvents</LogFile>
  <AllowedSourceNonDomainComputers></AllowedSourceNonDomainComputers>
  <AllowedSourceDomainComputers>$allowedSources</AllowedSourceDomainComputers>
</Subscription>
"@

$xml | Set-Content -Path $subscriptionPath -Encoding UTF8
wecutil.exe cs $subscriptionPath
if ($LASTEXITCODE -ne 0) {
    throw "wecutil could not create the subscription. Review $subscriptionPath."
}

$collectorFqdn = [System.Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName
Write-Host "WEC subscription created: $SubscriptionId"
Write-Host "Configure the source-computer GPO SubscriptionManager with:"
Write-Host "Server=http://$collectorFqdn`:5985/wsman/SubscriptionManager/WEC,Refresh=60"
Write-Host "Then validate with: wecutil gr `"$SubscriptionId`""
