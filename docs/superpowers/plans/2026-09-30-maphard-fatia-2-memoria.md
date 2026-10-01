# Plano da fatia 2: memória

> **Para quem executa:** siga tarefa por tarefa, na ordem. Cada passo tem caixa de seleção (`- [ ]`). Cada tarefa termina com teste verde e commit. Leia o `AGENTS.md` antes de começar.

**Objetivo:** o MapHard responde as perguntas que o técnico faz sobre memória num atendimento: que tipo de memória a máquina tem, quanto tem, em que velocidade roda, quais slots estão ocupados, quanto cabe a mais e se há algo errado na montagem (módulos diferentes, memória abaixo da velocidade, canal único, memória reservada demais). Tudo sem administrador.

**Arquitetura:** a mesma da fatia 1. O SMBIOS ganha os tipos 16 (conjunto de memória) e 17 (módulo de memória), o Windows entrega o uso, e um módulo `memoria/` junta as leituras, calcula os alertas e a resposta de ampliação. O coletor, o JSON, o painel e a linha de comando ganham a seção Memória.

**Tecnologia:** C# com .NET 8, xUnit. P/Invoke por `LibraryImport` para `GlobalMemoryStatusEx`, `GetPhysicallyInstalledSystemMemory` e `GetPerformanceInfo`. Nenhum pacote NuGet novo.

Desenho: `docs/superpowers/specs/2026-09-30-maphard-design.md`. Requisitos desta fatia: R9 a R14, e a memória total com o tipo na identificação (R2). Os erros de memória (R15) ficam na fatia 4, junto com os eventos do Windows, como está no desenho.

## Restrições globais

- As mesmas da fatia 1. Nesta fatia pesam as regras do produto 1 (só leitura), 2 (estados de campo), 3 (sem driver) e 6 (tabelas embutidas, sem internet).
- **Sem SPD.** Timings (CL, tRCD, tRP), perfis XMP e EXPO e o conteúdo bruto do SPD dependem de driver e ficam no módulo Sensores, depois da versão 1. A tela não mostra esses campos, nem vazios.
- Amostras de teste montadas à mão, com números de série e part numbers fictícios (`SERIE-MEM-0001`, `PN-TESTE-3200`).
- Na nuvem, os testes rodam pelo projeto de testes. A janela compila só no CI em Windows, por decisão do Manfred de 30/09/2026.

## Fontes

| Dado | Fonte | Onde foi conferido |
|---|---|---|
| Tipo 16, conjunto de memória | SMBIOS, seção 7.17 da especificação | `dmidecode.c`, `dmi_decode`, caso 16 |
| Tipo 17, módulo de memória | SMBIOS, seção 7.18 | `dmidecode.c`, `dmi_decode`, caso 17, e as funções de tamanho, velocidade, tipo, formato e voltagem |
| Fabricante pelo código JEDEC (JEP106) | Tabela embutida | Lista `@vendors` do `decode-dimms` do i2c-tools (GPL-2.0 ou posterior), com a regra de banco e paridade da função `manufacturer_ddr3` |
| Memória instalada | `GetPhysicallyInstalledSystemMemory`, em KB | MicrosoftDocs/sdk-api, sysinfoapi |
| Memória utilizável, disponível e carga | `GlobalMemoryStatusEx` | MicrosoftDocs/sdk-api, sysinfoapi |
| Memória confirmada, limite, cache | `GetPerformanceInfo` | Citado pela documentação do `MEMORYSTATUSEX` como a fonte do limite de todo o sistema [CONFERIR a estrutura PERFORMANCE_INFORMATION] |

Deslocamentos que o plano usa, conferidos no `dmidecode`:

