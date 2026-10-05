# Implantação centralizada com Windows Event Forwarding

Esta arquitetura coleta alterações de grupos privilegiados de Domain Controllers e servidores membros sem instalar o agente personalizado em cada origem.

```text
Windows Servers/DCs -> WEF -> Windows Event Collector -> agente v0.2 -> Darktrace
```

## 1. Criar o grupo de origens

Crie no Active Directory um grupo de segurança, por exemplo:

```text
Darktrace-WEF-Sources
```

Adicione as **contas de computador** dos servidores e Domain Controllers que participarão. Não adicione contas de usuários.

Obtenha o SID do grupo:

```powershell
Get-ADGroup "Darktrace-WEF-Sources" | Select-Object Name, SID
```

## 2. Preparar o Windows Event Collector

No servidor escolhido como WEC, copie o pacote `v0.2.0`, abra PowerShell como administrador e execute:

```powershell
.\setup-wec.ps1 -AllowedSourceGroupSid "S-1-5-21-..."
```

O script:

- habilita o serviço Windows Event Collector;
- aumenta o `ForwardedEvents` para 1 GB;
- cria a subscription `Darktrace-Privileged-Group-Changes`;
- limita o acesso ao grupo de computadores informado;
- mostra o endereço que deve ser configurado na GPO dos servidores de origem.

O script não sobrescreve uma subscription existente com o mesmo nome.

## 3. GPO de auditoria para os servidores de origem

Vincule a GPO às OUs que contêm os servidores e Domain Controllers:

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

Ative também:

```text
Account Management
  Audit User Account Management: Success
Logon/Logoff
  Audit Logon: Success
```

Essa política gera:

```text
4728, 4732, 4756  adições
4729, 4733, 4757  remoções
```

## 4. GPO de encaminhamento

Na mesma GPO ou em uma GPO dedicada:

```text
Computer Configuration
  Policies
    Administrative Templates
      Windows Components
        Event Forwarding
          Configure target Subscription Manager
```

Habilite e adicione a URI exibida pelo `setup-wec.ps1`, semelhante a:

```text
Server=http://WEC01.example.local:5985/wsman/SubscriptionManager/WEC,Refresh=60
```

Configure também o serviço **Windows Remote Management (WS-Management)** para inicialização automática e as regras corporativas correspondentes de WinRM.

Para encaminhar o Security Log, adicione `NETWORK SERVICE` ao grupo local **Event Log Readers** nos servidores de origem por GPO. Em Domain Controllers, aplique a permissão equivalente ao grupo `BUILTIN\Event Log Readers`.

Depois:

```powershell
gpupdate /force
```

## 5. Instalar o agente no WEC

Use o IP do WEC como fallback. Em condições normais, o agente resolve o hostname original de cada evento e utiliza o IP da máquina de origem no campo `src`.

```powershell
.\install.ps1 `
  -DarktraceHost 198.51.100.10 `
  -DarktracePort 1514 `
  -Protocol Tcp `
  -SourceAddress 192.0.2.20 `
  -SourceAddressMode ResolveEventComputer `
  -Channel ForwardedEvents `
  -Tag Windows_AD_Events
```

Para servidores com vários IPs ou resolução DNS ambígua, configure `SourceAddressOverrides` em:

```text
C:\ProgramData\DarktraceEventForwarder\agentsettings.json
```

Exemplo:

```json
"SourceAddressOverrides": {
  "SQL01.example.local": "192.0.2.30"
}
```

Reinicie o serviço depois de alterar a configuração.

## 6. Custom Telemetry no Darktrace

```text
Name: DomainController
Type: Custom Data
Log Filter: Windows_AD_Events
Pattern Match: Windows_AD_Events src="%{IP:src}" message=%{GREEDYDATA:message}
```

Faça um evento real depois de salvar o template para que a nova métrica seja criada.

Os filtros completos para os modelos de VPN e grupos administrativos estão em
[DARKTRACE-CONFIGURATION.pt-BR.md](DARKTRACE-CONFIGURATION.pt-BR.md).

## 7. Validação

No WEC:

```powershell
wecutil gr "Darktrace-Privileged-Group-Changes"
Get-WinEvent -LogName ForwardedEvents -MaxEvents 10
Get-Content "C:\ProgramData\DarktraceEventForwarder\logs\agent.log" -Tail 100
```

Na origem, verifique:

```text
Applications and Services Logs
  Microsoft
    Windows
      Eventlog-ForwardingPlugin
        Operational
```

Eventos 100 e 104 confirmam a criação da subscription e a conexão com o Subscription Manager.

## 8. Observações de segurança

- Restrinja a subscription ao grupo de computadores criado para WEF.
- Prefira HTTPS/5986 quando houver PKI e esse for o padrão da organização.
- Monitore a resolução de DNS; o agente registra warning quando utiliza o IP fallback do coletor.
- Dimensione retenção, fila e alta disponibilidade conforme a quantidade de servidores.
- Não inclua estações ou servidores fora do escopo no grupo de origens.
