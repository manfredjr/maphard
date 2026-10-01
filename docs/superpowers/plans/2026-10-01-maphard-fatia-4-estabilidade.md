# Plano da fatia 4: estabilidade, erros de memória, dispositivos e chipset

> **Para quem executa:** siga tarefa por tarefa, na ordem. Cada passo tem caixa de seleção (`- [ ]`). Cada tarefa termina com teste verde e commit. Leia o `AGENTS.md` antes de começar.

**Objetivo:** o MapHard responde as perguntas que o técnico faz quando o cliente diz "o computador trava" ou "desliga sozinho": quantas telas azuis e desligamentos inesperados houve, se o disco está registrando erro, se o hardware registrou erro corrigido ou não corrigido, se o Diagnóstico de Memória achou algo, quão estável o Windows está, há quanto tempo a máquina está ligada, qual dispositivo está sem driver ou com problema, e qual é o chipset.

**Arquitetura:** a mesma das fatias anteriores. Um módulo `eventos/` lê o log Sistema pela API de eventos do Windows (wevtapi), um intérprete transforma o XML de cada evento em dados e as regras agrupam e contam. Um módulo `dispositivos/` lê o estado de todos os dispositivos e o chipset pela SetupAPI. O índice de estabilidade, a data de instalação e o último boot vêm do WMI, pelo mesmo moniker já usado no BitLocker. As regras de saúde da seção 8 ganham as linhas de Estabilidade, Memória e Dispositivos.

**Tecnologia:** C# com .NET 8, xUnit. P/Invoke por `LibraryImport` para `wevtapi.dll`, `setupapi.dll`, `cfgmgr32.dll` e `GetTickCount64`. Nenhum pacote NuGet novo.

Desenho: `docs/superpowers/specs/2026-09-30-maphard-design.md`. Requisitos desta fatia: R15, R27 a R30 e o chipset do R16, como está na seção 12 do desenho. O cartão Estabilidade do Resumo (R1) fica na fatia 6; esta fatia já entrega o estado que o cartão vai usar.

## Restrições globais

- As mesmas das fatias anteriores. Nesta fatia pesam as regras do produto 1 (só leitura), 2 (estados de campo), 4 (administrador) e 6 (tabelas embutidas).
- **Só leitura.** O MapHard lê o log Sistema e o estado dos dispositivos; não limpa log, não reinstala driver, não habilita nem desabilita dispositivo.
- **Número de evento sem fonte oficial não decide gravidade.** Os eventos do WHEA e do Diagnóstico de Memória só têm o número em respostas de fórum. Para eles, a gravidade vem do nível que o próprio evento traz (crítico, erro, aviso, informação) e a tela mostra a mensagem do Windows como ela é. Os números ficam só como filtro de provedor e marcados com [CONFERIR] até aparecerem num evento real.
- **Evento de disco aponta para o disco daquela hora.** O texto `\Device\HarddiskN` traz o número que o disco tinha quando o evento foi registrado; o disco pode ter sido retirado ou ter mudado de número. A tela agrupa por esse texto e nunca atribui o evento ao disco que hoje tem o mesmo número.
- Amostras de teste montadas à mão: XML de eventos com valores fictícios, sem nome de computador nem usuário reais.

## O que dá para ler sem administrador

Conferido em 01/10/2026 numa máquina com Windows 11, como usuário comum:

| Leitura | Sem administrador |
|---|---|
| Log Sistema (`Get-WinEvent -LogName System`) | Sim |
| Índice de estabilidade (`Win32_ReliabilityStabilityMetrics`) | Sim |
| Estado dos dispositivos (`Win32_PnPEntity.ConfigManagerErrorCode`) | Sim |
| IDs de hardware da ponte do processador (classe 0600) e do controlador LPC/eSPI (classe 0601) | Sim |

Nenhuma leitura desta fatia exige administrador. Se alguma falhar por permissão numa máquina de domínio, o campo fica "requer administrador".

## Fontes

