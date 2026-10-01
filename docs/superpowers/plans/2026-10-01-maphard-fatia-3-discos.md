# Plano da fatia 3: discos

> **Para quem executa:** siga tarefa por tarefa, na ordem. Cada passo tem caixa de seleção (`- [ ]`). Cada tarefa termina com teste verde e commit. Leia o `AGENTS.md` antes de começar.

**Objetivo:** o MapHard responde as perguntas que o técnico faz sobre disco num atendimento: quais discos a máquina tem, de que tipo, por qual interface, quanto cabe e quanto sobra em cada volume, e, acima de tudo, se algum disco está no fim da vida. A saúde aparece grande, com o motivo escrito, e a tabela SMART completa fica logo abaixo.

**Arquitetura:** a mesma das fatias 1 e 2. Um módulo `discos/` busca os bytes de cada fonte (consultas de armazenamento, SMART ATA, log de saúde NVMe, volumes), intérpretes separados transformam os bytes em dados e o módulo `saude/` aplica as regras da seção 8 do desenho. O coletor, o JSON, o painel e a linha de comando ganham a seção Discos. Entra também o botão "Ler como administrador", porque o SMART de disco SATA só sai com elevação.

**Tecnologia:** C# com .NET 8, xUnit. P/Invoke por `LibraryImport` para `CreateFileW`, `DeviceIoControl`, `SetupDiGetClassDevsW` e as funções de volume. Nenhum pacote NuGet novo.

Desenho: `docs/superpowers/specs/2026-09-30-maphard-design.md`. Requisitos desta fatia: R20 a R26 e as regras de disco da seção 8. O cartão Discos do Resumo (R1) fica na fatia 6, como está no desenho; esta fatia já entrega o estado de saúde de cada disco que o cartão vai usar.

## Restrições globais

- As mesmas das fatias 1 e 2. Nesta fatia pesam as regras do produto 1 (só leitura), 2 (estados de campo), 4 (administrador), 6 (tabelas embutidas) e 7 (o disco só é lido pelos comandos SMART).
- **Só leitura de verdade.** Nenhum comando que grave no disco, que dispare autoteste ou que mude configuração do SMART. Os únicos comandos ATA permitidos são IDENTIFY DEVICE (0xEC), SMART READ DATA (0xB0/0xD0), SMART READ THRESHOLDS (0xB0/0xD1) e SMART RETURN STATUS (0xB0/0xDA). O teste `ComandosAtaPermitidosTestes` confere a lista.
- **Nunca saúde Bom sem SMART lido.** Disco sem SMART fica Desconhecido, com o motivo (R26).
- Amostras de teste montadas à mão: tabela de atributos, página de limites e log NVMe com valores fictícios e números de série `SERIE-DISCO-0001`.
- Formato brasileiro nos destaques do R24: "12.345 horas, 514 dias", "38,2 TB gravados", "42 °C".

## O que dá para ler sem administrador

Conferido em 01/10/2026 numa máquina com SSD NVMe e Windows 11, como usuário comum, abrindo `\\.\PhysicalDrive0` com acesso 0:

| Leitura | Sem administrador |
|---|---|
| Descritor do dispositivo (modelo, firmware, número de série, barramento) | Sim |
| Penalidade de busca (SSD ou HDD) e TRIM | Sim |
| Tamanho (`IOCTL_DISK_GET_DRIVE_GEOMETRY_EX`) | Sim |
| Estilo de partição (`IOCTL_DISK_GET_DRIVE_LAYOUT_EX`) | Sim |
| Disco e partição de um volume (`IOCTL_STORAGE_GET_DEVICE_NUMBER` em `\\.\C:`) | Sim |
| Log de saúde NVMe (página 02h, pela consulta de propriedade) | Sim |
| Abrir o disco para leitura e gravação, exigido pelo SMART ATA | Não (erro 5, acesso negado) |

