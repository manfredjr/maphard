# Plano da fatia 1: base, processador e placa-mãe

> **Para quem executa:** siga tarefa por tarefa, na ordem. Cada passo tem caixa de seleção (`- [ ]`). Cada tarefa termina com teste verde e commit. Leia o `AGENTS.md` antes de começar.

**Objetivo:** o MapHard abre numa janela com a identidade da MT e mostra a identificação da máquina, o processador e a placa-mãe com o firmware, sem nunca mostrar 0 ou vazio onde não houve leitura. A linha de comando grava a mesma coleta em JSON.

**Arquitetura:** a leitura e a interpretação ficam separadas. Cada fonte do Windows (SMBIOS, CPUID, topologia, clocks, registro, TPM) entra por uma interface que devolve os dados brutos. O intérprete transforma os dados brutos em campos com estado e fonte, e é testado com amostras montadas à mão, sem depender da máquina onde o teste roda. O aplicativo WPF só liga a janela ao painel do núcleo.

**Tecnologia:** C# com .NET 8 (`net8.0-windows`), WPF, xUnit, `System.Text.Json`, P/Invoke por `LibraryImport` para `GetSystemFirmwareTable`, `GetLogicalProcessorInformationEx`, `CallNtPowerInformation`, `PdhAddEnglishCounterW`, `GetFirmwareType`, `IsProcessorFeaturePresent` e `Tbsi_GetDeviceInfo`. CPUID pelo `X86Base.CpuId` do próprio .NET. Nenhum pacote NuGet além dos de teste.

Desenho: `docs/superpowers/specs/2026-09-30-maphard-design.md`. Requisitos desta fatia: R2 (identificação, sem os discos e sem a memória detalhada, que chegam nas fatias 2 e 3), R3 a R8, R16 (sem o chipset, ver "Ajuste no desenho"), R17 a R19, R39, R41 parcial (`coletar --json`) e R42.

## Restrições globais

- Regras do produto 1 a 9 do `AGENTS.md`. Nesta fatia pesam a 1 (só leitura), a 2 (estados de campo), a 3 (sem driver), a 5 (sem instalar), a 6 (sem internet) e a 9 (sem chave de produto).
- Nenhum código grava no registro, em arquivo do sistema ou em configuração. O único arquivo que o programa grava é o JSON, onde o técnico mandar.
- Constante, estrutura ou código de erro de API do Windows marcado com [CONFERIR] é lido na documentação da Microsoft antes do código, e o endereço da página vai no comentário do código.
- Tudo em português, sem os caracteres proibidos, nome de arquivo em minúsculas.
- `TreatWarningsAsErrors` ligado. Build sem aviso.
- Commit com verbo na 3ª pessoa, título sem acento, corpo com o porquê, fim `Autores: Manfred Heil Junior`. Mensagem em `.superpowers/rascunho/msg-commit.txt`, com `git commit -F`. Nenhum commit com coautor ou menção a ferramenta.
- Leitura autorizada: o MapDisk e o MapNet, públicos, para copiar tema, fontes, logo da MT, testes de convenção, modo de linha de comando e CI. Cada cópia é dita no commit.
- Amostras de teste montadas à mão, com números de série fictícios (`SERIE-TESTE-0001`). Nenhum dado de máquina real entra no repositório.

## Git desta fatia

Fase de nuvem (ver "Fases do projeto" no `AGENTS.md`):

1. O plano entra pelo ramo da sessão, num Pull Request só do plano para o `main`. O código só começa depois que o Manfred aprovar e juntar esse PR.
2. O código sai do `main` atualizado, no ramo que a sessão tiver, e entra num segundo Pull Request, com "O que muda", "Como testar" e a linha de autores.
3. Os ganchos de backup ficam ligados (`git config core.hooksPath .githooks`). Todo commit sobe na hora.

## Testes na nuvem e no Windows

A máquina da nuvem é Linux. O núcleo é `net8.0-windows`, mas não usa WPF, e o código que chama API do Windows fica atrás das interfaces das fontes. Por isso:

- Os testes dos intérpretes, da formatação, do JSON e da linha de comando rodam no Linux, com as amostras montadas à mão.
- Os testes que chamam a API real do Windows usam o atributo `[FatoWindows]`, que pula o teste fora do Windows com o motivo "só roda no Windows".
- O CI em Windows roda todos. O portão que vale é o CI verde, lido pelo agente depois de cada envio.
- O SDK entra na máquina da nuvem pelo `apt` do Ubuntu (`dotnet-sdk-8.0`). Se a compilação para Windows falhar com esse SDK, o agente registra o erro na pendência e segue só com o CI.