| Tipo | Deslocamento | Campo |
|---|---|---|
| 16 | 0x04 | Localização (placa, adicional...) |
| 16 | 0x05 | Uso (3 = memória do sistema) |
| 16 | 0x06 | Correção de erro: 3 nenhuma, 4 paridade, 5 ECC de um bit, 6 ECC de vários bits, 7 CRC |
| 16 | 0x07 | Capacidade máxima em KB, 4 bytes. 0x80000000 manda ler o campo estendido |
| 16 | 0x0D | Número de slots, 2 bytes |
| 16 | 0x0F | Capacidade máxima estendida em bytes, 8 bytes (estrutura com 0x17 ou mais) |
| 17 | 0x0C | Tamanho, 2 bytes: 0 sem módulo, 0xFFFF desconhecido, bit 15 ligado quer dizer KB e desligado MB, 0x7FFF manda ler o estendido |
| 17 | 0x0E | Formato: 0x09 DIMM, 0x0D SODIMM e os demais da tabela 7.18.1 |
| 17 | 0x10, 0x11 | Textos do slot e do banco |
| 17 | 0x12 | Tipo: 0x18 DDR3, 0x1A DDR4, 0x22 DDR5, 0x1D LPDDR3, 0x1E LPDDR4, 0x23 LPDDR5 e os demais da tabela 7.18.2 |
| 17 | 0x15 | Velocidade nominal em MT/s, 2 bytes. 0xFFFF manda ler o estendido em 0x54 |
| 17 | 0x17 a 0x1A | Textos de fabricante, número de série, patrimônio e part number |
| 17 | 0x1B | Ranks, bits 0 a 3 |
| 17 | 0x1C | Tamanho estendido em MB, 4 bytes |
| 17 | 0x20 | Velocidade configurada em MT/s, 2 bytes. 0xFFFF manda ler o estendido em 0x58 |
| 17 | 0x22, 0x24, 0x26 | Voltagem mínima, máxima e configurada, em mV |
| 17 | 0x2C | Código JEDEC do fabricante do módulo, 2 bytes (estrutura com 0x34 ou mais) |

## Mapa de arquivos

| Arquivo | Responsabilidade |
|---|---|
| `src/maphard.nucleo/smbios/estruturas-memoria.cs` | Tipos 16 e 17 interpretados |
| `src/maphard.nucleo/tabelas/fabricantes-memoria.csv` e `fabricantes-memoria.cs` | Tabela JEP106 e a tradução do código ou do texto do fabricante |
| `src/maphard.nucleo/memoria/fonte-memoria.cs` | Interface `IFonteMemoria` e as leituras do Windows |
| `src/maphard.nucleo/memoria/leitor-memoria.cs` | Seção Memória: módulos, slots, uso |
| `src/maphard.nucleo/memoria/alertas-memoria.cs` | Alertas do R13 e a resposta de ampliação do R14 |
| `src/maphard.nucleo/coleta/*`, `relatorios/*`, `painel/*`, `linha-de-comando/*` | A seção Memória em cada saída |
| `testes/maphard.testes/memoria-*.cs` | Testes |

---

### Tarefa 1: tipos 16 e 17 do SMBIOS

**Interfaces produzidas:**

```csharp
public sealed record ConjuntoMemoriaSmbios(byte? Uso, byte? CorrecaoErro, long? CapacidadeMaximaBytes, int? Slots);

public sealed record ModuloMemoriaSmbios(
    string? Slot, string? Banco,
    long? TamanhoBytes,          // null: desconhecido
    bool Vazio,                  // tamanho 0: slot sem módulo
    byte? Formato, byte? Tipo,
    int? VelocidadeNominal, int? VelocidadeConfigurada,   // MT/s
    string? Fabricante, ushort? CodigoFabricante,
    string? NumeroSerie, string? PartNumber,
    int? Ranks, int? VoltagemConfiguradaMv);
```

- [x] **Passo 1:** testes com o `ConstrutorSmbios` da fatia 1: módulo de 16 GB em MB; módulo com o bit 15 (KB); módulo com 0x7FFF e tamanho estendido de 64 GB; slot vazio (tamanho 0) com os outros campos ignorados; tamanho 0xFFFF desconhecido; velocidade 0xFFFF com o estendido; estrutura curta (0x15) sem velocidade; estrutura de versão 2.x sem voltagem nem código de fabricante; nomes de tipo e formato (DDR4, DDR5, LPDDR5, SODIMM, DIMM); tipo fora da tabela vira nulo.
- [x] **Passo 2:** testes do tipo 16: capacidade em KB; 0x80000000 com o estendido; estrutura curta com 0x80000000 vira nula; ECC de um bit; várias estruturas do tipo 16, das quais só as de uso 3 (memória do sistema) contam.
- [x] **Passo 3:** implementar em `estruturas-memoria.cs`, com os deslocamentos da tabela acima e as leituras protegidas da fatia 1.
- [x] **Passo 4:** teste `[FatoWindows]`: a máquina real tem ao menos um módulo do tipo 17 com tamanho.
- [x] **Passo 5:** testes verdes, commit.

