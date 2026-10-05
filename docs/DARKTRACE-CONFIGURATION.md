# Darktrace Custom Telemetry and models

This guide describes the Darktrace configuration expected by the agent. Menu
names can vary slightly between Darktrace versions.

## 1. Confirm syslog delivery

Configure the agent to send to the Darktrace syslog listener, normally TCP
port `1514`. A direct-mode event contains this payload after the syslog header:

```text
WIN_AD_GROUP_CHANGE src="192.0.2.10" message={"EventID":4732,"SubjectUserName":"Administrator","MemberName":"CN=alice,CN=Users,DC=example,DC=local","TargetUserName":"VPN-Users"}
```

WEC mode uses the tag `WIN_PRIV_GROUP_CHANGE` by default.

## 2. Create Custom Telemetry

Open **System Config > Modules > Telemetry > Custom Telemetry**, select **Add**,
and create the template that matches the deployment mode.

### Direct mode

| Field | Value |
|---|---|
| Name | `DomainController` |
| Type | `Custom Data` |
| Log Filter | `WIN_AD_GROUP_CHANGE` |
| Pattern Match | `WIN_AD_GROUP_CHANGE src="%{IP:src}" message=%{GREEDYDATA:message}` |

### WEC mode

| Field | Value |
|---|---|
| Name | `WindowsPrivilegedGroupChanges` |
| Type | `Custom Data` |
| Log Filter | `WIN_PRIV_GROUP_CHANGE` |
| Pattern Match | `WIN_PRIV_GROUP_CHANGE src="%{IP:src}" message=%{GREEDYDATA:message}` |

Save the template before using **Test**. A successful test must display:

- `src`: the source computer's IPv4 address;
- `message`: the complete JSON object;
- `type`: `Custom::DomainController` or
  `Custom::WindowsPrivilegedGroupChanges`.

> [!CAUTION]
> The Test button only validates parsing. It does not create a live metric event.
> After saving the template, generate a **new real Windows event** and wait for
> ingestion. Only then will the custom metric be available to the Model Editor.

## 3. Create a base model

In the Model Editor, create a model and add the custom component generated from
the telemetry template. Its label is normally similar to:

- `Custom DomainController`; or
- `Custom WindowsPrivilegedGroupChanges`.

Do not select the generic **Security Integration** component. The event was
ingested as Custom Data and must use its generated custom component.

Recommended base settings:

| Setting | Value |
|---|---|
| Component threshold | `> 0` in `1` minute |
| Minimum seconds between model alerts | `1` while testing |
| Active | On |
| Auto Update | Off |
| Auto Suppress | Off while testing |
| Model action | Generate Model Alert |
| Display fields | Message and source address/device |

After validation, choose a suppression interval appropriate to the operational
workflow. Events created in the same minute may otherwise be combined or
suppressed depending on model settings.

## 4. Model: user added to VPN-Users

Add two filters to the custom component and require **A AND B**:

| Filter | Field | Operator | Value |
|---|---|---|---|
| A | Message | matches regular expression | `.*"EventID":(4728|4732|4756).*` |
| B | Message | matches regular expression | `.*"TargetUserName":"VPN-Users".*` |

This alerts on additions only. For removals, clone the model and replace filter
A with:

```regex
.*"EventID":(4729|4733|4757).*
```

## 5. Model: privilege escalation through administrative groups

Add two filters and require **A AND B**:

| Filter | Field | Operator | Value |
|---|---|---|---|
| A | Message | matches regular expression | `.*"EventID":(4728|4732|4756).*` |
| B | Message | matches regular expression | `.*"TargetSid":"(S-1-5-32-544|S-1-5-21-[0-9-]+-(512|518|519))".*` |

The SIDs cover:

- `S-1-5-32-544`: local built-in Administrators;
- RID `512`: Domain Admins;
- RID `518`: Schema Admins;
- RID `519`: Enterprise Admins.

Consider separate higher-sensitivity models for other privileged groups. Common
RIDs include `520` (Group Policy Creator Owners), `526` (Key Admins), and `527`
(Enterprise Key Admins). Built-in aliases include `548` through `551`. Custom
groups such as `DnsAdmins` do not have a universal fixed SID; determine the SID
in your domain and add it explicitly.

## 6. Fields shown in the alert

Include **Message** as a display field. The JSON identifies:

| JSON field | Meaning |
|---|---|
| `SubjectUserName` / `SubjectDomainName` | account that performed the change |
| `MemberName` / `MemberSid` | account or principal added to the group |
| `TargetUserName` / `TargetDomainName` | group that was changed |
| `TargetSid` | stable SID of the changed group |
| `Hostname` | Windows host that recorded the event |
| `EventID` / `RecordId` | event type and source record identifier |

Filtering privileged groups by `TargetSid` is more robust than translated group
names and works across localized Windows installations.

## 7. Troubleshooting

If the component is missing from the Model Editor:

1. Confirm the Custom Telemetry test parses `src`, `message`, and `type`.
2. Save the template.
3. Generate a new live event after saving it.
4. Confirm the agent log contains `Queued` and `Sent` for that event.
5. Search for the exact custom template name, not Palo Alto or Security
   Integration metrics.

If the model does not alert, temporarily remove all message filters, use `> 0 in
1 minute`, set the minimum interval to `1`, turn Auto Suppress off, and generate
a fresh event. Once the base component alerts, restore filters one at a time.