## Ajuste no desenho

O chipset (parte do R16) sai pelo identificador PCI da ponte do sistema, lido pela SetupAPI, que é a mesma leitura dos dispositivos com problema. Proposta: o chipset passa para a fatia 5, junto com os dispositivos. O PR do plano pede a concordância do Manfred e, com ela, a seção 12 do desenho é ajustada no mesmo PR.

## Mapa de arquivos

| Arquivo | Responsabilidade |
|---|---|
| `global.json`, `Directory.Build.props`, `maphard.sln` | SDK, metadados e solução |
| `src/maphard.nucleo/maphard.nucleo.csproj` | Biblioteca `net8.0-windows`, sem WPF |
| `src/maphard.nucleo/campos/campo.cs` | `Campo<T>`, `EstadoCampo`, `FonteDado` e os textos de cada estado |
| `src/maphard.nucleo/formatacao/formatador.cs` | Bytes, MHz e GHz, datas, idade em anos, números em pt-BR |
| `src/maphard.nucleo/smbios/fonte-smbios.cs` | Interface `IFonteSmbios` e a leitura real pelo `GetSystemFirmwareTable` |
| `src/maphard.nucleo/smbios/tabela-smbios.cs` | Divide a tabela em estruturas e lê as cadeias de texto |
| `src/maphard.nucleo/smbios/estruturas.cs` | Tipos 0, 1, 2, 3 e 4 interpretados |
| `src/maphard.nucleo/cpuid/fonte-cpuid.cs` | Interface `IFonteCpuid` e a leitura real pelo `X86Base.CpuId` |
| `src/maphard.nucleo/cpuid/decodificador-cpuid.cs` | Fabricante, assinatura, nome comercial, instruções, nível x86-64, virtualização |
| `src/maphard.nucleo/processador/topologia.cs` | Núcleos, threads, núcleos de desempenho e de eficiência, caches, pela `GetLogicalProcessorInformationEx` |
| `src/maphard.nucleo/processador/clocks.cs` | Clock base, máximo e atual, uso por núcleo |
| `src/maphard.nucleo/processador/leitor-processador.cs` | Junta as fontes na seção Processador |
| `src/maphard.nucleo/tabelas/tabela-processadores.cs` e `tabelas/processadores.csv` | Codinome e litografia por fabricante, família e modelo |
| `src/maphard.nucleo/firmware/leitor-firmware.cs` | UEFI ou legado, Secure Boot, TPM, microcódigo, virtualização ligada no firmware |
| `src/maphard.nucleo/placa/leitor-placa.cs` | Seção Placa-mãe e firmware |
| `src/maphard.nucleo/windows/leitor-windows.cs` | Edição, versão e compilação do Windows, para a identificação |
| `src/maphard.nucleo/coleta/coleta.cs` | Modelo raiz da coleta e o coletor com tempo limite por fonte |
| `src/maphard.nucleo/relatorios/exportador-json.cs` | JSON da coleta |
| `src/maphard.nucleo/linha-de-comando/argumentos.cs`, `executor-cli.cs` | Linha de comando testável |
| `src/maphard.nucleo/painel/*.cs` | Seções, linhas da tela, estado da janela, dados de demonstração |
| `src/maphard.nucleo/sobre/sobre.cs` | Versão, autoria e licença para a janela Sobre |
| `src/maphard/*` | Aplicativo WPF: `programa.cs`, `modo-linha-de-comando.cs`, janelas, tema, fontes, logo, manifesto |
| `testes/maphard.testes/*` | Testes do núcleo e de convenção |
| `ferramentas/publicar.cmd`, `.github/workflows/testes.yml` | Gerar o `.exe` e CI |

---

### Tarefa 1: esqueleto, testes de convenção e CI

**Arquivos:**
- Criar: `global.json`, `Directory.Build.props`, `maphard.sln`, `src/maphard.nucleo/maphard.nucleo.csproj`, `testes/maphard.testes/maphard.testes.csproj`, `testes/maphard.testes/caracteres-proibidos-testes.cs`, `testes/maphard.testes/apoio/fato-windows.cs`, `.github/workflows/testes.yml`

