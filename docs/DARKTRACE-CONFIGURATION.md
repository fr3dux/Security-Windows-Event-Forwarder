# Darktrace Custom Telemetry and models

All events use the single `Windows_AD_Events` tag. Activity types are selected
from the JSON captured in `message=` instead of being encoded in the tag.

## Custom Telemetry

Open **System Config > Modules > Telemetry > Custom Telemetry**, select **Add**,
and configure:

| Field | Value |
|---|---|
| Name | `Windows_AD_Events` |
| Type | `Custom Data` |
| Log Filter | `Windows_AD_Events` |
| Pattern Match | `Windows_AD_Events src="%{IP:src}" message=%{GREEDYDATA:message}` |

Save before using **Test**. A successful result displays the IPv4 address in
`src`, the full JSON object in `message`, and
`type=Custom::Windows_AD_Events`. Testing validates only the parser: save the
template and generate a new live event before looking for the component in the
Model Editor.

## Forwarded events

| Event ID | Purpose |
|---:|---|
| 4624 | successful logon; types 2 and 10 by default |
| 4720 | user account created |
| 4728 / 4732 / 4756 | member added to a security group |
| 4729 / 4733 / 4757 | member removed from a security group |

`Correlation:AllowedLogonTypes` controls which 4624 events are accepted. The
default `[2, 10]` covers interactive and RDP logons without the high volume of
type 3 network logons.

Use **Custom Windows_AD_Events**, not Security Integration, in every model.
While testing, use a threshold greater than zero, minimum alert interval `1`,
Auto Suppress off, Generate Model Alert on, and Message as a display field.

## Model: account created

Use one Message regular-expression filter:

```regex
.*"EventID":4720.*
```

## Model: account added to an administrative group

Require both filters:

```regex
.*"EventID":(4728|4732|4756).*
.*"TargetSid":"(S-1-5-32-544|S-1-5-21-[0-9-]+-(512|518|519))".*
```

These SIDs cover built-in Administrators, Domain Admins, Schema Admins, and
Enterprise Admins.

## Behavioral model: logon, account creation, and privilege escalation

Create three `Custom Windows_AD_Events` components in a 30-minute window:

1. Successful interactive/RDP logon: EventID `4624` and LogonType `2` or `10`.
2. Account creation: EventID `4720`.
3. Administrative-group addition: EventID `4728`, `4732`, or `4756`, plus the
   privileged `TargetSid` expression above.

Trigger when all components are true. This detects suspicious co-occurrence,
but a Darktrace version may not enforce strict order or dynamically join the
same account SID across raw components.

## High-confidence model: complete chain and new-account logon

The agent links the initial logon to account creation through
`TargetLogonId`/`SubjectLogonId`, then tracks the new identity through
`TargetSid`, `MemberSid`, and `TargetUserSid`. When all four stages occur within
`Correlation:WindowMinutes` (1440 by default), it emits an additional event in
the same telemetry. Require both Message filters:

```regex
.*"CorrelationType":"InitialLogonThenCreatedAccountAddedToPrivilegedGroupThenNewAccountLoggedOn".*
.*"Risk":"High".*
```

This confirms the operator's initial session, identity, and event order rather
than combining unrelated users. The JSON includes `InitialLogon*` fields,
account SID/name, creator, privileged group, timestamps, new-account logon
host/type, source IP, and workstation name.

If Windows does not provide a linkable `SubjectLogonId`, the agent can still
emit the three-stage correlation type
`CreatedAccountAddedToPrivilegedGroupThenLoggedOn`. Keep it in a separate,
lower-priority model.

## Deployment scope

Direct mode sees only events recorded on the local computer. To correlate an
account created on a Domain Controller with a later logon to another server,
collect all in-scope servers through WEF/WEC. Raw multi-component models can
also be device-scoped in Darktrace; the agent's synthetic correlated event does
not require every step to map to the same Darktrace device.

## Troubleshooting

If the component is missing, save the template, generate a new event, confirm
`Queued` and `Sent` in the agent log, and search for exactly
`Custom Windows_AD_Events`. If a model does not alert, temporarily remove its
Message filters, use `> 0 in 1 minute`, disable Auto Suppress, and restore one
filter at a time.
