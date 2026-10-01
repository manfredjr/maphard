# Plano da fatia 5: vídeo, monitores, bateria, rede, Windows e Windows 11

> **Para quem executa:** siga tarefa por tarefa, na ordem. Cada passo tem caixa de seleção (`- [ ]`). Cada tarefa termina com teste verde e commit. Leia o `AGENTS.md` antes de começar.

**Objetivo:** o MapHard responde as últimas perguntas do atendimento: qual placa de vídeo e quanta memória ela tem de verdade, que monitor está ligado, quanto a bateria já perdeu, quais placas de rede existem, qual Windows está instalado e se está ativado e, a pergunta que o cliente mais faz desde o fim do suporte ao Windows 10, se a máquina aceita o Windows 11, item por item.

**Arquitetura:** a mesma das fatias anteriores. Cada área tem a sua fonte, com a leitura real separada do intérprete: DXGI para o vídeo, EDID para os monitores, consulta direta à bateria, a API de rede do Windows e o licenciamento do Windows. A verificação do Windows 11 junta o que as fatias 1 a 3 já leem (TPM, UEFI, Secure Boot, memória, disco) com as listas de processadores da Microsoft, embutidas.

**Tecnologia:** C# com .NET 8, xUnit. P/Invoke por `LibraryImport` para `dxgi.dll`, `setupapi.dll`, `iphlpapi.dll` e `slwga.dll`, e `DeviceIoControl` para a bateria. Nenhum pacote NuGet novo.

Desenho: `docs/superpowers/specs/2026-09-30-maphard-design.md`. Requisitos desta fatia: R31 a R36, e as linhas de Windows 11 e Bateria da seção 8. O Resumo (R1) fica na fatia 6.

## Restrições globais

- As mesmas das fatias anteriores. Nesta fatia pesam as regras do produto 1 (só leitura), 2 (estados de campo), 6 (tabelas embutidas) e 9 (chave de produto nunca).
- **Chave de produto nunca.** A ativação sai do estado do licenciamento, sem ler nem pedir a chave, nem parcial.
- **Lista de processadores sem palpite.** Processador que não for achado na lista da Microsoft fica "não consta na lista do MapHard", e não "não aceita o Windows 11": a própria Microsoft diz que gerações mais novas que seguem os mesmos princípios contam como aceitas mesmo fora da lista.
- Amostras de teste montadas à mão: EDID do exemplo da Microsoft (`Monsamp.inf`, na página "Overriding monitor EDIDs") e blocos de bateria com valores fictícios.

## O que dá para ler sem administrador

Conferido em 01/10/2026 num notebook com Windows 11, como usuário comum:

| Leitura | Sem administrador | Observação |
|---|---|---|
| Placas de vídeo pela DXGI (nome, fabricante, memória dedicada) | Sim | A memória dedicada veio sem o limite de 4 GB que o WMI tem, o problema citado no R31. O adaptador de software da Microsoft vem com o sinal `DXGI_ADAPTER_FLAG_SOFTWARE` e fica de fora |
| EDID dos monitores no registro | Sim | O registro guarda também monitores que já foram desligados; a leitura vale só para os presentes |
| Bateria pela consulta direta (`IOCTL_BATTERY_*`) | Sim | Abre com acesso de leitura |
| Bateria pelo WMI (`root\wmi`) | Em parte | `BatteryStaticData` falhou; o número de ciclos veio 0. Por isso a fonte é a consulta direta |
| Placas de rede | Sim | A lista inclui adaptadores virtuais (Hyper-V, loopback, Bluetooth); a tela mostra só os físicos |

## Fontes