- [ ] **Passo 1:** instalar o SDK na máquina da nuvem: `sudo apt-get install -y dotnet-sdk-8.0`. Conferir com `dotnet --version`.
- [ ] **Passo 2:** `global.json` igual ao do MapDisk (SDK 8.0.100, `rollForward` `latestFeature`).
- [ ] **Passo 3:** `Directory.Build.props` igual ao do MapDisk, com `<Product>MapHard - MT</Product>` e `<Version>0.1.0</Version>`, e com `<EnableWindowsTargeting>true</EnableWindowsTargeting>` para compilar no Linux.
- [ ] **Passo 4:** `maphard.nucleo.csproj` no molde do MapDisk: `net8.0-windows`, `AllowUnsafeBlocks`, `InternalsVisibleTo` para `maphard.testes`, licença embutida como `licenca.txt`.
- [ ] **Passo 5:** `maphard.testes.csproj` no molde do MapDisk (xUnit e `Microsoft.NET.Test.Sdk` nas mesmas versões), com `Using` de `MapHard.Nucleo`.
- [ ] **Passo 6:** solução com `dotnet new sln -n maphard` e `dotnet sln add` dos dois projetos.
- [ ] **Passo 7:** copiar `caracteres-proibidos-testes.cs` do MapDisk, trocando `MapDisk` por `MapHard` e `mapdisk.sln` por `maphard.sln`. O teste confere também a documentação em `docs/`.
- [ ] **Passo 8:** `FatoWindowsAttribute`, derivado de `FactAttribute`, que preenche `Skip` com "só roda no Windows" quando `OperatingSystem.IsWindows()` é falso.
- [ ] **Passo 9:** CI copiado do MapDisk, com `maphard.sln` e `maphard.exe`.
- [ ] **Passo 10:** rodar `dotnet test maphard.sln -c Release`. Esperado: os dois testes de convenção verdes (`Codigo_e_documentacao_nao_tem_caractere_proibido`, `Nome_de_arquivo_e_minusculo`).
- [ ] **Passo 11:** commit e conferência de que chegou ao GitHub.

### Tarefa 2: campos com estado

**Arquivos:** `src/maphard.nucleo/campos/campo.cs`, `testes/maphard.testes/campo-testes.cs`

**Interfaces produzidas:**

```csharp
namespace MapHard.Nucleo.Campos;

public enum EstadoCampo { Lido, NaoInformado, RequerAdministrador, NaoSuportado, ErroLeitura }

public enum FonteDado { Cpuid, Smbios, Topologia, Contador, Registro, Firmware, Tpm, Windows }

public sealed record Campo<T>(T? Valor, EstadoCampo Estado, FonteDado Fonte, string? Motivo = null)
{
    public static Campo<T> Lido(T valor, FonteDado fonte);
    public static Campo<T> NaoInformado(FonteDado fonte);
    public static Campo<T> RequerAdministrador(FonteDado fonte);
    public static Campo<T> NaoSuportado(FonteDado fonte);
    public static Campo<T> Erro(FonteDado fonte, string motivo);
}
```

- [ ] **Passo 1:** testes: `Lido` exige valor não nulo; os outros estados nunca carregam valor; o texto de cada estado é o da seção 6 do desenho ("não informado pelo fabricante", "requer administrador", "não disponível neste equipamento", "erro de leitura").
- [ ] **Passo 2:** texto de fábrica vira `NaoInformado`. Lista inicial: "To Be Filled By O.E.M.", "To be filled by O.E.M.", "Default string", "System Product Name", "System manufacturer", "Not Applicable", "None", "0123456789", "Not Specified", "INVALID", texto vazio e texto só com espaços. Teste para cada um e para um texto válido que contém "Default" no meio.
- [ ] **Passo 3:** implementar, testes verdes, commit.

### Tarefa 3: formatação em pt-BR

**Arquivos:** `src/maphard.nucleo/formatacao/formatador.cs`, `testes/maphard.testes/formatador-testes.cs`

- [ ] **Passo 1:** testes com a cultura pt-BR fixa, independentes da máquina: `Bytes(49152)` dá "48 KB"; `Bytes(1310720)` dá "1,25 MB"; `Bytes(34359738368)` dá "32 GB"; `Mhz(3600)` dá "3,60 GHz"; `Mhz(800)` dá "800 MHz"; `Data(2021-03-15)` dá "15/03/2021"; `IdadeEmAnos` de 15/03/2021 a 30/09/2026 dá "5 anos"; singular "1 ano"; menos de um ano dá "menos de 1 ano".
- [ ] **Passo 2:** implementar com `CultureInfo("pt-BR")` fixa, nunca a cultura da máquina.
- [ ] **Passo 3:** testes verdes, commit.

### Tarefa 4: tabela SMBIOS

**Arquivos:** `src/maphard.nucleo/smbios/fonte-smbios.cs`, `tabela-smbios.cs`, `estruturas.cs`, `testes/maphard.testes/apoio/construtor-smbios.cs`, `testes/maphard.testes/smbios-testes.cs`