| Dado | Fonte | Onde foi conferido |
|---|---|---|
| Kernel-Power 41: desligamento sem encerramento limpo, campos `BugcheckCode` (decimal), `PowerButtonTimestamp` e os demais | "Advanced troubleshooting for Event ID 41" | learn.microsoft.com, troubleshoot/windows-client/performance/event-id-41-restart, lido em 01/10/2026 |
| 6008 (desligamento anterior inesperado), 6006 (encerramento limpo), 1074 (desligamento pedido) | Mesma página | Idem. O provedor do 6008 não aparece na página: [CONFERIR] num evento real |
| Disco: 51 (erro durante paginação, fonte `Disk`), 153 (repetição de E/S), 157 (disco removido de surpresa) | "Event ID 51" e "Troubleshoot data corruption and disk errors" | learn.microsoft.com, lido em 01/10/2026 |
| Disco: 7 (bloco defeituoso) e 11 (erro de controladora) | Resposta do suporte da Microsoft no Q&A | [CONFERIR] |
| NTFS: 50, 55, 98, 140 como corrupção do sistema de arquivos | "Troubleshoot data corruption and disk errors" | learn.microsoft.com. O provedor (`Microsoft-Windows-Ntfs`) não aparece na página: [CONFERIR] |
| Tela azul: provedor `Microsoft-Windows-WER-SystemErrorReporting`, evento 1001 | Só em resposta do Q&A | [CONFERIR]. A contagem oficial vem do Kernel-Power 41 com `BugcheckCode` diferente de zero |
| WHEA: provedor `Microsoft-Windows-WHEA-Logger` | Só em respostas do Q&A (17, 18, 19, 47, 1) | [CONFERIR]. Gravidade pelo nível do evento |
| Diagnóstico de Memória: provedor `Microsoft-Windows-MemoryDiagnostics-Results` | Só em respostas do Q&A (1101, 1201 sem erro) | [CONFERIR]. Resultado pelo nível do evento e pela mensagem |
| Níveis: 1 crítico, 2 erro, 3 aviso, 4 informação | `winmeta.h` do SDK (`WINEVENT_LEVEL_*`) | Cópia do `microsoft/win32metadata` |
| `EvtQuery`, `EvtNext`, `EvtRender`, `EvtFormatMessage`, `EvtClose`; `EvtQueryChannelPath` 0x1, `EvtQueryReverseDirection` 0x200, `EvtRenderEventXml` 1, `EvtFormatMessageEvent` 1; consulta XPath com `TimeCreated[timediff(@SystemTime) <= N]` em milissegundos | learn.microsoft.com (winevt.h, "Consuming events") e `winevt.h` do SDK | Lido em 01/10/2026 |
| `Win32_ReliabilityStabilityMetrics`: `SystemStabilityIndex` de 1 a 10, `TimeGenerated`; política "Configure Reliability WMI Providers" ligada por padrão no Windows cliente e desligada no Server | learn.microsoft.com (previous-versions, racwmiprov) | Lido em 01/10/2026 |
| Data de instalação e último boot: `Win32_OperatingSystem.InstallDate` e `LastBootUpTime` | MicrosoftDocs/win32, win32-operatingsystem.md | Lido em 01/10/2026 |
| Tempo ligado: `GetTickCount64`, em milissegundos desde o boot | learn.microsoft.com, sysinfoapi | Lido em 01/10/2026 |
| Códigos de problema de dispositivo (1 a 57) e a mensagem de cada um | "Device Manager error messages" | learn.microsoft.com, windows-hardware/drivers/install/device-manager-error-messages |
| `DEVPKEY_Device_ProblemCode` (UINT32, 0 sem problema), `DEVPKEY_Device_FriendlyName`, `DEVPKEY_Device_DeviceDesc`, `DEVPKEY_Device_Class`, `DEVPKEY_Device_HardwareIds`, `DEVPKEY_Device_DriverVersion` | `devpkey.h` do SDK; "Retrieving the status and problem code for a device instance" | `microsoft/win32metadata`; learn.microsoft.com |
| `DIGCF_ALLCLASSES` 0x4, `DIGCF_PRESENT` 0x2 | `SetupAPI.h` do SDK | `microsoft/win32metadata` |
| Classe PCI 06 (ponte), subclasse 00 (ponte do processador) e 01 (ponte ISA, onde fica o LPC/eSPI do chipset); nomes de dispositivo | `pci.ids` do pciutils (GPL-2.0 ou posterior, ou BSD de 3 cláusulas) | Repositório `pciutils/pciids` no GitHub |