Consequência: em disco NVMe, a saúde sai como usuário comum. Em disco SATA, a tabela SMART e a saúde ficam "requer administrador" até o técnico usar o botão "Ler como administrador".

## Fontes

| Dado | Fonte | Onde foi conferido |
|---|---|---|
| Códigos dos IOCTLs e a macro `CTL_CODE` | SDK do Windows: `devioctl.h`, `ntddstor.h`, `ntdddisk.h`, `ntddscsi.h` | Cópia do repositório `microsoft/win32metadata`, `generation/WinSDK/RecompiledIdlHeaders/shared` |
| `STORAGE_PROPERTY_QUERY`, `STORAGE_DEVICE_DESCRIPTOR`, `DEVICE_SEEK_PENALTY_DESCRIPTOR`, `DEVICE_TRIM_DESCRIPTOR`, `STORAGE_PROTOCOL_SPECIFIC_DATA`, `STORAGE_DEVICE_NUMBER`, `DISK_GEOMETRY_EX`, `DRIVE_LAYOUT_INFORMATION_EX` | learn.microsoft.com, winioctl.h | Páginas de cada estrutura, lidas em 01/10/2026 |
| Log de saúde NVMe e o modo de pedir | "Working with NVMe drives" e `NVME_HEALTH_INFO_LOG` (nvme.h) | learn.microsoft.com, lido em 01/10/2026; `NVME_LOG_PAGE_HEALTH_INFO = 0x02` no `nvme.h` do SDK |
| `ATA_PASS_THROUGH_EX`, `IOCTL_ATA_PASS_THROUGH` e a frase "não exige administrador, mas exige acesso de leitura e gravação ao dispositivo" | learn.microsoft.com, ntddscsi.h | Lido em 01/10/2026 |
| `ID_CMD = 0xEC`, `SMART_CMD = 0xB0`, `READ_ATTRIBUTES = 0xD0`, `READ_THRESHOLDS = 0xD1`, `RETURN_SMART_STATUS = 0xDA`, `SMART_CYL_LOW = 0x4F`, `SMART_CYL_HI = 0xC2` | `ntdddisk.h` do SDK | `microsoft/win32metadata` |
| Formato da tabela de atributos (30 itens de 12 bytes a partir do byte 2: id, flags de 2 bytes, atual, pior, bruto de 6 bytes, reservado) e da página de limites (id, limite, 10 reservados) | smartmontools, `include/smartmon/ata.h` (GPL-2.0) | Repositório oficial `smartmontools/smartmontools` |
| Resposta do SMART RETURN STATUS: 0x4F/0xC2 bom, 0xF4/0x2C falha prevista | smartmontools, `ata.h` e `ata_get_smart_status` em `lib/atacmds.cpp` | Idem |
| Nomes dos atributos | smartmontools, entrada `DEFAULT` do `drivedb/drivedb.h` | Idem |
| Rotação do HDD (palavra 217 do IDENTIFY) e velocidade SATA (palavras 76 e 77) | Especificação ATA (ACS), pela leitura do smartmontools | [CONFERIR] no `lib/ataprint.cpp` do smartmontools antes da tarefa 4 |
| Geração e linhas do PCIe do NVMe | Propriedades `DEVPKEY_PciDevice_CurrentLinkSpeed` e `DEVPKEY_PciDevice_CurrentLinkWidth` do controlador pai, pela SetupAPI | [CONFERIR] no `pciprop.h` do SDK e no learn.microsoft.com antes da tarefa 1 |
| BitLocker por volume | Classe WMI `Win32_EncryptableVolume`, com administrador | [CONFERIR] no learn.microsoft.com antes da tarefa 2 |

Valores que o plano usa, conferidos no SDK:

| Nome | Valor |
|---|---|
| `IOCTL_STORAGE_QUERY_PROPERTY` | 0x002D1400 |
| `IOCTL_STORAGE_GET_DEVICE_NUMBER` | 0x002D1080 |
| `IOCTL_DISK_GET_DRIVE_GEOMETRY_EX` | 0x000700A0 |
| `IOCTL_DISK_GET_DRIVE_LAYOUT_EX` | 0x00070050 |
| `IOCTL_ATA_PASS_THROUGH` | 0x0004D02C |
| `SMART_RCV_DRIVE_DATA` | 0x0007C088 |
| `StorageDeviceProperty`, `StorageAdapterProperty`, `StorageDeviceSeekPenaltyProperty`, `StorageDeviceTrimProperty`, `StorageDeviceProtocolSpecificProperty` | 0, 1, 7, 8, 50 |
| `BusTypeAta`, `BusTypeUsb`, `BusTypeRAID`, `BusTypeSas`, `BusTypeSata`, `BusTypeNvme` | 3, 7, 8, 10, 11, 17 |
| `ProtocolTypeNvme`, `NVMeDataTypeLogPage` | 3, 2 |
| `ATA_FLAGS_DRDY_REQUIRED`, `ATA_FLAGS_DATA_IN` | 1, 2 |
| `GUID_DEVINTERFACE_DISK` | 53f56307-b6bf-11d0-94f2-00a0c91efb8b |
| Estilo de partição | 0 MBR, 1 GPT, 2 RAW |

Unidades do log NVMe, pela página do `NVME_HEALTH_INFO_LOG`: temperatura em kelvin; reserva disponível e limite em porcentagem; percentual usado pode passar de 100; dados lidos e gravados em milhares de unidades de 512 bytes (1 = 512.000 bytes); horas ligado sem contar estados de baixo consumo.

## Mapa de arquivos

| Arquivo | Responsabilidade |
|---|---|
| `src/maphard.nucleo/discos/fonte-discos.cs` | `IFonteDiscos` e a leitura real: enumeração, consultas de propriedade, IOCTLs, SMART ATA e log NVMe |
| `src/maphard.nucleo/discos/descritor-armazenamento.cs` | Interpretação do descritor, da penalidade de busca, do TRIM, da geometria e do layout |
| `src/maphard.nucleo/discos/volumes.cs` | Volumes de cada disco |
| `src/maphard.nucleo/smart/smart-ata.cs` | Tabela de atributos, limites, IDENTIFY e status |
| `src/maphard.nucleo/smart/saude-nvme.cs` | Log de saúde NVMe |
| `src/maphard.nucleo/tabelas/atributos-smart.csv` e `atributos-smart.cs` | Nomes dos atributos em português |
| `src/maphard.nucleo/saude/regras-disco.cs` | Regras da seção 8 para ATA e NVMe |
| `src/maphard.nucleo/discos/leitor-discos.cs` | Seção Discos: junta tudo, com estado em cada campo |
| `src/maphard.nucleo/coleta/*`, `relatorios/*`, `painel/*`, `linha-de-comando/*`, `formatacao/*` | A seção Discos em cada saída |
| `src/maphard/*` | O botão "Ler como administrador" e o argumento `--elevado` |
| `ferramentas/gerar-atributos-smart.ps1` | Gera a tabela de atributos a partir do `drivedb.h` |
| `testes/maphard.testes/discos-*.cs`, `smart-*.cs`, `saude-*.cs` | Testes |

---

### Tarefa 1: lista de discos e dados básicos (R20, sem administrador)

**Interfaces produzidas:**

```csharp
public enum TipoDisco { Hdd, SsdSata, SsdNvme, Usb, Raid, Outro }

public sealed record DiscoBruto(
    int Numero,                 // N de \\.\PhysicalDriveN
    byte[]? Descritor,          // STORAGE_DEVICE_DESCRIPTOR
    bool? PenalidadeBusca,      // null: consulta falhou
    bool? Trim,
    long? TamanhoBytes,
    int? EstiloParticao,        // 0 MBR, 1 GPT, 2 RAW
    string? LinkPcie);          // "PCIe 4.0 x4", só NVMe

public interface IFonteDiscos
{
    IReadOnlyList<DiscoBruto> Discos();
    IReadOnlyList<VolumeBruto> Volumes();                 // tarefa 2
    byte[]? LogSaudeNvme(int numero);                     // tarefa 3
    LeituraSmartAta? SmartAta(int numero);                // tarefa 4
}
```