**Como a tabela chega:** `GetSystemFirmwareTable` com o provedor `'RSMB'` devolve um cabeçalho de 8 bytes (método de chamada, versão maior, versão menor, revisão DMI, tamanho de 4 bytes) seguido das estruturas. Cada estrutura tem tipo (1 byte), tamanho da parte formatada (1 byte) e identificador (2 bytes), a parte formatada e, depois dela, as cadeias de texto terminadas em zero, com um zero a mais no fim. O campo de texto guarda o número da cadeia, começando em 1; zero é "sem texto". O tipo 127 marca o fim. Estrutura e deslocamentos pela especificação SMBIOS da DMTF (DSP0134) [CONFERIR a versão citada no comentário].

**Interfaces produzidas:**

```csharp
public interface IFonteSmbios { byte[]? LerTabelaBruta(); }   // null quando a API falha

public sealed record EstruturaSmbios(byte Tipo, ushort Identificador, byte[] Formatada, IReadOnlyList<string> Textos)
{
    public byte? Byte(int deslocamento);
    public ushort? Palavra(int deslocamento);
    public string? Texto(int deslocamento);   // null quando o índice é 0 ou não existe
}

public sealed record TabelaSmbios(Version Versao, IReadOnlyList<EstruturaSmbios> Estruturas);
public static class LeitorTabelaSmbios { public static TabelaSmbios? Interpretar(byte[] bruta); }
```

Campos interpretados nesta fatia:

| Tipo | Campos |
|---|---|
| 0, BIOS | Fabricante, versão, data (texto `mm/dd/aaaa` convertido), suporte a UEFI (byte de extensão 2, bit 3) |
| 1, Sistema | Fabricante, produto, versão, número de série, UUID (os três primeiros grupos em little-endian a partir da versão 2.6), SKU, família |
| 2, Placa-mãe | Fabricante, produto, versão, número de série |
| 3, Gabinete | Fabricante, tipo (bits 0 a 6 do byte do tipo, traduzido: desktop, torre, notebook, all-in-one, mini PC e os demais da tabela da especificação) |
| 4, Processador | Soquete, fabricante, versão, clock externo, clock máximo, núcleos, núcleos ativos, threads (com os campos de 2 bytes da versão 3.0 quando o de 1 byte vale 0xFF) |

- [ ] **Passo 1:** `ConstrutorSmbios`, apoio de teste que monta uma tabela em bytes a partir de estruturas descritas no teste (tipo, bytes formatados, textos).
- [ ] **Passo 2:** testes da divisão: tabela com três estruturas e o fim; estrutura sem textos (dois zeros logo após a parte formatada); índice de texto 0; índice de texto maior que a quantidade de textos; tabela truncada no meio de uma estrutura (devolve as estruturas completas e para, sem exceção); tamanho declarado maior que o buffer; buffer vazio; buffer nulo.
- [ ] **Passo 3:** testes dos tipos 0 a 4, com valores fictícios, incluindo o UUID com a ordem de bytes certa, o gabinete "notebook" e o processador com 0xFF no campo de 1 byte.
- [ ] **Passo 4:** implementar `LeitorTabelaSmbios` e as estruturas. Nenhum acesso fora dos limites do vetor: toda leitura passa pelos métodos `Byte`, `Palavra` e `Texto`, que devolvem null.
- [ ] **Passo 5:** `FonteSmbiosWindows`, com `GetSystemFirmwareTable` chamada duas vezes (tamanho, depois leitura). Teste `[FatoWindows]`: a tabela real tem ao menos uma estrutura do tipo 1.
- [ ] **Passo 6:** testes verdes, commit.

### Tarefa 5: CPUID

**Arquivos:** `src/maphard.nucleo/cpuid/fonte-cpuid.cs`, `decodificador-cpuid.cs`, `testes/maphard.testes/apoio/cpuid-simulado.cs`, `testes/maphard.testes/cpuid-testes.cs`

**Interfaces produzidas:**

```csharp
public readonly record struct RegistrosCpuid(uint Eax, uint Ebx, uint Ecx, uint Edx);

public interface IFonteCpuid { RegistrosCpuid Ler(uint funcao, uint subfuncao); }

public sealed record IdentidadeCpu(
    string Fabricante,          // "GenuineIntel", "AuthenticAMD" ou outro
    int Familia, int Modelo, int Revisao,
    string? NomeComercial,
    IReadOnlyList<string> Instrucoes,
    int NivelX8664,             // 1 a 4
    bool VirtualizacaoNoProcessador,   // VMX (Intel) ou SVM (AMD)
    bool HipervisorPresente,
    bool Hibrido);
```

