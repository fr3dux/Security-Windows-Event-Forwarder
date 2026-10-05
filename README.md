# Darktrace Windows Event Forwarder

Purpose-built Windows service that forwards selected Windows Security events to
**Darktrace Custom Telemetry**. It reads structured fields from Windows Event
XML, serializes them as JSON, and sends one RFC 3164 syslog line per event.

> [!IMPORTANT]
> This software is designed exclusively for Darktrace Custom Telemetry. Its
> message contract, tags, and deployment guidance are intentionally tailored to
> Darktrace and it is not intended to be a generic syslog or SIEM agent.

This is an independent community project. It is not affiliated with, endorsed
by, or supported by Darktrace. Darktrace is a trademark of its respective owner.

Portuguese documentation: [Configurando o Darktrace](docs/DARKTRACE-CONFIGURATION.pt-BR.md) ·
[Implantação WEC](docs/WEC-DEPLOYMENT.md)

## What it monitors

The default configuration forwards account lifecycle, interactive logon, and
security-group membership events:

| Event ID | Meaning |
|---:|---|
| 4624 | Successful logon (types 2 and 10 by default) |
| 4720 | User account created |
| 4728 | Member added to a global security group |
| 4732 | Member added to a local security group |
| 4756 | Member added to a universal security group |
| 4729 | Member removed from a global security group |
| 4733 | Member removed from a local security group |
| 4757 | Member removed from a universal security group |

The agent parses `EventData` directly from Windows Event XML, so detection does
not depend on the display language used by Event Viewer.

## Deployment modes

- **Direct mode:** install one agent on each writable Domain Controller or
  Windows server and read its local `Security` channel.
- **WEC mode (recommended at scale):** use native Windows Event Forwarding to
  centralize events in `ForwardedEvents`, then install one agent on the Windows
  Event Collector.

See [Windows Event Collector deployment](docs/WEC-DEPLOYMENT.en.md) for the full
WEC procedure.

## Darktrace message contract

Each event is sent as a single RFC 3164 line:

```text
<134>Oct 03 12:30:00 DC01.example.local Windows_AD_Events src="192.0.2.10" message={"EventID":4732,"TargetUserName":"VPN-Users"}
```

The JSON contains `EventTime`, `Hostname`, `EventID`, `RecordId`, `Provider`,
and every named field present in the Windows event, including fields such as
`SubjectUserName`, `MemberName`, `MemberSid`, `TargetUserName`, and `TargetSid`.

The agent can also persistently correlate the same account SID across account
creation, privileged-group membership, and a later successful logon. It emits a
high-confidence event into the same `Windows_AD_Events` telemetry while keeping
the individual events available for multi-component Darktrace models.

For exact Custom Telemetry templates and model filters, see
[Darktrace configuration](docs/DARKTRACE-CONFIGURATION.md).

The wire tag is `Windows_AD_Events`, while the Darktrace Custom Telemetry
template remains named `DomainController`. Models must use the generated
**Custom DomainController** component; these identifiers are intentionally
different to preserve backward compatibility.

## Requirements

- Windows Server 2016 or later
- Administrator access for installation
- Advanced Audit Policy for **Audit Security Group Management / Success**,
  **Audit User Account Management / Success**, and **Audit Logon / Success**
- Network connectivity from the agent to the Darktrace appliance, normally TCP
  port `1514`
- .NET 8 SDK only on the build workstation; published packages are self-contained

## Build

From a PowerShell session with .NET 8 SDK:

```powershell
.\scripts\build.ps1
```

The self-contained Windows x64 package is produced at:

```text
artifacts\releases\v0.3.0\win-x64
```

## Install: direct mode

Copy the published `win-x64` directory to the Windows server and run PowerShell
as Administrator:

```powershell
.\install.ps1 `
  -DarktraceHost 198.51.100.10 `
  -DarktracePort 1514 `
  -Protocol Tcp `
  -SourceAddress 192.0.2.10
```

The service is installed as `LocalSystem`, starts automatically, and stores its
effective configuration at:

```text
C:\ProgramData\DarktraceEventForwarder\agentsettings.json
```

After changing that file, restart the service:

```powershell
Restart-Service DarktraceEventForwarder
```

## Validate

```powershell
Get-Service DarktraceEventForwarder
Get-Content "C:\ProgramData\DarktraceEventForwarder\logs\agent.log" -Tail 100
```

Generate a new group-membership event and confirm that the log contains both
`Queued` and `Sent`. Then validate the live event in Darktrace Custom Telemetry.

## Reliability and storage

- `EventLogWatcher` receives new events in real time.
- A bookmark is saved after an event enters the local queue.
- A disk-backed queue retains events while Darktrace is unavailable.
- Sending uses exponential retry.
- `EventRecordID` can identify a duplicate after a checkpoint-edge failure.

Operational data is stored under:

```text
C:\ProgramData\DarktraceEventForwarder
```

The installer restricts this directory to `SYSTEM` and local `Administrators`
because its queue and logs may contain sensitive Security Log data.

## Uninstall

Run PowerShell as Administrator:

```powershell
.\uninstall.ps1
```

The uninstaller removes the service and program files but preserves the
configuration, queue, bookmark, and logs under `ProgramData` to prevent
accidental data loss.

## Security notes

- Community binaries are not code-signed. Build from source or sign the binary
  according to your organization's software-trust policy.
- The service runs as `LocalSystem` because access to the Security event log is
  privileged. Restrict write access to its executable and configuration.
- TCP and UDP transports currently do not provide TLS. Use a trusted management
  network or an approved encrypted transport path.
- Review event contents and retention before production use; Security Log data
  can contain sensitive identity information.

Please report vulnerabilities according to [SECURITY.md](SECURITY.md).

## Version status

- `v0.1.0`: direct Security-log collector, validated in a Windows Server 2016 lab.
- `v0.2.0`: adds WEC/`ForwardedEvents` support and source-host address resolution;
  treat WEC mode as pre-release until it has been validated in your environment.
- `v0.3.0`: adds account creation, filtered successful logons, and SID-based
  account-creation → privilege-escalation → logon correlation.

Local build artifacts are intentionally excluded from Git. Published binaries
should be attached to versioned GitHub Releases after validation.

## License

Released under the [MIT License](LICENSE).