- [ ] **Passo 1:** conferir as propriedades de PCIe no `pciprop.h` e no learn.microsoft.com e anotar a fonte no comentário. Se não houver como ler sem administrador, o campo fica "não informado" e a pendência vai para `pendencias.md`.
- [ ] **Passo 2:** testes do intérprete do descritor com buffers montados à mão: modelo, firmware e número de série pelos deslocamentos; deslocamento 0 vira nulo; deslocamento fora do buffer vira nulo sem exceção; texto com espaços nas pontas sai aparado; barramento NVMe com penalidade nula dá `SsdNvme`; SATA sem penalidade dá `SsdSata`; SATA com penalidade dá `Hdd`; USB dá `Usb` mesmo com penalidade; RAID dá `Raid`.
- [ ] **Passo 3:** implementar a enumeração pela SetupAPI (`GUID_DEVINTERFACE_DISK`), com o número de cada disco pelo `IOCTL_STORAGE_GET_DEVICE_NUMBER`, e as consultas com o disco aberto em acesso 0. Disco que some entre a enumeração e a consulta não derruba a lista.
- [ ] **Passo 4:** teste `[FatoWindows]`: a máquina real tem ao menos um disco com tamanho e modelo.
- [ ] **Passo 5:** testes verdes, commit.

### Tarefa 2: volumes (R21)

```csharp
public sealed record VolumeBruto(string Letra, string? Rotulo, string? SistemaArquivos,
    long? LivreBytes, long? TotalBytes, int? Disco, bool? BitLocker /* null sem administrador */);
```

- [ ] **Passo 1:** conferir `Win32_EncryptableVolume` no learn.microsoft.com (namespace, propriedade `ProtectionStatus` e seus valores) e anotar a fonte. Sem administrador, o BitLocker fica "requer administrador".
- [ ] **Passo 2:** testes: volume ligado ao disco pelo número; volume sem disco (unidade de rede, mapeada) não entra; volume que ocupa dois discos (`IOCTL_STORAGE_GET_DEVICE_NUMBER` falha) entra no primeiro e leva o motivo; espaço livre e total formatados.
- [ ] **Passo 3:** implementar com `GetLogicalDriveStringsW`, `GetDriveTypeW` (só fixos e removíveis), `GetVolumeInformationW` e `GetDiskFreeSpaceExW`.
- [ ] **Passo 4:** teste `[FatoWindows]`: a unidade do Windows aparece ligada a um disco.
- [ ] **Passo 5:** testes verdes, commit.

### Tarefa 3: saúde NVMe (R23, sem administrador)

```csharp
public sealed record SaudeNvme(
    byte AlertaCritico,            // bits 0 a 4
    int TemperaturaC,              // kelvin - 273
    int ReservaDisponivel, int LimiteReserva, int PercentualUsado,
    decimal DadosLidosBytes, decimal DadosGravadosBytes,   // unidades x 512.000
    decimal CiclosEnergia, decimal HorasLigado, decimal DesligamentosInseguros,
    decimal ErrosMidia, decimal EntradasRegistroErros,
    int? LimiteAvisoTemperaturaC);  // WCTEMP do IDENTIFY do controlador, quando houver
```