Regras de decodificação, pelos manuais da Intel (SDM, volume 2A, instrução CPUID) e da AMD (APM, volume 3) [CONFERIR cada bit na fonte]:

- Função 0: maior função e fabricante (EBX, EDX, ECX, nessa ordem).
- Função 1, EAX: revisão nos bits 0 a 3, modelo 4 a 7, família 8 a 11, modelo estendido 16 a 19, família estendida 20 a 27. Família exibida: família + família estendida quando a família é 0xF. Modelo exibido: (modelo estendido << 4) + modelo quando a família é 6 ou 0xF.
- Funções 0x80000002 a 0x80000004: nome comercial, 48 bytes, sem os espaços das pontas.
- Instruções: funções 1, 7 e 0x80000001. Nível x86-64: v2 (CMPXCHG16B, LAHF, POPCNT, SSE3, SSSE3, SSE4.1, SSE4.2), v3 (v2 mais AVX, AVX2, BMI1, BMI2, F16C, FMA, LZCNT, MOVBE), v4 (v3 mais AVX-512 F, BW, CD, DQ, VL).
- Híbrido: função 7, EDX bit 15.

- [ ] **Passo 1:** `CpuidSimulado`, que responde a partir de um dicionário de funções montado no teste.
- [ ] **Passo 2:** testes da assinatura: EAX 0x000906EA dá família 6, modelo 0x9E, revisão 10; EAX 0x00A20F10 dá família 0x19, modelo 0x21, revisão 0.
- [ ] **Passo 3:** testes do nome comercial (texto fictício empacotado nos registradores, com espaços nas pontas), das instruções (cada bit ligado sozinho), dos níveis x86-64 (um caso por nível, e o caso em que falta uma instrução do v3), de VMX, SVM, hipervisor e híbrido.
- [ ] **Passo 4:** teste de processador que não tem as funções 7 ou 0x80000004: os campos correspondentes ficam `NaoSuportado`, sem exceção.
- [ ] **Passo 5:** implementar. `FonteCpuidWindows` usa `X86Base.CpuId` e confere `X86Base.IsSupported`. Teste `[FatoWindows]`: o fabricante real não é vazio.
- [ ] **Passo 6:** testes verdes, commit.

### Tarefa 6: topologia e caches

**Arquivos:** `src/maphard.nucleo/processador/topologia.cs`, `testes/maphard.testes/apoio/construtor-topologia.cs`, `testes/maphard.testes/topologia-testes.cs`

`GetLogicalProcessorInformationEx` com `RelationAll` devolve registros de tamanho variável. Nesta fatia interessam `RelationProcessorCore` (com a classe de eficiência, que separa núcleos de desempenho e de eficiência), `RelationCache` (nível, associatividade, tamanho da linha, tamanho, tipo e máscara dos processadores que compartilham) e `RelationProcessorPackage`. Estruturas e constantes [CONFERIR].

**Interfaces produzidas:**

```csharp
public interface IFonteTopologia { byte[]? LerBruta(); }

public sealed record CacheCpu(int Nivel, string Tipo, long TamanhoPorUnidade, int Unidades, int Associatividade, int TamanhoLinha);

public sealed record TopologiaCpu(int Pacotes, int Nucleos, int Threads, int NucleosDesempenho, int NucleosEficiencia, IReadOnlyList<CacheCpu> Caches);
```

- [ ] **Passo 1:** `ConstrutorTopologia`, que monta o buffer com registros de núcleo e de cache.
- [ ] **Passo 2:** testes: 4 núcleos com 2 threads cada, sem classes de eficiência (todos contam como desempenho); processador híbrido com 6 núcleos de desempenho com 2 threads e 8 de eficiência com 1 thread; caches L1 de dados e de instruções por núcleo, L2 por grupo e L3 único, agrupados por nível e tipo, com o número de unidades; buffer truncado; registro de tipo desconhecido, que é pulado pelo tamanho declarado.
- [ ] **Passo 3:** implementar e `FonteTopologiaWindows`. Teste `[FatoWindows]`: ao menos 1 núcleo e threads maior ou igual a núcleos.
- [ ] **Passo 4:** testes verdes, commit.

### Tarefa 7: tabela de processadores

**Arquivos:** `src/maphard.nucleo/tabelas/processadores.csv`, `tabela-processadores.cs`, `testes/maphard.testes/tabela-processadores-testes.cs`