## Mapa de arquivos

| Arquivo | Responsabilidade |
|---|---|
| `src/maphard.nucleo/eventos/fonte-eventos.cs` | `IFonteEventos` e a leitura real pela wevtapi |
| `src/maphard.nucleo/eventos/evento.cs` | Intérprete do XML de um evento |
| `src/maphard.nucleo/eventos/estabilidade.cs` | Grupos e contagem do R27, índice do R28, tempo ligado do R29 |
| `src/maphard.nucleo/dispositivos/fonte-dispositivos.cs` | `IFonteDispositivos` e a leitura real pela SetupAPI |
| `src/maphard.nucleo/dispositivos/problemas.cs` | Dispositivos com problema (R30) |
| `src/maphard.nucleo/dispositivos/chipset.cs` | Chipset pela ponte do processador e pelo LPC/eSPI |
| `src/maphard.nucleo/tabelas/problemas-dispositivo.csv` e `.cs` | Código, constante, texto em português e fonte |
| `src/maphard.nucleo/tabelas/chipsets.csv` e `.cs` | Subconjunto do `pci.ids` |
| `ferramentas/gerar-chipsets.ps1` | Gera `chipsets.csv` a partir do `pci.ids` |
| `src/maphard.nucleo/saude/regras-estabilidade.cs` | Linhas da seção 8 de Estabilidade, Memória e Dispositivos |
| `src/maphard.nucleo/coleta/*`, `relatorios/*`, `painel/*`, `linha-de-comando/*` | As seções novas em cada saída |
| `testes/maphard.testes/eventos-*.cs`, `dispositivos-*.cs`, `saude-estabilidade-testes.cs` | Testes |

---

### Tarefa 1: leitura do log Sistema

**Interfaces produzidas:**

```csharp
public sealed record EventoSistema(
    string Provedor, int Id, int Nivel, DateTimeOffset Momento,
    IReadOnlyDictionary<string, string> Dados,   // EventData, por nome
    string? Mensagem);                           // EvtFormatMessage, no idioma do Windows

public interface IFonteEventos
{
    /// <summary>XML de cada evento dos provedores pedidos nos últimos <paramref name="dias"/> dias, do mais novo para o mais antigo.</summary>
    IReadOnlyList<(string Xml, string? Mensagem)> Ler(IReadOnlyList<string> provedores, int dias, int limite);
}
```

- [x] **Passo 1:** testes do intérprete com XML montado à mão: provedor, id, nível e momento da seção `System`; `EventData` com `Data Name=`; `EventData` sem nome (só posição) vira chave "1", "2"...; XML malformado vira nulo sem exceção; momento em UTC convertido sem perder o fuso.
- [x] **Passo 2:** teste da consulta XPath gerada: provedores no `Provider[@Name=...]` com `or`, tempo em milissegundos, e o limite de 20 expressões da página "Consuming events" respeitado (acima disso, uma consulta por grupo).
- [x] **Passo 3:** implementar a fonte real com `EvtQuery` (canal `System`, `EvtQueryChannelPath | EvtQueryReverseDirection`), `EvtNext` em blocos, `EvtRender` com `EvtRenderEventXml` e `EvtFormatMessage` com `EvtFormatMessageEvent`, fechando cada handle com `EvtClose` no mesmo thread. Limite de eventos por provedor para não pesar na máquina (regra do produto 7).
- [x] **Passo 4:** teste `[FatoWindows]`: a consulta do log Sistema roda sem administrador e devolve uma lista (pode ser vazia).
- [x] **Passo 5:** testes verdes, commit.

### Tarefa 2: estabilidade e erros (R15, R27 a R29)

```csharp
public sealed record GrupoEventos(string Codigo, string Titulo, int Quantidade, DateTimeOffset? Ultimo, IReadOnlyList<string> Detalhes);

public sealed record SecaoEstabilidade(
    int Dias,
    Campo<IReadOnlyList<GrupoEventos>> Grupos,   // telas azuis, desligamentos, disco, sistema de arquivos, WHEA corrigido, WHEA não corrigido
    Campo<string> DiagnosticoMemoria,            // última mensagem, com a data
    Campo<double> IndiceEstabilidade,
    Campo<TimeSpan> TempoLigado,
    Campo<DateTimeOffset> UltimoBoot,
    Campo<DateTimeOffset> InstalacaoWindows);
```

