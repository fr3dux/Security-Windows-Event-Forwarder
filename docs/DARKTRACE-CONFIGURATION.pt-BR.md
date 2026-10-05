# Custom Telemetry e modelos no Darktrace

Todos os eventos usam a tag única `Windows_AD_Events`. O tipo de atividade é
identificado pelos campos JSON capturados em `message=`; a tag não representa
mais somente alterações de grupos.

## 1. Criar o Custom Telemetry

Acesse **System Config > Modules > Telemetry > Custom Telemetry**, clique em
**Add** e configure:

| Campo | Valor |
|---|---|
| Name | `Windows_AD_Events` |
| Type | `Custom Data` |
| Log Filter | `Windows_AD_Events` |
| Pattern Match | `Windows_AD_Events src="%{IP:src}" message=%{GREEDYDATA:message}` |

Exemplo recebido:

```text
Windows_AD_Events src="192.0.2.10" message={"EventID":4720,"TargetUserName":"new-admin","TargetSid":"S-1-5-21-1-2-3-1200"}
```

Salve antes de usar **Test**. O resultado deve mostrar `src`, o JSON completo em
`message` e `type=Custom::Windows_AD_Events`.

> [!CAUTION]
> O Test valida somente o parser. Depois de salvar, gere um evento real novo e
> aguarde a ingestão para que `Custom Windows_AD_Events` apareça no Model Editor.

## 2. Eventos enviados

| Event ID | Uso |
|---:|---|
| 4624 | logon bem-sucedido; por padrão somente tipos 2 e 10 |
| 4720 | conta de usuário criada |
| 4728 / 4732 / 4756 | membro adicionado a grupo de segurança |
| 4729 / 4733 / 4757 | membro removido de grupo de segurança |

O agente não envia todos os eventos `4624`. `Correlation:AllowedLogonTypes`
controla os tipos aceitos; o padrão `[2, 10]` cobre logon interativo e RDP sem o
volume elevado de logons de rede tipo 3.

## 3. Configuração-base dos modelos

Use o componente **Custom Windows_AD_Events**, nunca o componente genérico
Security Integration. Durante os testes:

| Opção | Valor |
|---|---|
| Component threshold | `> 0` |
| Janela | `1` minuto para eventos simples; `30` minutos para cadeias |
| Minimum seconds between model alerts | `1` |
| Active | Ativado |
| Auto Update | Desativado |
| Auto Suppress | Desativado |
| Model action | Generate Model Alert |
| Display fields | Message e dispositivo/endereço de origem |

## 4. Modelo simples: uma conta foi criada

Um componente com:

| Filtro | Campo | Operador | Valor |
|---|---|---|---|
| A | Message | matches regular expression | `.*"EventID":4720.*` |

O alerta mostrará `TargetUserName`, `TargetSid`, `SubjectUserName` e
`SubjectDomainName`, ou seja, a conta criada e quem executou a criação.

## 5. Modelo simples: escalação para grupo administrativo

Use A AND B:

| Filtro | Campo | Operador | Valor |
|---|---|---|---|
| A | Message | matches regular expression | `.*"EventID":(4728|4732|4756).*` |
| B | Message | matches regular expression | `.*"TargetSid":"(S-1-5-32-544|S-1-5-21-[0-9-]+-(512|518|519))".*` |

Isso cobre Administrators, Domain Admins, Schema Admins e Enterprise Admins.

## 6. Modelo comportamental: logon, criação e escalação

Crie três componentes `Custom Windows_AD_Events`, todos dentro de 30 minutos:

1. **Logon interativo/RDP**
   - A: `.*"EventID":4624.*`
   - B: `.*"LogonType":"(2|10)".*`
   - condição: A AND B.
2. **Criação de conta**
   - A: `.*"EventID":4720.*`
3. **Adição a grupo administrativo**
   - A: `.*"EventID":(4728|4732|4756).*`
   - B: `.*"TargetSid":"(S-1-5-32-544|S-1-5-21-[0-9-]+-(512|518|519))".*`
   - condição: A AND B.

Configure o modelo para disparar quando todos os componentes forem verdadeiros.
Esse modelo identifica coocorrência suspeita. Dependendo da versão do Darktrace,
ele pode não garantir ordem estrita nem comparar dinamicamente o SID entre
componentes.

## 7. Modelo de alta confiança: cadeia completa com novo logon

O agente relaciona o logon inicial ao evento de criação usando
`TargetLogonId`/`SubjectLogonId` e acompanha a nova conta usando
`TargetSid`/`MemberSid`/`TargetUserSid`. Quando as quatro etapas são confirmadas,
ele emite outro evento na mesma telemetria contendo:

```json
"CorrelationType":"InitialLogonThenCreatedAccountAddedToPrivilegedGroupThenNewAccountLoggedOn"
```

Crie um componente com:

| Filtro | Campo | Operador | Valor |
|---|---|---|---|
| A | Message | matches regular expression | `.*"CorrelationType":"InitialLogonThenCreatedAccountAddedToPrivilegedGroupThenNewAccountLoggedOn".*` |
| B | Message | matches regular expression | `.*"Risk":"High".*` |

Use A AND B. Esse é o alerta recomendado para confirmar o **logon inicial do
operador**, a criação, a escalação e o logon da **própria conta nova**, evitando
juntar eventos de usuários diferentes. A janela do agente é configurada em
`Correlation:WindowMinutes` e o padrão é 1440 minutos.

O JSON correlacionado inclui os campos `InitialLogon*`, `AccountSid`,
`AccountName`, `CreatedAt`, `CreatedBy`, `PrivilegedAt`, `PrivilegedBy`,
`PrivilegedGroupName`, `PrivilegedGroupSid`, `LogonAt`, `LogonHost`,
`LogonType`, `LogonIpAddress` e `LogonWorkstationName`.

Se o Windows não fornecer um `SubjectLogonId` relacionável, o agente ainda pode
emitir a correlação de três etapas com
`CorrelationType=CreatedAccountAddedToPrivilegedGroupThenLoggedOn`. Mantenha-a
em um modelo separado com prioridade inferior.

## 8. Limitação de origem e WEC

Em modo direto, o agente enxerga somente eventos registrados no servidor local.
Para detectar um usuário criado no DC e usado depois em outro servidor, use WEF
para enviar os eventos de todos os servidores em escopo ao WEC.

Nos modelos Darktrace com vários componentes, os eventos ainda podem ser
associados ao `src` de suas máquinas originais. A telemetria correlacionada do
agente não depende de todos os passos aparecerem no mesmo dispositivo Darktrace.

## 9. Campos úteis no alerta

| Campo JSON | Significado |
|---|---|
| `SubjectUserName` / `SubjectDomainName` | quem executou a alteração |
| `TargetUserName` / `TargetSid` no 4720 | conta criada |
| `MemberName` / `MemberSid` | conta adicionada ao grupo |
| `TargetUserName` / `TargetSid` nos eventos de grupo | grupo alterado |
| `TargetUserName` / `TargetUserSid` no 4624 | conta que fez logon |
| `LogonType`, `IpAddress`, `WorkstationName` | contexto do logon |

## 10. Solução de problemas

Se o componente não aparecer, salve o template, gere um evento novo, confirme
`Queued` e `Sent` no log do agente e procure exatamente por
`Custom Windows_AD_Events`. Se um modelo não alertar, remova temporariamente os
filtros, use `> 0 em 1 minuto`, desative Auto Suppress e recoloque um filtro de
cada vez.