Formato, separado por ponto e vírgula, UTF-8, recurso embutido:

```
fabricante;familia;modelo;codinome;litografia;fonte
```

`familia` e `modelo` em hexadecimal, como no CPUID. `fonte` diz de onde veio a linha (página de especificação do fabricante ou lista de famílias publicada), com o endereço. Linha sem fonte não entra: é a regra "nunca inventar" do `AGENTS.md`.

- [ ] **Passo 1:** testes da leitura: linha válida, linha com campo faltando (erro no teste de integridade), comentário com `#`, busca que acha, busca que não acha.
- [ ] **Passo 2:** teste de integridade da tabela embutida: toda linha tem os seis campos e a fonte não é vazia; não há duas linhas com o mesmo fabricante, família e modelo.
- [ ] **Passo 3:** montar as linhas iniciais das gerações Intel Core de 6ª geração em diante e AMD Ryzen (Zen em diante), cada uma com a fonte. O que não tiver fonte fica fora.
- [ ] **Passo 4:** na tela, processador fora da tabela mostra "não consta na tabela do MapHard", no estado `NaoInformado`. Codinome e litografia levam a dica "pela tabela do MapHard".
- [ ] **Passo 5:** testes verdes, commit.

### Tarefa 8: clocks e uso

**Arquivos:** `src/maphard.nucleo/processador/clocks.cs`, `testes/maphard.testes/clocks-testes.cs`

Fontes:

- Clock base: CPUID função 0x16, EAX, quando existe. Senão, o valor `~MHz` em `HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0` [CONFERIR].
- Clock máximo: `MaxMhz` da `CallNtPowerInformation(ProcessorInformation)` [CONFERIR], ou o clock máximo do SMBIOS tipo 4.
- Clock atual: contador `\Processor Information(_Total)\% Processor Performance` multiplicado pelo clock base, com duas amostras separadas por 1 segundo.
- Uso por núcleo: contador `\Processor Information(*)\% Processor Utility`.
- Os contadores entram por `PdhAddEnglishCounterW`, que usa o nome em inglês em qualquer idioma do Windows. O nome traduzido do Windows em português não funciona em outra máquina.

**Interfaces produzidas:**

```csharp
public interface IFonteClocks
{
    int? ClockBaseMhz();
    int? ClockMaximoMhz();
    double? DesempenhoPercentual();              // amostra de 1 segundo
    IReadOnlyList<double>? UsoPorNucleo();
}
```

- [ ] **Passo 1:** testes do cálculo com fonte simulada: base 3600 e desempenho 125% dá 4500 MHz; desempenho nulo deixa o clock atual em `ErroLeitura` com o motivo; base nula e máximo presente mostra o máximo e o atual fica `NaoInformado`.
- [ ] **Passo 2:** implementar e `FonteClocksWindows`. Teste `[FatoWindows]`: base maior que zero.
- [ ] **Passo 3:** testes verdes, commit.

### Tarefa 9: firmware, segurança e Windows

**Arquivos:** `src/maphard.nucleo/firmware/leitor-firmware.cs`, `src/maphard.nucleo/windows/leitor-windows.cs`, `testes/maphard.testes/firmware-testes.cs`, `testes/maphard.testes/windows-testes.cs`

| Dado | Fonte | Sem administrador |
|---|---|---|
| UEFI ou legado | `GetFirmwareType` [CONFERIR] | Sim |
| Secure Boot | `UEFISecureBootEnabled` em `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State` [CONFERIR]. Chave ausente em modo legado vira `NaoSuportado` | Sim |
| TPM presente e versão | `Tbsi_GetDeviceInfo` [CONFERIR]. "TPM não encontrado" vira `NaoSuportado` | Sim |
| TPM fabricante e estado | Fica `RequerAdministrador` nesta fatia | Não |
| Revisão de microcódigo | Valor `Update Revision` em `HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0` [CONFERIR o formato dos bytes] | Sim |
| Virtualização ligada no firmware | `IsProcessorFeaturePresent(PF_VIRT_FIRMWARE_ENABLED)` [CONFERIR a constante] | Sim |
| Windows: nome, versão, compilação, edição | `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion`: `CurrentBuild`, `UBR`, `DisplayVersion`, `EditionID`. Compilação 22000 ou maior é Windows 11, porque o `ProductName` continua dizendo "Windows 10" no Windows 11 | Sim |

Cada leitura entra por interface (`IFonteFirmware`, `IFonteRegistro`), para os testes simularem.

