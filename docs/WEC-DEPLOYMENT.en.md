# Centralized deployment with Windows Event Forwarding

This architecture collects privileged-group changes from Domain Controllers and
member servers without installing the custom agent on every source.

```text
Windows servers/DCs -> WEF -> Windows Event Collector -> agent -> Darktrace
```

## 1. Create the source-computer group

Create an Active Directory security group, for example:

```text
Darktrace-WEF-Sources
```

Add the **computer accounts** of participating servers and Domain Controllers.
Do not add user accounts. Retrieve its SID:

```powershell
Get-ADGroup "Darktrace-WEF-Sources" | Select-Object Name, SID
```

## 2. Prepare the Windows Event Collector

Copy the v0.2.0 package to the WEC server, open PowerShell as Administrator, and
run:

```powershell
.\setup-wec.ps1 -AllowedSourceGroupSid "S-1-5-21-..."
```

The script enables Windows Event Collector, increases the `ForwardedEvents` log
to 1 GB, creates the source-initiated subscription
`Darktrace-Privileged-Group-Changes`, limits access to the supplied computer
group, and prints the URI required by the source-computer GPO. It will not
overwrite a subscription with the same name.

## 3. Configure auditing by GPO

Link a GPO to the OUs containing the servers and Domain Controllers:

```text
Computer Configuration
  Policies
    Windows Settings
      Security Settings
        Advanced Audit Policy Configuration
          Audit Policies
            Account Management
              Audit Security Group Management: Success
```

This policy produces event IDs `4728`, `4732`, and `4756` for additions, and
`4729`, `4733`, and `4757` for removals.

## 4. Configure event forwarding by GPO

In the same or a dedicated GPO:

```text
Computer Configuration
  Policies
    Administrative Templates
      Windows Components
        Event Forwarding
          Configure target Subscription Manager
```

Enable it and enter the URI printed by `setup-wec.ps1`, for example:

```text
Server=http://WEC01.example.local:5985/wsman/SubscriptionManager/WEC,Refresh=60
```

Configure the **Windows Remote Management (WS-Management)** service for
automatic startup and enable the WinRM firewall rules approved by your
organization. Grant `NETWORK SERVICE` access to read the Security log through
the local **Event Log Readers** group on source servers. Apply the equivalent
`BUILTIN\Event Log Readers` permission on Domain Controllers.

Then update policy:

```powershell
gpupdate /force
```

## 5. Install the agent on the collector

Use the WEC server's address as the fallback. Normally the agent resolves the
original event hostname and puts that computer's address in `src`:

```powershell
.\install.ps1 `
  -DarktraceHost 198.51.100.10 `
  -DarktracePort 1514 `
  -Protocol Tcp `
  -SourceAddress 192.0.2.20 `
  -SourceAddressMode ResolveEventComputer `
  -Channel ForwardedEvents `
  -Tag WIN_PRIV_GROUP_CHANGE
```

For multihomed hosts or ambiguous DNS, add explicit overrides to
`C:\ProgramData\DarktraceEventForwarder\agentsettings.json`:

```json
"SourceAddressOverrides": {
  "SQL01.example.local": "192.0.2.30"
}
```

Restart the service after changing the configuration.

## 6. Configure Darktrace

Use this Custom Telemetry template:

```text
Name: WindowsPrivilegedGroupChanges
Type: Custom Data
Log Filter: WIN_PRIV_GROUP_CHANGE
Pattern Match: WIN_PRIV_GROUP_CHANGE src="%{IP:src}" message=%{GREEDYDATA:message}
```

Save the template and then generate a new real event so that the metric is
created. Model filters are documented in
[DARKTRACE-CONFIGURATION.md](DARKTRACE-CONFIGURATION.md).

## 7. Validate

On the collector:

```powershell
wecutil gr "Darktrace-Privileged-Group-Changes"
Get-WinEvent -LogName ForwardedEvents -MaxEvents 10
Get-Content "C:\ProgramData\DarktraceEventForwarder\logs\agent.log" -Tail 100
```

On a source computer, inspect:

```text
Applications and Services Logs
  Microsoft
    Windows
      Eventlog-ForwardingPlugin
        Operational
```

Events 100 and 104 confirm subscription creation and connection to the
Subscription Manager.

## 8. Security considerations

- Restrict the subscription to the dedicated computer group.
- Prefer HTTPS/5986 when PKI and organizational standards support it.
- Monitor DNS resolution; the agent logs a warning when it uses the collector's
  fallback address.
- Size event-log retention, the disk queue, and availability for the number of
  source systems.
- Do not add out-of-scope workstations or servers to the source group.
