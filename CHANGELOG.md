# Changelog

All notable changes to this project are documented here.

## 0.4.0 - 2026-10-07

- Renamed the project and repository to `Security-Windows-Event-Forwarder`.
- Renamed the Windows service, executable, namespaces, data paths, scripts, and
  release package so the third-party product name is not used as software branding.
- Documented the supported scope as **for Darktrace /NETWORK only**.

## 0.3.0 - 2026-10-05

- Replaced event-specific tags with the unified `Windows_AD_Events` telemetry.
- Kept the Darktrace Custom Telemetry template name `DomainController` for
  backward compatibility with existing model components.
- Added user-account creation event 4720.
- Added successful interactive/RDP logon event 4624 (types 2 and 10 by default).
- Added persistent SID-based correlation for account creation, privileged-group
  membership, and subsequent logon by the new account.
- Added Darktrace multi-component and high-confidence model guidance.

## 0.2.0 - 2026-10-05

- Added Windows Event Collector and `ForwardedEvents` support.
- Added source-computer DNS resolution and explicit address overrides.
- Added source-initiated WEC subscription setup script.
- Added deployment and Darktrace model documentation.

WEC mode should be treated as pre-release until validated in a representative
environment.

## 0.1.0 - 2026-10-03

- Initial direct Windows Security event collector.
- Added persistent queue, event-log bookmark, and retry behavior.
- Added Windows service installer and uninstaller.
- Validated direct mode on Windows Server 2016 in a lab environment.