- [ ] **Passo 1:** testes com fontes simuladas: UEFI com Secure Boot ligado; legado sem a chave do Secure Boot; TPM 2.0; TPM ausente; compilação 19045 dá "Windows 10", 22631 dá "Windows 11", com a versão (`23H2`) e o número completo (`22631.4169`); valor de registro ausente vira `NaoInformado`.
- [ ] **Passo 2:** implementar e as fontes reais. Testes `[FatoWindows]`: o tipo de firmware é UEFI ou legado; a compilação é maior que zero.
- [ ] **Passo 3:** testes verdes, commit.

### Tarefa 10: seções e coleta

**Arquivos:** `src/maphard.nucleo/processador/leitor-processador.cs`, `src/maphard.nucleo/placa/leitor-placa.cs`, `src/maphard.nucleo/coleta/coleta.cs`, `testes/maphard.testes/coleta-testes.cs`

**Interfaces produzidas:**

```csharp
public sealed record SecaoProcessador(/* campos dos R3 a R8, todos Campo<T> */);
public sealed record SecaoPlaca(/* campos dos R16 a R19, todos Campo<T> */);
public sealed record Identificacao(/* campos do R2 disponíveis nesta fatia */);

public sealed record Coleta(
    string Formato,            // "maphard-coleta"
    int VersaoFormato,         // 1
    string VersaoPrograma,
    DateTimeOffset ColetadoEm,
    bool Administrador,
    Identificacao Identificacao,
    SecaoProcessador Processador,
    SecaoPlaca Placa);

public sealed class Coletor
{
    public Coletor(FontesColeta fontes, TimeSpan tempoLimitePorFonte);
    public Task<Coleta> ColetarAsync(IProgress<string>? andamento, CancellationToken cancelar);
}
```

- [ ] **Passo 1:** testes: fonte que lança exceção deixa só os campos dela em `ErroLeitura`, com o motivo, e o resto da coleta sai normal; fonte que passa do tempo limite vira "erro de leitura: tempo esgotado"; SMBIOS diz 8 núcleos e a topologia diz 8: sai 8 da topologia; os dois divergem: vale a topologia, e a divergência vai no motivo; nenhum campo da coleta fica sem estado.
- [ ] **Passo 2:** nome, modelo e número de série do equipamento para a identificação saem do SMBIOS tipo 1; se vierem como texto de fábrica, tenta o tipo 2 e diz na dica.
- [ ] **Passo 3:** implementar, testes verdes, commit.

### Tarefa 11: JSON

**Arquivos:** `src/maphard.nucleo/relatorios/exportador-json.cs`, `testes/maphard.testes/exportador-json-testes.cs`

Cada campo sai como objeto:

```json
{ "valor": "3,60 GHz", "valorBruto": 3600, "estado": "lido", "fonte": "cpuid", "motivo": null }
```

Nomes das propriedades em português, sem acento, no formato `camelCase`. Arquivo em UTF-8 sem BOM, indentado. Nome padrão do arquivo: `maphard-<computador>-<aaaa-mm-dd-hhmm>.json`, que o `.gitignore` já barra.

- [ ] **Passo 1:** testes: todos os estados saem com o nome certo; `valor` nulo fora do estado `lido`; ida e volta (gravar e ler) dá a mesma coleta; nome de computador com caractere inválido para arquivo é trocado por hífen.
- [ ] **Passo 2:** implementar com `System.Text.Json`, testes verdes, commit.

### Tarefa 12: linha de comando

**Arquivos:** `src/maphard.nucleo/linha-de-comando/argumentos.cs`, `executor-cli.cs`, `testes/maphard.testes/argumentos-testes.cs`, `testes/maphard.testes/executor-cli-testes.cs`

```bat
maphard coletar --json estacao.json
maphard coletar --json
maphard --ajuda
```

Sem nome depois de `--json`, o arquivo recebe o nome padrão na pasta atual. Códigos de saída: 0 deu certo, 1 argumento inválido, 2 não conseguiu gravar.

- [ ] **Passo 1:** testes dos argumentos (válidos, desconhecido, repetido, `--json` sem valor) e do executor com coletor e gravador simulados (mensagens em português, código de saída, nada gravado quando o argumento é inválido).
- [ ] **Passo 2:** implementar, testes verdes, commit.

### Tarefa 13: painel e demonstração

**Arquivos:** `src/maphard.nucleo/painel/secao-tela.cs`, `linha-tela.cs`, `painel-principal.cs`, `demonstracao.cs`, `testes/maphard.testes/painel-testes.cs`

