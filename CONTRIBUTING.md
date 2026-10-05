# Contributing

Contributions that improve reliability, Windows compatibility, documentation,
testing, or the Darktrace Custom Telemetry integration are welcome.

## Before opening a pull request

1. Do not include real hostnames, domains, usernames, SIDs, IP addresses, logs,
   credentials, or appliance details. Use `example.local` and documentation
   address ranges such as `192.0.2.0/24` and `198.51.100.0/24`.
2. Keep the output contract compatible with Darktrace Custom Telemetry.
3. Add or update tests for behavioral changes.
4. Run `dotnet test DarktraceEventForwarder.sln -c Release`.
5. Explain security and backward-compatibility implications in the pull request.

Use GitHub Issues for reproducible bugs and focused feature proposals. Follow
[SECURITY.md](SECURITY.md) for vulnerability reports.
