# Changelog

All notable changes to this project are documented here.

## 0.4.0 - 2026-10-07

- Provides direct Security-log and centralized WEC/`ForwardedEvents` collection.
- Resolves source host addresses and supports explicit address overrides.
- Uses the unified `Windows_AD_Events` telemetry and the `DomainController`
  Custom Telemetry template.
- Added user-account creation event 4720.
- Added successful interactive/RDP logon event 4624 (types 2 and 10 by default).
- Added persistent SID-based correlation for account creation, privileged-group
  membership, and subsequent logon by the new account.
- Includes persistent queue, event-log bookmark, retry behavior, Windows service
  installer, WEC setup, model examples, and deployment documentation.
- Supported scope: **for Darktrace /NETWORK only**.