- [ ] **Passo 1:** testes com um log de 512 bytes montado à mão: cada campo no deslocamento da página do `NVME_HEALTH_INFO_LOG`; contadores de 16 bytes maiores que `ulong` sem estouro; temperatura 315 K dá 42 °C; temperatura 0 vira nulo; buffer curto vira nulo; cada bit do alerta crítico vira o texto certo ("reserva abaixo do limite", "temperatura fora da faixa", "confiabilidade degradada", "mídia só de leitura", "falha na memória de reserva volátil").
- [ ] **Passo 2:** conferir no `nvme.h` do SDK o deslocamento do WCTEMP na estrutura de IDENTIFY do controlador e o `NVMeDataTypeIdentify` com CNS 1. Sem a leitura, o limite de aviso fica nulo e a regra de temperatura do NVMe não dispara.
- [ ] **Passo 3:** implementar a leitura com `StorageDeviceProtocolSpecificProperty`, como no exemplo da Microsoft, e o intérprete.
- [ ] **Passo 4:** teste `[FatoWindows]`: em disco NVMe real, a temperatura fica entre 0 e 100 °C e o percentual usado é lido.
- [ ] **Passo 5:** testes verdes, commit.

### Tarefa 4: SMART ATA (R22, com administrador)

```csharp
public sealed record AtributoSmart(byte Id, ushort Flags, byte Atual, byte Pior, byte? Limite, ulong Bruto);

public sealed record LeituraSmartAta(
    IReadOnlyList<AtributoSmart> Atributos,
    bool? FalhaPrevista,         // SMART RETURN STATUS: 0xF4/0x2C sim, 0x4F/0xC2 não
    int? RotacaoRpm,             // palavra 217: 1 SSD, 0x0401 a 0xFFFE rpm
    string? VelocidadeSata);     // "SATA 6 Gb/s"
```

- [ ] **Passo 1:** conferir no `lib/ataprint.cpp` do smartmontools as palavras 76, 77 e 217 do IDENTIFY e anotar a fonte.
- [ ] **Passo 2:** testes com a tabela de 512 bytes montada à mão: 30 itens, id 0 ignorado; bruto de 6 bytes em little-endian; limite casado pelo id; tabela sem limites deixa o limite nulo; soma de verificação errada vai no motivo e não descarta a leitura; status 0xF4/0x2C dá falha prevista; resposta incompleta dá nulo.
- [ ] **Passo 3:** teste `ComandosAtaPermitidosTestes`: a fonte real só monta os quatro comandos permitidos.
- [ ] **Passo 4:** implementar com `IOCTL_ATA_PASS_THROUGH`, com o disco aberto em leitura e gravação. Sem administrador, a abertura falha com acesso negado e a leitura volta "requer administrador", sem tentar de novo. Disco atrás de ponte USB ou de controladora RAID que não responde volta "SMART indisponível por esta controladora".
- [ ] **Passo 5:** testes verdes, commit.

### Tarefa 5: nomes dos atributos em português

Formato da tabela, com a fonte no cabeçalho e em cada linha:

```
id;nome_original;nome;tipo_disco;fonte
```

- [ ] **Passo 1:** roteiro `ferramentas/gerar-atributos-smart.ps1` que baixa o `drivedb.h` do repositório oficial do smartmontools pela API do GitHub e extrai a entrada `DEFAULT` (id, nome original e se vale para HDD ou SSD). A coluna `nome` em português é preenchida à mão, uma vez, e o roteiro preserva o que já estiver traduzido.
- [ ] **Passo 2:** tradução dos nomes, pela `humanizar-ptbr`. Exemplos de padrão: "Setores realocados" (05h), "Horas ligado" (09h), "Ciclos de energia" (0Ch), "Temperatura" (C2h), "Setores pendentes" (C5h), "Setores incorrigíveis" (C6h), "Erros de CRC na interface" (C7h).
- [ ] **Passo 3:** testes: id conhecido dá o nome em português e o original na dica; id fora da tabela dá "atributo do fabricante (0xNN)"; integridade da tabela (sem id repetido, fonte em toda linha, nome em toda linha).
- [ ] **Passo 4:** testes verdes, commit do roteiro e da tabela juntos.