| Dado | Fonte | Onde foi conferido |
|---|---|---|
| `CreateDXGIFactory1`, `IDXGIFactory1::EnumAdapters1`, `IDXGIAdapter1::GetDesc1`, `DXGI_ADAPTER_DESC1` (memórias em bytes), `DXGI_ADAPTER_FLAG_SOFTWARE` 2, `DXGI_ERROR_NOT_FOUND` 0x887A0002 | learn.microsoft.com (dxgi.h) | Lido em 01/10/2026 |
| IID do `IDXGIFactory1` (770aae78-f26f-4dba-a829-253c83d1b387) e ordem dos métodos (EnumAdapters1 na posição 12, GetDesc1 na 10) | `dxgi.h` do SDK | `microsoft/win32metadata` |
| Versão e data do driver de vídeo: `DEVPKEY_Device_DriverVersion` e `DriverDate` na classe de vídeo | `devpkey.h` do SDK | Já usados na fatia 4 |
| Bateria: enumeração pela classe `GUID_DEVCLASS_BATTERY` (72631e54-78a4-11d0-bcf7-00aa00b7b32a), `BATTERY_QUERY_INFORMATION`, níveis (0 informação, 4 nome, 5 data de fabricação, 6 fabricante, 8 série), `BATTERY_INFORMATION` (capacidades em mWh, `CycleCount` 0 quando a bateria não conta ciclos), `BATTERY_SYSTEM_BATTERY`, `BATTERY_CAPACITY_RELATIVE`, `BATTERY_IS_SHORT_TERM` | learn.microsoft.com ("Enumerating Battery Devices" e páginas de cada estrutura) | Lido em 01/10/2026 |
| `IOCTL_BATTERY_QUERY_TAG` 0x00294040 e `IOCTL_BATTERY_QUERY_INFORMATION` 0x00294044 (leitura), `BATTERY_UNKNOWN_CAPACITY` 0xFFFFFFFF, `BATTERY_TAG_INVALID` 0 | `poclass.h` do SDK | `microsoft/win32metadata` |
| `GetAdaptersAddresses`, `IP_ADAPTER_ADDRESSES_LH`, `IF_TYPE_ETHERNET_CSMACD` 6, `IF_TYPE_IEEE80211` 71, `IfOperStatusUp` 1, velocidades em bits por segundo | learn.microsoft.com (iphlpapi, iptypes) | Lido em 01/10/2026; `MAX_ADAPTER_ADDRESS_LENGTH` 8 pelo `IPTypes.h` do SDK |
| Placa de rede física: `MIB_IF_ROW2` e o sinal `HardwareInterface` | learn.microsoft.com (netioapi) | [CONFERIR] antes da tarefa 4 |
| Ativação: `SLIsGenuineLocal` e `SL_GENUINE_STATE` | learn.microsoft.com (slpublic.h) | Lido em 01/10/2026. O identificador do Windows (`55c92734-d682-4d71-983e-d6ec3f16059f`) só aparece em respostas de fórum: [CONFERIR] numa máquina ativada e numa não ativada |
| Requisitos do Windows 11: processador compatível, 4 GB de memória, 64 GB de armazenamento, UEFI com Secure Boot, TPM 2.0 | "Windows 11 requirements" | learn.microsoft.com, windows/whats-new/windows-11-requirements |
| Listas de processadores do Windows 11 (Intel por série, AMD e Qualcomm por modelo), por versão do Windows | "Windows 11 supported Intel/AMD/Qualcomm processors" | learn.microsoft.com, windows-hardware/design/minimum/supported/ |
| EDID: bloco de 128 bytes, cabeçalho 00 FF FF FF FF FF FF 00, fabricante (bytes 8 e 9), produto, série, semana e ano (+1990), versão, largura e altura em cm (21 e 22), quatro descritores de 18 bytes a partir do 54 (0xFC nome, 0xFF número de série), resolução ativa do primeiro descritor de tempo | Linux, `include/drm/drm_edid.h` e `drivers/gpu/drm/drm_edid.c` (GPL-2.0) | Repositório `torvalds/linux` |
| Onde o Windows guarda o EDID do monitor | O caminho `Device Parameters\EDID` só aparece em respostas de fórum | [CONFERIR]; a leitura vai pela SetupAPI na chave de hardware do monitor, que a página "Overriding monitor EDIDs" cita |

## Mapa de arquivos

