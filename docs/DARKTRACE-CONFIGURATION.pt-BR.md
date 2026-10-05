# Custom Telemetry e modelos no Darktrace

Este guia descreve a configuração do Darktrace esperada pelo agente. Os nomes
dos menus podem variar um pouco entre versões do Darktrace.

## 1. Confirmar o envio por syslog

Configure o agente para enviar ao listener syslog do Darktrace, normalmente na
porta TCP `1514`. No modo direto, o conteúdo depois do cabeçalho syslog é:

```text
WIN_AD_GROUP_CHANGE src="192.0.2.10" message={"EventID":4732,"SubjectUserName":"Administrator","MemberName":"CN=alice,CN=Users,DC=example,DC=local","TargetUserName":"VPN-Users"}
```

No modo WEC, a tag padrão é `WIN_PRIV_GROUP_CHANGE`.

## 2. Criar o Custom Telemetry

Acesse **System Config > Modules > Telemetry > Custom Telemetry**, clique em
**Add** e crie o template correspondente ao modo de implantação.

### Modo direto

| Campo | Valor |
|---|---|
| Name | `DomainController` |
| Type | `Custom Data` |
| Log Filter | `WIN_AD_GROUP_CHANGE` |
| Pattern Match | `WIN_AD_GROUP_CHANGE src="%{IP:src}" message=%{GREEDYDATA:message}` |

### Modo WEC

| Campo | Valor |
|---|---|
| Name | `WindowsPrivilegedGroupChanges` |
| Type | `Custom Data` |
| Log Filter | `WIN_PRIV_GROUP_CHANGE` |
| Pattern Match | `WIN_PRIV_GROUP_CHANGE src="%{IP:src}" message=%{GREEDYDATA:message}` |

Salve o template antes de clicar em **Test**. Um teste bem-sucedido deve exibir:

- `src`: endereço IPv4 do computador de origem;
- `message`: objeto JSON completo;
- `type`: `Custom::DomainController` ou
  `Custom::WindowsPrivilegedGroupChanges`.

> [!CAUTION]
> O botão Test valida somente o parser; ele não cria um evento real da métrica.
> Depois de salvar o template, gere **um novo evento no Windows** e aguarde a
> ingestão. Só então o componente customizado ficará disponível no Model Editor.

## 3. Criar o modelo-base

No Model Editor, crie um modelo e adicione o componente customizado gerado pelo
template. O nome normalmente aparece como:

- `Custom DomainController`; ou
- `Custom WindowsPrivilegedGroupChanges`.

Não use o componente genérico **Security Integration**. O evento entrou como
Custom Data e deve usar o componente customizado criado para esse template.

Configuração-base recomendada:

| Opção | Valor |
|---|---|
| Limite do componente | `> 0` em `1` minuto |
| Minimum seconds between model alerts | `1` durante os testes |
| Active | Ativado |
| Auto Update | Desativado |
| Auto Suppress | Desativado durante os testes |
| Model action | Generate Model Alert |
| Display fields | Message e endereço/dispositivo de origem |

Depois de validar, ajuste a supressão à operação. Eventos feitos no mesmo minuto
podem ser agrupados ou suprimidos, dependendo das opções do modelo.

## 4. Modelo: novo usuário adicionado ao grupo VPN-Users

Adicione dois filtros ao componente e configure **A AND B**:

| Filtro | Campo | Operador | Valor |
|---|---|---|---|
| A | Message | matches regular expression | `.*"EventID":(4728|4732|4756).*` |
| B | Message | matches regular expression | `.*"TargetUserName":"VPN-Users".*` |

Esse modelo alerta somente adições. Para remoções, clone o modelo e troque o
filtro A por:

```regex
.*"EventID":(4729|4733|4757).*
```

## 5. Modelo: escalação de privilégio por grupos administrativos

Adicione dois filtros e configure **A AND B**:

| Filtro | Campo | Operador | Valor |
|---|---|---|---|
| A | Message | matches regular expression | `.*"EventID":(4728|4732|4756).*` |
| B | Message | matches regular expression | `.*"TargetSid":"(S-1-5-32-544|S-1-5-21-[0-9-]+-(512|518|519))".*` |

Os SIDs cobrem:

- `S-1-5-32-544`: grupo local interno Administrators;
- RID `512`: Domain Admins;
- RID `518`: Schema Admins;
- RID `519`: Enterprise Admins.

Considere modelos separados, com maior criticidade, para outros grupos. RIDs
comuns incluem `520` (Group Policy Creator Owners), `526` (Key Admins) e `527`
(Enterprise Key Admins). Os aliases internos incluem `548` a `551`. Grupos
customizados como `DnsAdmins` não têm SID universal fixo: consulte o SID no seu
domínio e inclua-o explicitamente.

## 6. Mostrar no alerta quem fez e o que foi alterado

Inclua **Message** nos Display Fields. O JSON informa:

| Campo JSON | Significado |
|---|---|
| `SubjectUserName` / `SubjectDomainName` | conta que executou a alteração |
| `MemberName` / `MemberSid` | conta ou principal adicionado ao grupo |
| `TargetUserName` / `TargetDomainName` | grupo que foi alterado |
| `TargetSid` | SID estável do grupo alterado |
| `Hostname` | servidor Windows que registrou o evento |
| `EventID` / `RecordId` | tipo do evento e identificador no log de origem |

Filtrar grupos privilegiados pelo `TargetSid` é mais confiável do que pelo nome
traduzido e funciona em instalações do Windows com idiomas diferentes.

## 7. Solução de problemas

Se o componente não aparecer no Model Editor:

1. Confirme que o teste do Custom Telemetry extrai `src`, `message` e `type`.
2. Salve o template.
3. Gere um evento real novo depois de salvar.
4. Confirme `Queued` e `Sent` no log do agente.
5. Pesquise pelo nome exato do template customizado, não por Palo Alto ou
   Security Integration.

Se o modelo não alertar, remova temporariamente os filtros de Message, use `> 0
em 1 minuto`, intervalo mínimo `1`, desative Auto Suppress e gere um evento
novo. Quando o componente-base alertar, recoloque um filtro de cada vez.