### Tarefa 6: regras de saúde (R25, R26 e seção 8)

Valores da seção 8 do desenho, que o Manfred pode ajustar:

| Disco | Estado | Quando |
|---|---|---|
| ATA | Ruim | Algum atributo com valor atual igual ou abaixo do limite do fabricante (limite diferente de zero), ou falha prevista pelo próprio disco |
| ATA | Atenção | 05h, C4h, C5h ou C6h com bruto acima de zero. Vida restante informada pelo SSD abaixo de 10% (valor atual dos atributos E7h ou E9h, quando existirem). Temperatura acima de 50 °C em HDD ou 70 °C em SSD (C2h, ou BEh quando não houver C2h) |
| NVMe | Ruim | Qualquer bit do alerta crítico |
| NVMe | Atenção | Percentual usado igual ou acima de 90. Erros de mídia acima de zero. Reserva a menos de 10 pontos do limite. Temperatura acima do limite de aviso do próprio disco |
| Qualquer | Bom | Nenhuma das anteriores, com SMART lido |
| Qualquer | Desconhecido | SMART não lido, com o motivo: "requer administrador" ou "SMART indisponível por esta controladora" |

```csharp
public enum EstadoSaude { Bom, Atencao, Ruim, Desconhecido }
public sealed record SaudeDisco(EstadoSaude Estado, IReadOnlyList<string> Motivos);
```

- [ ] **Passo 1:** um teste por linha do quadro, nos dois sentidos, com o motivo escrito ("3 setores realocados", "92% da vida útil usada", "temperatura de 55 °C"). Ruim ganha de Atenção; os motivos dos dois aparecem.
- [ ] **Passo 2:** testes do R26: disco USB sem SMART fica Desconhecido com "SMART indisponível por esta controladora", nunca Bom.
- [ ] **Passo 3:** implementar em `saude/regras-disco.cs`.
- [ ] **Passo 4:** testes verdes, commit.

### Tarefa 7: "Ler como administrador"

Segue o desenho (seção 3) e a decisão do Manfred de 01/10/2026 na pergunta 2: o programa abre como usuário comum, e o botão reabre o programa elevado. O manifesto continua `asInvoker`.

- [ ] **Passo 1:** argumento `--elevado` na leitura dos argumentos, com teste: só vale para a janela, como o `--demonstracao`.
- [ ] **Passo 2:** botão na faixa do topo, visível só quando o programa não está elevado. Ele reabre o próprio `.exe` com o verbo `runas` e `--elevado`, e fecha a janela atual quando a nova abre. Se o técnico recusar o pedido do Windows, a janela atual continua, com a mensagem "Leitura como administrador cancelada".
- [ ] **Passo 3:** a barra de status já diz "usuário comum" ou "administrador"; teste do texto com o argumento.
- [ ] **Passo 4:** testes verdes, commit.

### Tarefa 8: saídas

```csharp
public sealed record DiscoTela(
    Campo<string> Modelo, Campo<string> Firmware, Campo<string> NumeroSerie,
    Campo<long> Tamanho, Campo<string> Tipo, Campo<string> Interface,
    Campo<int> Rotacao, Campo<bool> Trim, Campo<string> EstiloParticao,
    SaudeDisco Saude,
    Campo<int> Temperatura, Campo<long> HorasLigado, Campo<long> CiclosEnergia, Campo<decimal> DadosGravadosBytes,
    Campo<IReadOnlyList<LinhaSmart>> Smart,      // ATA: a tabela; NVMe: os campos do log
    IReadOnlyList<VolumeTela> Volumes);

public sealed record SecaoDiscos(Campo<IReadOnlyList<DiscoTela>> Discos);

// Uma linha da tabela SMART. Em NVMe, Id é o nome do campo do log e Atual, Pior e Limite ficam nulos.
public sealed record LinhaSmart(string Id, string Nome, string NomeOriginal, int? Atual, int? Pior, int? Limite, decimal Bruto);

public sealed record VolumeTela(
    Campo<string> Letra, Campo<string> Rotulo, Campo<string> SistemaArquivos,
    Campo<long> Livre, Campo<long> Total, Campo<bool> BitLocker);
```