| Arquivo | Responsabilidade |
|---|---|
| `src/maphard.nucleo/video/fonte-video.cs` e `video.cs` | Placas de vídeo (R31) |
| `src/maphard.nucleo/video/edid.cs` e `monitores.cs` | Monitores (R32) |
| `src/maphard.nucleo/bateria/fonte-bateria.cs` e `bateria.cs` | Bateria (R33) |
| `src/maphard.nucleo/rede/fonte-rede.cs` e `rede.cs` | Placas de rede (R36) |
| `src/maphard.nucleo/windows/ativacao.cs` | Ativação e arquitetura (R34) |
| `src/maphard.nucleo/windows/windows11.cs` | Verificação do Windows 11 (R35) |
| `src/maphard.nucleo/tabelas/processadores-windows11.csv` e `.cs` | Listas de processadores da Microsoft |
| `ferramentas/gerar-processadores-windows11.ps1` | Gera a tabela a partir das páginas da Microsoft |
| `src/maphard.nucleo/saude/regras-windows.cs` | Linhas de Windows 11 e Bateria da seção 8 |
| `src/maphard.nucleo/coleta/*`, `relatorios/*`, `painel/*`, `linha-de-comando/*` | As seções novas em cada saída |
| `testes/maphard.testes/video-*.cs`, `bateria-*.cs`, `rede-*.cs`, `windows11-*.cs` | Testes |

---

### Tarefa 1: placas de vídeo (R31)

```csharp
public sealed record PlacaVideo(Campo<string> Nome, Campo<string> Fabricante, Campo<long> MemoriaDedicada,
    Campo<long> MemoriaCompartilhada, Campo<string> VersaoDriver, Campo<DateOnly> DataDriver);
```

- [ ] **Passo 1:** testes do intérprete com o `DXGI_ADAPTER_DESC1` montado à mão: nome até o primeiro zero; fabricante pelo VendorId (10DE NVIDIA, 1002 AMD, 8086 Intel, pela tabela de fabricantes PCI do `pci.ids`); adaptador com o sinal de software não entra; VendorId acima de 0xFFFF é ID ACPI e fica sem fabricante.
- [ ] **Passo 2:** implementar a fonte real pela DXGI (ponteiros da tabela de métodos, sem pacote), e a versão e a data do driver pela classe de vídeo da SetupAPI, casando o adaptador pelo VendorId e DeviceId com os IDs de hardware.
- [ ] **Passo 3:** teste `[FatoWindows]`: a máquina real tem ao menos uma placa com nome.
- [ ] **Passo 4:** testes verdes, commit.

### Tarefa 2: monitores (R32)

```csharp
public sealed record Monitor(Campo<string> Fabricante, Campo<string> Modelo, Campo<string> NumeroSerie,
    Campo<int> AnoFabricacao, Campo<double> Polegadas, Campo<string> ResolucaoNativa);
```

- [ ] **Passo 1:** testes do intérprete do EDID com o exemplo da Microsoft e blocos montados à mão: cabeçalho errado ou bloco curto vira nulo; soma de verificação errada vai na observação; fabricante pelas três letras do código PNP; nome e série pelos descritores 0xFC e 0xFF; ano pelo byte 17 + 1990; semana 0xFF é "ano do modelo"; polegadas pela diagonal de largura e altura em cm (0 em qualquer um vira nulo, como em projetor); resolução pelo primeiro descritor de tempo.
- [ ] **Passo 2:** conferir onde ler o EDID do monitor presente: SetupAPI na classe de monitores, chave de hardware do dispositivo. Anotar a fonte; sem fonte oficial, o caminho fica [CONFERIR] no comentário.
- [ ] **Passo 3:** implementar só para monitores presentes, sem administrador.
- [ ] **Passo 4:** teste `[FatoWindows]`: a leitura não falha (notebook com tela interna tem ao menos um monitor).
- [ ] **Passo 5:** testes verdes, commit.

### Tarefa 3: bateria (R33)

```csharp
public sealed record Bateria(Campo<string> Nome, Campo<string> Fabricante, Campo<string> Quimica,
    Campo<long> CapacidadeProjetoMwh, Campo<long> CapacidadeAtualMwh, Campo<double> Desgaste, Campo<int> Ciclos);
```