### Tarefa 2: fabricantes de memória (JEP106)

O firmware às vezes grava o nome do fabricante ("Samsung") e às vezes o código em hexadecimal ("80CE", "CE00", "0x80CE"). O código tem o banco (número de bytes de continuação 0x7F) e o número do fabricante no banco, com paridade ímpar no bit 7.

Formato da tabela, com a fonte no cabeçalho e em cada linha:

```
banco;codigo;nome;fonte
```

- [x] **Passo 1:** gerar `fabricantes-memoria.csv` a partir da lista `@vendors` do `decode-dimms`, com um roteiro em `ferramentas/` que baixa, converte e grava a fonte. O roteiro e a tabela entram no mesmo commit.
- [x] **Passo 2:** testes da tradução: nome já em texto fica como está; "80CE" e "CE00" dão o mesmo fabricante; código com paridade errada vira "código inválido"; código fora da tabela vira "código JEDEC 0x...", no estado lido, com a dica "fora da tabela do MapHard"; o código de 2 bytes do deslocamento 0x2C dá o mesmo resultado que o texto.
- [x] **Passo 3:** teste de integridade da tabela: sem repetição de banco e código, e fonte em toda linha.
- [x] **Passo 4:** testes verdes, commit.

### Tarefa 3: uso da memória pelo Windows

```csharp
public interface IFonteMemoria
{
    long? InstaladaKb();                     // GetPhysicallyInstalledSystemMemory
    EstadoMemoriaWindows? Estado();          // GlobalMemoryStatusEx e GetPerformanceInfo
}

public sealed record EstadoMemoriaWindows(long TotalBytes, long DisponivelBytes, int CargaPercentual,
    long? ConfirmadaBytes, long? LimiteConfirmadaBytes, long? CacheBytes);
```

- [x] **Passo 1:** conferir `PERFORMANCE_INFORMATION` na documentação (MicrosoftDocs/sdk-api, psapi) e anotar a fonte no comentário. Os valores vêm em páginas e são multiplicados pelo tamanho da página.
- [x] **Passo 2:** implementar a fonte real. Teste `[FatoWindows]`: instalada maior ou igual à utilizável.
- [x] **Passo 3:** testes verdes, commit.

### Tarefa 4: seção Memória

```csharp
public sealed record SecaoMemoria(
    Campo<long> Instalada, Campo<long> Utilizavel, Campo<long> Reservada,
    Campo<string> Tipo,                    // "DDR4", ou "DDR4 e DDR5" se misturar (não deveria)
    Campo<int> SlotsTotal, Campo<int> SlotsOcupados,
    Campo<long> CapacidadeMaxima, Campo<string> Ecc,
    Campo<IReadOnlyList<ModuloTela>> Modulos,   // um por slot, inclusive os vazios
    Campo<long> EmUso, Campo<long> Disponivel, Campo<int> Carga,
    Campo<long> Confirmada, Campo<long> LimiteConfirmada, Campo<long> Cache,
    IReadOnlyList<AlertaMemoria> Alertas,
    Campo<string> Ampliacao);
```

- [x] **Passo 1:** testes: dois módulos iguais de 8 GB DDR4-3200 em quatro slots; slots sem módulo aparecem como "vazio", nunca somem; memória reservada é a instalada menos a utilizável; soma dos módulos diferente da instalada vai no motivo; SMBIOS sem tipo 17 deixa os módulos em "não informado" e a instalada ainda sai do Windows.
- [x] **Passo 2:** implementar em `leitor-memoria.cs`, com os mesmos cuidados do coletor da fatia 1 (tempo limite, erro só nos campos da fonte).
- [x] **Passo 3:** testes verdes, commit.

### Tarefa 5: alertas e ampliação

Regras, com valores iniciais que o Manfred pode ajustar:

| Alerta | Quando | Texto |
|---|---|---|
| Módulos diferentes | Entre os módulos instalados, capacidade ou velocidade nominal diferem. O part number saiu da regra por decisão do Manfred em 01/10/2026, depois do teste em máquina real | "módulos diferentes: 8 GB e 4 GB" |
| Abaixo da velocidade | Velocidade configurada menor que a nominal | "rodando a 2666 MT/s; os módulos aceitam 3200. Pode ser limite do processador ou da placa" |
| Canal único provável | Um módulo só numa placa com dois ou mais slots; ou dois módulos cujos textos de slot indicam o mesmo canal ("ChannelA-DIMM0" e "ChannelA-DIMM1", "A1" e "A2") | "provável canal único: o desempenho da memória cai" |
| Reserva alta | Reservada acima de 25% da instalada | "o Windows usa 6 GB dos 16 GB instalados" |

- Ampliação: "cabem até 64 GB (informado pelo firmware); 2 slots livres; tipo DDR4, formato SODIMM". Sem capacidade máxima no firmware, a frase diz só os slots e o tipo. Com todos os slots ocupados, diz "sem slot livre: ampliar exige trocar módulos".
- A regra do canal é heurística e o texto diz "provável". Texto de slot que não segue nenhum padrão conhecido não gera alerta.

- [x] **Passo 1:** um teste por linha do quadro, nos dois sentidos (dispara e não dispara), e os três casos da ampliação.
- [x] **Passo 2:** implementar em `alertas-memoria.cs`.
- [x] **Passo 3:** testes verdes, commit.

### Tarefa 6: saídas

- [x] **Passo 1:** coletor e JSON: a coleta ganha `Memoria`. A versão do formato do JSON passa a 2, porque a estrutura cresce. Teste de ida e volta.
- [x] **Passo 2:** identificação: "Memória" passa a ser a instalada com o tipo ("16 GB DDR4"), e a utilizável continua ao lado.
- [x] **Passo 3:** painel: seção **Memória**, entre Processador e Placa-mãe, com os cartões "Resumo" (instalada, utilizável, reservada, tipo, slots, capacidade máxima, ECC), "Atenção" (os alertas, só quando houver), "Slots" (um bloco por slot: tamanho, tipo e velocidade, fabricante, part number, número de série, ranks, voltagem), "Ampliação" e "Uso agora".
- [x] **Passo 4:** linha de comando: o resumo ganha a linha "Memória: 16 GB DDR4-3200, 2 de 4 slots" e os alertas.
- [x] **Passo 5:** demonstração com dois módulos fictícios e um alerta, para a imagem mostrar como fica.
- [x] **Passo 6:** testes do painel e da linha de comando, testes verdes, commit.

### Tarefa 7: fechamento

- [x] **Passo 1:** README com a seção Memória no "Uso" e a linha da fatia 2 na "Situação do projeto".
- [x] **Passo 2:** pendências atualizadas.
- [ ] **Passo 3:** portões, envio, Pull Request do código e leitura do CI até ficar verde.

## Como o Manfred testa

1. Baixar o `maphard.exe` do PR, ou gerar com `ferramentas\publicar.cmd` em `C:\COWORK\CODE\MAPHARD-MT`.
2. Na seção **Memória**, conferir tipo, velocidade, fabricante e part number de cada pente com o programa de referência e com a etiqueta do pente, quando der para abrir a máquina.
3. Conferir os slots vazios e a frase de ampliação num notebook e num desktop.
4. Numa máquina com um pente só, conferir o alerta de canal único.
5. Rodar `maphard coletar --json` e conferir a seção `memoria` no arquivo.

## Riscos

| Risco | O que fazer |
|---|---|
| Firmware com capacidade máxima errada (comum em placas antigas) | A frase diz "informado pelo firmware" e a dica explica que o manual da placa é a referência |
| Texto do slot sem padrão ("DIMM 0", "Bottom-Slot 1") | O alerta de canal só usa padrões conhecidos; o resto não gera alerta |
| Memória soldada na placa (notebooks finos) | Aparece como módulo com o formato informado pelo firmware ("Row Of Chips" ou "Die"); a ampliação diz "sem slot livre" |
| Máquina virtual | O hipervisor entrega tipo 17 genérico; a seção mostra o que vier, e a identificação já marca "máquina virtual" |