- [ ] **Passo 1:** formatador: `Formatador.Horas(12345)` dá "12.345 horas, 514 dias"; `Formatador.Gravados(38.2e12)` dá "38,2 TB gravados"; temperatura "42 °C". Testes.
- [ ] **Passo 2:** coletor e JSON: a coleta ganha `Discos`, cada fonte com tempo limite; o SMART de cada disco tem o próprio tempo limite, para um disco lento não travar os outros. A versão do formato do JSON passa a 3. Teste de ida e volta.
- [ ] **Passo 3:** identificação: a linha "Discos" com a capacidade de cada um ("SSD NVMe 1 TB, HDD 2 TB").
- [ ] **Passo 4:** painel: seção **Discos**, entre Memória e Placa-mãe, com um cartão por disco: a saúde grande no alto, com o motivo; temperatura, horas e total gravado ao lado; dados do disco; volumes; e a tabela SMART embaixo (ID, nome, atual, pior, limite, bruto). Disco SATA sem elevação mostra "requer administrador" e o botão.
- [ ] **Passo 5:** linha de comando: uma linha por disco, "Disco 0: SSD NVMe 1 TB, Bom, 42 °C, 3% da vida usada", e os motivos de Atenção e Ruim.
- [ ] **Passo 6:** demonstração com dois discos fictícios, um Bom e um em Atenção, para a imagem mostrar os dois.
- [ ] **Passo 7:** testes do painel e da linha de comando, testes verdes, commit.

### Tarefa 9: fechamento

- [ ] **Passo 1:** README com a seção Discos e o botão no "Uso"; na "Situação do projeto", a fatia 2 marcada como concluída em 01/10/2026 e a linha da fatia 3.
- [ ] **Passo 2:** pendências atualizadas.
- [ ] **Passo 3:** portões, envio, Pull Request do código e leitura do CI até ficar verde.

## Como o Manfred testa

1. Baixar o `maphard.exe` do PR, ou gerar com `ferramentas\publicar.cmd` em `C:\COWORK\CODE\MAPHARD-MT`.
2. Como usuário comum, numa máquina com SSD NVMe: conferir modelo, tamanho, interface, temperatura, percentual usado e horas com o programa de referência de saúde de disco.
3. Numa máquina com disco SATA (HDD ou SSD): conferir que a tabela SMART aparece como "requer administrador"; usar "Ler como administrador" e comparar a tabela com o programa de referência, atributo por atributo.
4. Ligar um disco externo USB e conferir que ele aparece com "SMART indisponível por esta controladora" quando a ponte não repassa o SMART, e nunca com saúde Bom.
5. Rodar `maphard coletar --json` como usuário comum e como administrador e conferir a seção `discos`.

## Riscos

| Risco | O que fazer |
|---|---|
| Ponte USB que trava ou demora com comando ATA | Tempo limite por disco; a ponte que não responde vira "SMART indisponível por esta controladora" |
| Controladora RAID que esconde os discos físicos | O disco lógico aparece com os dados básicos e o aviso do R26 |
| Atributo com significado próprio do fabricante (C2h com mínimo e máximo no bruto, E7h e E9h variando) | A temperatura lê só o byte baixo do bruto; a regra de vida do SSD diz "informada pelo SSD" e usa o valor atual, não o bruto |
| Antivírus que estranha a abertura do disco em leitura e gravação | Só com administrador e só no comando de SMART; o programa nunca grava. A página do programa explica |
| Disco que entra em espera | A consulta de propriedade não acorda o disco; o SMART só roda quando o técnico pede, ou na coleta com administrador |