- [ ] **Passo 1:** testes do intérprete do `BATTERY_INFORMATION` (36 bytes): desgaste = 1 menos a capacidade de carga total sobre a de projeto; capacidade relativa (`BATTERY_CAPACITY_RELATIVE`) não vira mWh e o desgaste sai da razão; capacidade 0 ou desconhecida vira nula; ciclos 0 é "a bateria não informa"; química pelos 4 bytes ("LION" vira "íon de lítio", texto fora da tabela da página fica como veio); bateria de nobreak (`BATTERY_IS_SHORT_TERM`) e bateria que não é do sistema ficam de fora.
- [ ] **Passo 2:** implementar a fonte real como no exemplo "Enumerating Battery Devices", abrindo com acesso de leitura.
- [ ] **Passo 3:** desktop sem bateria: "não disponível neste equipamento".
- [ ] **Passo 4:** teste `[FatoWindows]`: a leitura não falha.
- [ ] **Passo 5:** testes verdes, commit.

### Tarefa 4: placas de rede (R36)

```csharp
public sealed record PlacaRede(Campo<string> Nome, Campo<string> Descricao, Campo<string> Mac,
    Campo<string> Tipo, Campo<long> VelocidadeBps, Campo<bool> Conectada);
```

- [ ] **Passo 1:** conferir no learn.microsoft.com o `MIB_IF_ROW2` e o sinal de interface de hardware, para separar placa física de adaptador virtual. Sem fonte, a tela mostra todas e marca Ethernet e Wi-Fi pelo tipo.
- [ ] **Passo 2:** testes: MAC no formato "AA-BB-CC-DD-EE-FF"; velocidade "1 Gb/s", "721 Mb/s"; tipo Ethernet (6) e Wi-Fi (71); placa desconectada fica sem velocidade; adaptador virtual fora da lista.
- [ ] **Passo 3:** implementar com `GetIfTable2`, que devolve o próprio `MIB_IF_ROW2` com o sinal de interface de hardware, sem administrador. Só a lista: o detalhe de rede é assunto do MapNet.
- [ ] **Passo 4:** testes verdes, commit.

### Tarefa 5: Windows e ativação (R34)

- [ ] **Passo 1:** arquitetura pelo Windows (x64 ou ARM64); edição, versão e compilação já vêm da fatia 1.
- [ ] **Passo 2:** ativação pelo `SLIsGenuineLocal` com o identificador do Windows [CONFERIR]: 0 "ativado", 1 "licença inválida", 2 "licença adulterada", 3 "sem conexão para confirmar"; falha da chamada vira "não informado". Nenhuma leitura de chave.
- [ ] **Passo 3:** conferir o identificador numa máquina ativada (deve dar "ativado") e registrar a conferência no comentário.
- [ ] **Passo 4:** testes verdes, commit.

### Tarefa 6: verificação do Windows 11 (R35)

| Item | Atende | Não atende | Fonte do dado |
|---|---|---|---|
| Processador | Consta na lista da versão atual do Windows 11 | Intel ou AMD de geração anterior à primeira da lista | CPUID e a tabela |
| TPM | Versão 2.0 | Sem TPM ou 1.2 | Fatia 1 |
| Firmware | UEFI | BIOS legado (resolve na configuração, com reinstalação ou conversão do disco) | Fatia 1 |
| Secure Boot | Ligado | Desligado com UEFI (resolve na configuração) | Fatia 1 |
| Memória | 4 GB ou mais instalados | Menos | Fatia 2 |
| Armazenamento | Disco do Windows com 64 GB ou mais | Menos | Fatia 3 |