- Cada seção vira uma lista de linhas: rótulo, texto do valor, estado (para a cor), dica com a fonte e o motivo.
- Seções desta fatia: Resumo (só a identificação), Processador, Placa-mãe e firmware. As outras seções da navegação aparecem só quando a fatia delas chegar.
- `--demonstracao` preenche a coleta com dados fictícios (fabricante "Fabricante Exemplo", série `SERIE-TESTE-0001`), para imagem de tela.

- [ ] **Passo 1:** testes: cada estado vira o texto da seção 6 do desenho; a dica diz a fonte ("SMBIOS", "CPUID", "tabela do MapHard"); o painel começa em "coletando..." e troca para os dados quando a coleta termina; a demonstração não chama nenhuma fonte real.
- [ ] **Passo 2:** implementar, testes verdes, commit.

### Tarefa 14: aplicativo WPF

**Arquivos:** `src/maphard/maphard.csproj`, `app.manifest`, `programa.cs`, `modo-linha-de-comando.cs`, `janela-principal.xaml(.cs)`, `janela-sobre.xaml(.cs)`, `tema/tema-mt.xaml`, `recursos/fontes/*`, `recursos/mt-logo.png`, `src/maphard.nucleo/sobre/sobre.cs`

- [ ] **Passo 1:** `maphard.csproj` no molde do MapDisk: `WinExe`, `net8.0-windows`, WPF, `win-x64`, autocontido, arquivo único comprimido, manifesto `asInvoker` (sem pedir administrador, até a decisão da pergunta 2 do desenho).
- [ ] **Passo 2:** copiar do MapDisk o tema `tema-mt.xaml`, as fontes Montserrat com a `ofl.txt`, o logo `mt-logo.png`, o `modo-linha-de-comando.cs` e a janela Sobre, trocando o nome do produto.
- [ ] **Passo 3:** janela principal: faixa verde com o nome "MapHard - MT" e o logo da MT, botões Atualizar, Salvar JSON e Sobre; navegação à esquerda; cartões à direita; barra de status com computador, "usuário comum" ou "administrador" e a hora da coleta. Valor em estado diferente de `Lido` aparece em cinza, com a dica.
- [ ] **Passo 4:** sem a arte do MapHard, o `.exe` fica sem ícone próprio e a faixa mostra só o nome e o logo da MT. Pendência registrada.
- [ ] **Passo 5:** teste de recursos no molde do MapDisk: fontes, logo e licença embutidos.
- [ ] **Passo 6:** build do aplicativo, testes verdes, commit.

### Tarefa 15: publicação, README e fechamento

- [ ] **Passo 1:** `ferramentas/publicar.cmd` copiado do MapDisk, com os nomes trocados.
- [ ] **Passo 2:** README com o "Uso" desta fatia e a linha da fatia 1 na "Situação do projeto".
- [ ] **Passo 3:** `pendencias.md` atualizado (ícone, chipset na fatia 5 se aprovado, o que o CI apontar).
- [ ] **Passo 4:** portões do `AGENTS.md`, envio, Pull Request do código com "O que muda", "Como testar" e `Autores: Manfred Heil Junior`.
- [ ] **Passo 5:** ler o CI do PR até ficar verde. Só então pedir o teste do Manfred.

## Como o Manfred testa

Na fase de nuvem, pelo `.exe` que o CI gera em cada PR (aba Actions, execução do PR, Artifacts):

1. Baixar o `maphard.exe` e abrir como usuário comum.
2. Conferir no Processador: nome, núcleos e threads, caches, instruções e clock, comparando com o programa de referência que ele já usa.
3. Conferir na Placa-mãe e firmware: fabricante, modelo, BIOS com a data, UEFI, Secure Boot e TPM.
4. Rodar `maphard coletar --json` e abrir o arquivo.
5. Abrir com `--demonstracao` e conferir que nenhum dado real aparece.

## Riscos

| Risco | O que fazer |
|---|---|
| O SDK do `apt` não compila para Windows no Linux | Seguir só com o CI em Windows e registrar a pendência |
| Firmware com SMBIOS incompleto ou com texto de fábrica | Estados `NaoInformado`, testados com amostras |
| Máquina virtual (Hyper-V, VMware) com CPUID e SMBIOS do hipervisor | Mostrar o que vier e marcar "máquina virtual" quando o bit de hipervisor estiver ligado e o fabricante do SMBIOS for de hipervisor |
| Processador novo fora da tabela | "não consta na tabela do MapHard", nunca um palpite |
| Contador de desempenho desativado na máquina | Clock atual em `ErroLeitura`, com o motivo. O resto segue |