Grupos, com a fonte de cada um:

| Grupo | Eventos | Observação |
|---|---|---|
| Telas azuis | Kernel-Power 41 com `BugcheckCode` diferente de zero | Detalhe: o código em hexadecimal ("0x9F"), como a página da Microsoft ensina a converter. O 1001 do WER entra só para completar o código quando o 41 não tiver [CONFERIR] |
| Desligamentos inesperados | Kernel-Power 41 com `BugcheckCode` zero | Detalhe: "botão de energia pressionado" quando `PowerButtonTimestamp` não é zero; "possível problema de fonte de energia" quando os dois são zero, como diz a página |
| Erros de disco | `Disk` 7, 11, 51, 153, 157 | Agrupados pelo texto `\Device\HarddiskN` e com o aviso de que o número é o da hora do evento |
| Sistema de arquivos | NTFS 50, 55, 98, 140 | Detalhe: a mensagem do Windows |
| Erro de hardware corrigido | WHEA-Logger com nível 3 (aviso) | Detalhe: a mensagem do Windows |
| Erro de hardware não corrigido | WHEA-Logger com nível 1 ou 2 | Idem |

- [x] **Passo 1:** um teste por linha do quadro, com eventos montados à mão, nos dois sentidos (entra e não entra no grupo).
- [x] **Passo 2:** testes do Diagnóstico de Memória: sem evento no período, "nenhum teste no período"; evento de nível 4, "sem erros", com a data; nível 2 ou 3, a mensagem do Windows.
- [x] **Passo 3:** índice de estabilidade pelo valor mais recente de `Win32_ReliabilityStabilityMetrics`, arredondado a uma casa; sem valor (política desligada, Windows Server), "não disponível neste equipamento" com o motivo. Tempo ligado por `GetTickCount64`; último boot e instalação por `Win32_OperatingSystem`. Teste `[FatoWindows]` do índice entre 1 e 10.
- [x] **Passo 4:** período de 30 dias, e 90 a escolher: opção `--dias 90` na linha de comando e escolha na janela (tarefa 6).
- [x] **Passo 5:** testes verdes, commit.

### Tarefa 3: códigos de problema de dispositivo

Formato da tabela:

```
codigo;constante;texto;fonte
```

- [x] **Passo 1:** montar `problemas-dispositivo.csv` a partir da página "Device Manager error messages" e das páginas de cada código, com o texto em português passado pela `humanizar-ptbr` ("driver não instalado", "dispositivo desativado", "o Windows parou este dispositivo porque ele informou problemas"). Uma linha por código da página oficial.
- [x] **Passo 2:** testes: código conhecido dá o texto; código fora da tabela dá "problema código N"; integridade (sem código repetido, fonte em toda linha).
- [x] **Passo 3:** testes verdes, commit.

### Tarefa 4: dispositivos com problema (R30)

```csharp
public sealed record DispositivoBruto(string? Nome, string? Classe, IReadOnlyList<string> IdsHardware, uint? CodigoProblema, string? VersaoDriver);
public sealed record DispositivoProblema(Campo<string> Nome, Campo<string> Classe, int Codigo, string Texto);
```

- [x] **Passo 1:** testes: só entra dispositivo com código diferente de zero; nome pelo nome amigável, e pela descrição quando não houver; código 22 (desativado) entra, mas separado dos outros, porque costuma ser escolha do usuário; lista vazia é "nenhum dispositivo com problema", lido.
- [x] **Passo 2:** implementar a fonte real: `SetupDiGetClassDevsW` com `DIGCF_ALLCLASSES | DIGCF_PRESENT`, propriedades pelo `SetupDiGetDevicePropertyW` com as chaves do `devpkey.h`.
- [x] **Passo 3:** teste `[FatoWindows]`: a máquina real tem dispositivos e a leitura não falha.
- [x] **Passo 4:** testes verdes, commit.

### Tarefa 5: chipset (R16)