- [ ] **Passo 1:** roteiro `ferramentas/gerar-processadores-windows11.ps1` que baixa as páginas de processadores da Intel, da AMD e da Qualcomm da versão atual do Windows 11 no learn.microsoft.com e grava `processadores-windows11.csv` (fabricante, família ou série, modelo, versão do Windows, fonte). Roteiro e tabela no mesmo commit.
- [ ] **Passo 2:** casamento com o processador: AMD e Qualcomm pelo modelo no nome comercial; Intel pela geração e pela família no nome comercial ("12th Gen Intel Core i5-12500H" é 12ª geração Core i5) contra as séries da lista. Sem casamento certo, "não consta na lista do MapHard", com a observação da Microsoft sobre gerações mais novas.
- [ ] **Passo 3:** um teste por linha do quadro, nos dois sentidos, e os casos do processador: na lista, de geração anterior, fora da lista sem certeza.
- [ ] **Passo 4:** testes verdes, commit.

### Tarefa 7: regras de saúde e saídas

Linhas da seção 8 do desenho:

| Cartão | Atenção | Ruim |
|---|---|---|
| Windows 11 | Algum requisito não atendido que se resolve na configuração (TPM ou Secure Boot desligados, modo legado) | Processador de geração anterior à lista, ou memória abaixo de 4 GB |
| Bateria | Desgaste acima de 30% | Desgaste acima de 50% |

- [ ] **Passo 1:** um teste por linha, nos dois sentidos, com o motivo escrito ("desgaste de 34%", "Secure Boot desligado: ligar no firmware"). Processador "não consta na lista do MapHard" deixa o cartão Windows 11 em Desconhecido, não em Ruim.
- [ ] **Passo 2:** coletor e JSON: a coleta ganha `Video`, `Monitores`, `Bateria`, `Rede` e `Windows11`, e a identificação ganha a ativação. Versão do formato 5. Teste de ida e volta.
- [ ] **Passo 3:** painel: seções **Vídeo e monitores**, **Bateria** e **Windows** (com os cartões "Windows", "Windows 11, item por item" e "Placas de rede"), na ordem da navegação do desenho.
- [ ] **Passo 4:** linha de comando: "Windows 11: aceita" ou "não aceita: processador fora da lista", "Bateria: desgaste de 12%", "Vídeo: Placa Exemplo, 4 GB".
- [ ] **Passo 5:** demonstração com uma placa de vídeo, um monitor, uma bateria e duas placas de rede fictícias, e o Windows 11 com um item em Atenção.
- [ ] **Passo 6:** testes do painel e da linha de comando, testes verdes, commit.

### Tarefa 8: fechamento

- [ ] **Passo 1:** README com as seções novas no "Uso" e a linha da fatia 5 na "Situação do projeto".
- [ ] **Passo 2:** pendências atualizadas, com cada [CONFERIR] que continuar aberto.
- [ ] **Passo 3:** portões, envio, Pull Request do código e leitura do CI até ficar verde.

## Como o Manfred testa

1. Baixar o `maphard.exe` do PR, ou gerar com `ferramentas\publicar.cmd`.
2. **Vídeo:** comparar a memória dedicada com o Gerenciador de Tarefas (Desempenho, GPU) e com o programa de referência.
3. **Monitores:** num desktop com monitor externo, conferir fabricante, modelo, ano e polegadas com a etiqueta do monitor.
4. **Bateria:** num notebook, comparar com o relatório do Windows (`powercfg /batteryreport`).
5. **Windows:** conferir a ativação em Configurações, Sistema, Ativação, numa máquina ativada e numa não ativada.
6. **Windows 11:** numa máquina com Windows 10, comparar item por item com o PC Integridade do Computador da Microsoft.
7. **Rede:** comparar com `Get-NetAdapter -Physical`.

## Riscos

| Risco | O que fazer |
|---|---|
| Lista de processadores muda a cada versão do Windows 11 | O roteiro regenera a tabela; a tela diz a versão da lista usada |
| Processador novo fora da lista | "Não consta na lista do MapHard", nunca "não aceita", como a própria Microsoft orienta |
| EDID de monitor desligado no registro | Só os monitores presentes pela SetupAPI |
| Bateria com capacidade relativa (sem mWh) | Desgaste pela razão, sem mostrar mWh |
| Identificador do Windows na ativação sem fonte oficial | Conferido em máquina ativada e não ativada antes do PR |