- [x] **Passo 1:** roteiro `ferramentas/gerar-chipsets.ps1` que baixa o `pci.ids` do repositório `pciutils/pciids` e grava em `chipsets.csv` os dispositivos da Intel (8086) e da AMD (1022) cujo nome indica ponte do processador, LPC, eSPI ou controlador do chipset, com a fonte e a licença no cabeçalho. Roteiro e tabela no mesmo commit.
- [x] **Passo 2:** testes: o dispositivo de classe `CC_0601` (LPC/eSPI) dá o chipset; sem ele, o de classe `CC_0600`; ID fora da tabela usa o nome que o Windows dá ao dispositivo, com a dica "fora da tabela do MapHard"; máquina virtual sem ponte conhecida fica "não informado".
- [x] **Passo 3:** a linha "Chipset" entra no cartão "Placa-mãe" da seção Placa-mãe e firmware.
- [x] **Passo 4:** testes verdes, commit.

### Tarefa 6: regras de saúde e saídas

Linhas da seção 8 do desenho, com valores que o Manfred pode ajustar:

| Cartão | Atenção | Ruim |
|---|---|---|
| Estabilidade | Um desligamento inesperado ou uma tela azul no período | Três ou mais telas azuis, ou erro de hardware não corrigido no WHEA |
| Memória | Módulos diferentes, abaixo da velocidade, canal único provável (fatia 2) | Erro no Diagnóstico de Memória. O erro de memória do WHEA entra quando o componente puder ser lido com fonte [CONFERIR] |
| Dispositivos | Algum dispositivo com problema | Nenhum caso |

- [x] **Passo 1:** um teste por linha, nos dois sentidos, com o motivo escrito ("2 telas azuis nos últimos 30 dias").
- [x] **Passo 2:** coletor e JSON: a coleta ganha `Estabilidade` e `Dispositivos`, cada fonte com tempo limite. Versão do formato 4. Teste de ida e volta.
- [x] **Passo 3:** painel: seções **Estabilidade** e **Dispositivos**, na ordem da navegação do desenho; cartão "Erros de memória" na seção Memória; linha "Chipset" na placa-mãe; escolha de 30 ou 90 dias na seção Estabilidade, que refaz só a leitura dos eventos.
- [x] **Passo 4:** linha de comando: "Estabilidade: 2 telas azuis, 1 desligamento inesperado em 30 dias; índice 6,2", "Dispositivos: 1 com problema", e a opção `--dias`.
- [x] **Passo 5:** demonstração com eventos e um dispositivo com problema fictícios.
- [x] **Passo 6:** testes do painel e da linha de comando, testes verdes, commit.

### Tarefa 7: fechamento

- [x] **Passo 1:** README com as seções novas no "Uso" e a linha da fatia 4 na "Situação do projeto".
- [x] **Passo 2:** pendências atualizadas, com cada [CONFERIR] que continuar aberto.
- [ ] **Passo 3:** portões, envio, Pull Request do código e leitura do CI até ficar verde.

## Como o Manfred testa

1. Baixar o `maphard.exe` do PR, ou gerar com `ferramentas\publicar.cmd`.
2. Na seção **Estabilidade**, comparar as contagens com o Visualizador de Eventos (log Sistema, filtrado pelos mesmos provedores) e o índice com o Monitor de Confiabilidade (`perfmon /rel`).
3. Numa máquina que já teve tela azul, conferir o código ("0x9F") com o Monitor de Confiabilidade.
4. Rodar o Diagnóstico de Memória do Windows numa máquina de teste e conferir o resultado no MapHard depois do reinício.
5. Na seção **Dispositivos**, comparar com o Gerenciador de Dispositivos, num computador com algum dispositivo sem driver.
6. Conferir o chipset com o programa de referência.

## Riscos

| Risco | O que fazer |
|---|---|
| Log Sistema muito grande ou lento | Consulta só dos provedores da fatia, com período e limite de eventos; tempo limite como as outras fontes |
| Número de evento que muda de versão do Windows ou de fabricante | A gravidade vem do nível do evento; a tela mostra a mensagem do Windows |
| Máquina de domínio com o log Sistema fechado | O grupo fica "requer administrador" ou "erro de leitura", com o motivo |
| Índice de estabilidade desligado (Windows Server, política) | "Não disponível neste equipamento", com o motivo |
| Dispositivo desativado de propósito | Código 22 aparece separado, sem pesar no cartão Dispositivos |
