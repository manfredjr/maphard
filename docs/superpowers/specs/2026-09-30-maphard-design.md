# MapHard - MT: desenho da versão 1

Data: 30/09/2026. Autor: Manfred Heil Junior.

Este desenho registra o pedido do Manfred de 30/09/2026, antes de qualquer código. Os pontos de partida são dois programas gratuitos que ele usa hoje no atendimento: o CPU-Z, da CPUID, para processador, placa-mãe e memória, e o CrystalDiskInfo, do Crystal Dew World, para a saúde dos discos. Os nomes dos dois aparecem aqui só como referência factual. O MapHard não usa esses nomes na tela, no código, no relatório, na página nem no README.

Os itens marcados com **[DECIDIR]** esperam a decisão do Manfred. Os marcados com **[CONFERIR]** saem da documentação oficial da Microsoft no plano da fatia, e não de memória.

## 1. O que é

Programa para Windows que mostra o hardware do computador em que roda e diz, logo na primeira tela, se há algo errado. Terceiro item do conjunto de ferramentas da MT, ao lado do MapNet (rede) e do MapDisk (espaço em disco).

O fluxo de trabalho que o programa atende:

1. O técnico chega numa estação do cliente, presencialmente ou por acesso remoto.
2. Abre o `maphard.exe`, sem instalar nada.
3. Na tela **Resumo**, vê em segundos o que exige atenção: disco no fim da vida, memória com erro, memória rodando abaixo da velocidade, desligamentos inesperados, dispositivo sem driver, máquina que não aceita o Windows 11.
4. Quando precisa de um dado específico (tipo de memória, slots livres, modelo do SSD, número de série), abre a seção correspondente.
5. Salva o relatório para o cliente ou o arquivo de inventário para a MT.

## 2. Para quem

- **Quem usa:** a equipe técnica da MT e qualquer pessoa que baixar o programa, já que o código é aberto.
- **Onde roda:** Windows 10 e Windows 11, 64 bits, nas estações dos clientes da MT. Windows Server fica fora dos testes da versão 1 **[DECIDIR]**.
- **Quem recebe o resultado:** o cliente, pelo relatório HTML, e a MT, pelo arquivo JSON de inventário.

## 3. Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Nome | MapHard - MT, executável `maphard.exe` | Pedido do Manfred em 30/09/2026. Faz trio com o MapNet e o MapDisk |
| Código | Aberto, repositório `manfredjr/maphard`, licença GPL-3.0 | Pedido do Manfred em 30/09/2026, no mesmo modelo do MapNet e do MapDisk. O repositório hoje é privado. Passa a público quando o Manfred decidir **[DECIDIR]** |
| Linguagem | C# com .NET 8 | Mesma stack dos outros dois. Acesso direto às APIs do Windows e à instrução CPUID do processador |
| Entrega | `.exe` único, autocontido, `win-x64`, sem instalador | Rodar de pendrive ou de pasta de rede na máquina do cliente |
| Interface | Janela WPF e linha de comando no mesmo `.exe` | Mesmo padrão do MapDisk, com o tema `tema-mt.xaml`, a fonte Montserrat e o logo da MT reaproveitados |
| Visual | Faixa verde no topo com o símbolo do MapHard e o logo da MT, cartões brancos, botões em pílula, linha em gradiente acima da barra de status | Mesmas características visuais do MapNet e do MapDisk, pedido do Manfred |
| Driver de kernel | Nenhum na versão 1 | Ver seção 4. É a decisão que mais pesa no que o programa consegue ou não mostrar |
| Sensores | Módulo opcional depois da versão 1, com a biblioteca LibreHardwareMonitor e o driver PawnIO | Decisão do Manfred em 30/09/2026. Ver seção 14 |
| Permissão | Abre como usuário comum. O botão "Ler como administrador" reabre o programa elevado | Regra do MapDisk. Sem administrador já sai a maior parte das informações. SMART, TPM e BitLocker pedem elevação, e a tela diz isso em vez de esconder. Decidido pelo Manfred em 01/10/2026 (pergunta 2) |
| Só leitura | O MapHard não altera nada na máquina | Programa de diagnóstico. Nenhuma configuração, driver ou firmware é mudado |
| Internet | Nenhum acesso | Mesma regra dos outros dois. Tabelas de fabricantes e de processadores vão embutidas no `.exe` |

## 4. O que dá e o que não dá para ler sem driver de kernel

Os programas de referência leem parte das informações com um driver de kernel próprio, que acessa diretamente os registradores do processador e o barramento SMBus das memórias. Um driver desses precisa de assinatura de código da Microsoft, costuma ser barrado por antivírus e, quando tem falha, abre uma porta de ataque na máquina do cliente. Um driver livre muito usado por programas de monitoramento passou a ser detectado como ferramenta de ataque pelo Microsoft Defender em 2025, por causa de uma falha conhecida.

Por isso a versão 1 não usa driver. O que depende dele vai para o módulo Sensores, depois da versão 1 (seção 14). O quadro mostra o efeito prático:

| Informação | Sem driver | Como |
|---|---|---|
| Processador: nome, fabricante, família, modelo, revisão, instruções, caches, núcleos | Sim | Instrução CPUID, pelo `X86Base.CpuId` do .NET, sem administrador |
| Clock atual do processador | Sim, aproximado | Contador de desempenho do Windows (`% Processor Performance`) sobre o clock base |
| Temperatura e voltagem do processador | Não | Exige driver. Fica fora da versão 1 |
| Memória: tipo (DDR3, DDR4, DDR5), capacidade, velocidade nominal e configurada, fabricante, part number, número de série, slot, voltagem | Sim | Tabela SMBIOS do firmware, lida pelo `GetSystemFirmwareTable`, sem administrador |
| Memória: timings (CL, tRCD, tRP), perfis XMP e EXPO, dados brutos do SPD | Não | Exige acesso ao SMBus, portanto driver. Fica fora da versão 1 |
| Placa-mãe e BIOS | Sim | SMBIOS |
| Disco: modelo, firmware, número de série, interface, capacidade | Sim | APIs de armazenamento do Windows |
| Disco: SMART, temperatura, horas ligado, dados gravados, saúde | Sim, com administrador | `DeviceIoControl` no disco físico, com os comandos ATA e NVMe documentados pela Microsoft |
| Disco atrás de controladora RAID ou de adaptador USB | Parcial | Depende da controladora e da ponte USB. Quando não der, a tela diz "SMART indisponível por esta controladora" |
| Temperatura do disco | Sim, com administrador | Vem do SMART |

## 5. Requisitos

As seções da janela seguem a ordem do quadro. Cada requisito diz de onde vem o dado.

### Resumo

| # | Requisito |
|---|---|
| R1 | Tela inicial com um cartão por área (Discos, Memória, Processador, Estabilidade, Dispositivos, Windows 11, Bateria), cada um com estado **Bom**, **Atenção**, **Ruim** ou **Desconhecido** e uma frase curta com o motivo. Clique no cartão abre a seção |
| R2 | Identificação da máquina no topo do Resumo: nome do computador, fabricante e modelo, número de série do equipamento, processador, memória total e tipo, discos com capacidade, versão do Windows |

### Processador

| # | Requisito |
|---|---|
| R3 | Nome comercial, fabricante, família, modelo e revisão (stepping), codinome e litografia (tabela embutida, marcada "pela tabela do MapHard"), soquete (SMBIOS), revisão de microcódigo (registro do Windows) |
| R4 | Núcleos e threads. Em processador híbrido, núcleos de desempenho e de eficiência separados (`GetLogicalProcessorInformationEx`) |
| R5 | Clock base, máximo e atual. Uso atual por núcleo |
| R6 | Caches L1 de dados, L1 de instruções, L2 e L3, com tamanho, associatividade e quantos núcleos compartilham cada um (CPUID) |
| R7 | Conjunto de instruções: SSE até SSE4.2, AVX, AVX2, AVX-512, FMA, AES, SHA, x86-64 e o nível x86-64 (v2, v3, v4) |
| R8 | Virtualização: suporte do processador (VT-x ou AMD-V), se está ligada no firmware e se o Hyper-V está ativo |

### Memória

| # | Requisito |
|---|---|
| R9 | Total instalado, total utilizável e a diferença reservada pelo hardware (`GetPhysicallyInstalledSystemMemory` e `GlobalMemoryStatusEx`) |
| R10 | Slots: quantos existem, quantos ocupados, capacidade máxima suportada pela placa, suporte a ECC (SMBIOS tipo 16) |
| R11 | Por módulo: slot, capacidade, tipo, formato (DIMM ou SODIMM), velocidade nominal e configurada em MT/s com o rótulo (por exemplo "DDR4-3200"), fabricante (nome ou código JEDEC traduzido pela tabela JEP106 embutida), part number, número de série, ranks, voltagem (SMBIOS tipo 17) |
| R12 | Uso atual: em uso, em cache, confirmada e tamanho do arquivo de paginação |
| R13 | Alertas: módulos com velocidades ou capacidades diferentes, memória rodando abaixo da velocidade nominal, um módulo só numa placa com dois ou mais canais (canal único provável), memória utilizável muito menor que a instalada |
| R14 | Resposta pronta para ampliação: "cabem até X GB, há Y slots livres, tipo Z" |
| R15 | Erros de memória: resultado do Diagnóstico de Memória do Windows, quando houver, e erros de hardware corrigidos ou não corrigidos registrados pelo WHEA (ver R27) |

### Placa-mãe e firmware

| # | Requisito |
|---|---|
| R16 | Placa-mãe: fabricante, modelo, versão, número de série (SMBIOS tipo 2). Chipset pelo identificador PCI da ponte do sistema (tabela embutida, melhor esforço) |
| R17 | Equipamento: fabricante, modelo, SKU, número de série, UUID, tipo de gabinete (desktop, notebook, all-in-one) (SMBIOS tipos 1 e 3) |
| R18 | BIOS: fabricante, versão, data e idade em anos, modo UEFI ou legado (`GetFirmwareType`), Secure Boot ligado ou desligado |
| R19 | TPM: presente, versão (1.2 ou 2.0), fabricante e estado (`Tbsi_GetDeviceInfo` sem administrador, detalhes com administrador) |

### Discos

| # | Requisito |
|---|---|
| R20 | Lista dos discos físicos: modelo, firmware, número de série, capacidade, tipo (HDD, SSD SATA, SSD NVMe, USB), interface e modo de transferência (SATA 3 Gb/s ou 6 Gb/s, PCIe geração e número de linhas), rotação em HDD, TRIM, estilo de partição (GPT ou MBR) |
| R21 | Volumes de cada disco: letra, rótulo, sistema de arquivos, espaço livre e total, BitLocker (com administrador) |
| R22 | SMART de disco ATA (HDD e SSD SATA): tabela completa dos atributos com ID, nome em português, valor atual, pior, limite do fabricante e valor bruto |
| R23 | SMART de disco NVMe (página de log 02h): alerta crítico, temperatura, reserva disponível e limite, percentual de vida usado, dados lidos e gravados, horas ligado, ciclos de energia, desligamentos inseguros, erros de mídia, entradas no registro de erros |
| R24 | Temperatura, horas ligado, ciclos de energia e total gravado em destaque, no formato brasileiro ("12.345 horas, 514 dias", "38,2 TB gravados") |
| R25 | Estado de saúde por disco, pelas regras da seção 8, com o motivo escrito ("3 setores realocados", "92% da vida útil usada") |
| R26 | Disco atrás de controladora RAID ou de ponte USB sem SMART aparece com os dados básicos e o aviso "SMART indisponível por esta controladora", nunca com saúde Bom |

### Estabilidade e erros

| # | Requisito |
|---|---|
| R27 | Eventos dos últimos 30 dias (e 90, a escolher), lidos do log Sistema do Windows, agrupados e contados: telas azuis, desligamentos inesperados, erros de disco e de sistema de arquivos, erros de hardware do WHEA (processador, memória, PCIe), resultado do Diagnóstico de Memória. Os IDs de cada evento **[CONFERIR]** |
| R28 | Índice de estabilidade do Monitor de Confiabilidade do Windows (1 a 10), quando disponível |
| R29 | Tempo ligado desde o último boot e data da instalação do Windows |
| R30 | Dispositivos com problema no Gerenciador de Dispositivos: nome, código do problema e o texto em português ("driver não instalado", "dispositivo desativado") |

### Vídeo, monitores e bateria

| # | Requisito |
|---|---|
| R31 | Placas de vídeo: nome, fabricante, memória dedicada real (o valor da WMI para em 4 GB e não serve), versão e data do driver |
| R32 | Monitores: fabricante, modelo, número de série, ano de fabricação, tamanho em polegadas e resolução nativa, pela leitura do EDID no registro |
| R33 | Bateria de notebook: capacidade de projeto, capacidade atual de carga total, desgaste em porcentagem, ciclos, fabricante e química |

### Windows e compatibilidade

| # | Requisito |
|---|---|
| R34 | Windows: edição, versão, compilação, arquitetura, estado da ativação |
| R35 | Verificação para o Windows 11, item por item: processador na lista da Microsoft (tabela embutida), TPM 2.0, UEFI, Secure Boot, 4 GB de memória, 64 GB de disco. O suporte ao Windows 10 terminou em 14/10/2025, e esta é a resposta que o cliente pede para saber se troca a máquina ou só atualiza |
| R36 | Placas de rede: nome, MAC e velocidade. Só a lista. O detalhe de rede é assunto do MapNet |

### Saída

| # | Requisito |
|---|---|
| R37 | Copiar qualquer seção como texto, para colar em chamado ou e-mail |
| R38 | Relatório HTML de arquivo único com a marca da MT, imprimível em PDF pelo navegador, com o Resumo na primeira página |
| R39 | Arquivo JSON com todos os dados, para a MT guardar e comparar coletas. O nome do arquivo leva o nome do computador e a data |
| R40 | CSV de uma linha por máquina, para juntar várias coletas numa planilha. Separador ponto e vírgula, UTF-8 com BOM, como no MapNet |
| R41 | Linha de comando para coletar sem abrir a janela, inclusive gravando numa pasta de rede, para rodar por roteiro de logon em todas as estações de um cliente |
| R42 | Modo `--demonstracao` com dados fictícios, para imagem de tela e página |

## 6. Estados de um campo

Regra herdada do MapDisk: **nunca mostrar 0 ou vazio onde não houve leitura.** Todo campo tem um destes estados, com texto e cor próprios:

| Estado | Texto na tela | Quando |
|---|---|---|
| Lido | O valor | A fonte respondeu |
| Não informado | "não informado pelo fabricante" | A fonte respondeu vazio ou com texto de fábrica ("To Be Filled By O.E.M.", "Default string") |
| Requer administrador | "requer administrador", com o botão ao lado | A leitura precisa de elevação |
| Não suportado | "não disponível neste equipamento" | O hardware não tem o recurso, como bateria num desktop |
| Erro de leitura | "erro de leitura", com o motivo na dica | A fonte falhou |

Cada campo guarda de onde veio (CPUID, SMBIOS, SMART, WMI, registro, log de eventos). A dica do campo mostra a fonte, para o técnico saber quanto confiar no dado.

## 7. Arquitetura

A divisão segue o MapDisk:

| Pasta | Conteúdo |
|---|---|
| `src/maphard.nucleo` | Biblioteca `net8.0-windows` sem tela: toda a leitura e a interpretação. É o que os testes cobrem |
| `src/maphard` | Aplicativo WPF e ponto de entrada da linha de comando. Gera o `maphard.exe` |
| `testes/maphard.testes` | Testes xUnit do núcleo |
| `ferramentas/` | Roteiros de apoio, como o `publicar.cmd` e a atualização das tabelas embutidas |
| `public/` | Página do programa, em `maphard.manfred.com.br` **[DECIDIR]** |
| `docs/superpowers/` | Specs, planos e pendências |

### Módulos do núcleo

A leitura e a interpretação ficam separadas. O leitor só busca os bytes da fonte. O intérprete transforma os bytes em dados e é testado com amostras gravadas, sem depender da máquina onde o teste roda.

| Módulo | O que faz | Fonte |
|---|---|---|
| `cpuid/` | Decodifica fabricante, família, modelo, instruções, caches e topologia | `X86Base.CpuId` |
| `smbios/` | Interpreta a tabela SMBIOS: tipos 0 (BIOS), 1 (sistema), 2 (placa-mãe), 3 (gabinete), 4 (processador), 16 (conjunto de memória) e 17 (módulo de memória) | `GetSystemFirmwareTable("RSMB")` |
| `processador/` | Junta CPUID, SMBIOS, clocks, uso e a tabela de codinomes | `cpuid/`, `smbios/`, contadores de desempenho |
| `memoria/` | Módulos, slots, uso e os alertas do R13 | `smbios/`, APIs de memória do Windows |
| `discos/` | Lista de discos e volumes, interface e modo de transferência | APIs de armazenamento e SetupAPI |
| `smart/` | Leitura e interpretação do SMART ATA e NVMe, nomes dos atributos em português | `DeviceIoControl` |
| `saude/` | Regras da seção 8 e o estado de cada cartão do Resumo | Os módulos acima |
| `eventos/` | Contagem dos eventos do R27 e índice de estabilidade | Log Sistema do Windows, WMI |
| `dispositivos/` | Dispositivos com problema, vídeo, monitores (EDID), bateria, rede | SetupAPI, registro, WMI |
| `windows/` | Edição, ativação, verificação do Windows 11, TPM, Secure Boot, BitLocker | Registro, TBS, WMI |
| `tabelas/` | Tabelas embutidas: fabricantes JEDEC, codinomes de processador, processadores aceitos pelo Windows 11, nomes de atributos SMART, chipsets | Arquivos no `.exe` |
| `relatorios/` | HTML com a marca da MT, JSON e CSV | Todos |
| `linha-de-comando/` | Leitura dos argumentos, no padrão do MapNet e do MapDisk | Nada |
| `painel/` | Estado da tela, fora do WPF para ser testável | Todos |

### Aplicativo

- `programa.cs`: sem argumentos abre a janela. Com argumentos roda a linha de comando. `--demonstracao` abre com dados fictícios. `--elevado` é usado pelo botão "Ler como administrador".
- Tema `tema-mt.xaml`, fonte Montserrat e logo da MT copiados do MapDisk.
- A coleta roda em segundo plano, seção por seção. A janela abre na hora e cada cartão se preenche quando a sua leitura termina.

### Janela principal

- **Faixa do topo**, verde, com o símbolo do MapHard, o logo da MT e os botões: Atualizar, Ler como administrador, Copiar, Salvar relatório (HTML, JSON, CSV), Sobre.
- **Navegação à esquerda:** Resumo, Processador, Memória, Placa-mãe e firmware, Discos, Estabilidade, Vídeo e monitores, Bateria, Windows, Dispositivos.
- **Conteúdo à direita**, em cartões. Em Discos, um cartão por disco, com o estado de saúde grande no alto, temperatura, horas e total gravado ao lado, e a tabela SMART embaixo. Em Memória, um cartão por slot, inclusive os vazios.
- **Barra de status:** nome do computador, se está como administrador, data e hora da coleta.

### Linha de comando

Exemplos:

```bat
maphard coletar --html estacao.html
maphard coletar --json estacao.json --csv estacao.csv
maphard coletar --pasta \\servidor\inventario
```

Com `--pasta`, o arquivo recebe o nome do computador e a data, para vários computadores gravarem na mesma pasta sem sobrescrever um ao outro. Sem administrador, o arquivo sai com os campos no estado "requer administrador".

## 8. Regras de saúde

Valores iniciais, a confirmar com o Manfred no plano da fatia de discos. Cada regra que dispara vira uma frase no motivo.

### Disco ATA (HDD e SSD SATA)

| Estado | Quando |
|---|---|
| Ruim | Algum atributo com valor atual igual ou abaixo do limite do fabricante (quando o limite não é zero), ou o próprio disco informa falha prevista |
| Atenção | Setores realocados (05h), eventos de realocação (C4h), setores pendentes (C5h) ou setores incorrigíveis (C6h) acima de zero. Vida restante informada pelo SSD abaixo de 10%. Temperatura acima de 50 °C em HDD ou 70 °C em SSD |
| Bom | Nenhuma das anteriores |
| Desconhecido | SMART não lido |

### Disco NVMe

| Estado | Quando |
|---|---|
| Ruim | Qualquer bit do alerta crítico ligado: reserva abaixo do limite, temperatura fora da faixa, confiabilidade degradada, mídia só de leitura |
| Atenção | Percentual de vida usado igual ou acima de 90%. Erros de mídia acima de zero. Reserva disponível a menos de 10 pontos do limite. Temperatura acima do limite de aviso informado pelo próprio disco |
| Bom | Nenhuma das anteriores |
| Desconhecido | SMART não lido |

### Outros cartões do Resumo

| Cartão | Atenção | Ruim |
|---|---|---|
| Memória | Módulos diferentes, abaixo da velocidade nominal, canal único provável | Erro no Diagnóstico de Memória ou erro de memória no WHEA |
| Estabilidade | Um desligamento inesperado ou uma tela azul nos últimos 30 dias | Três ou mais telas azuis, ou erro de hardware não corrigido no WHEA |
| Dispositivos | Algum dispositivo com problema | Nenhum caso. Dispositivo com problema fica em Atenção |
| Windows 11 | Algum requisito não atendido que se resolve na configuração (TPM ou Secure Boot desligados, modo legado) | Processador fora da lista ou memória abaixo de 4 GB |
| Bateria | Desgaste acima de 30% | Desgaste acima de 50% |
| Processador | Virtualização desligada no firmware, quando o Hyper-V é pedido | Erro de processador no WHEA |

## 9. Erros

| Situação | Comportamento |
|---|---|
| Fonte não responde (WMI parado, por exemplo) | O campo fica "erro de leitura". As outras seções seguem |
| SMART sem administrador | O cartão do disco mostra os dados básicos, "requer administrador" e o botão |
| Controladora RAID ou ponte USB sem SMART | Aviso do R26. Saúde Desconhecido |
| Log de eventos sem permissão | O cartão Estabilidade fica Desconhecido, com o motivo |
| Leitura demorada (WMI lento, disco dormindo) | Tempo limite por fonte. O campo fica "erro de leitura: tempo esgotado" e a coleta segue |
| Pasta de destino do relatório sem gravação | Mensagem clara. Nada é perdido: a coleta continua na tela |

## 10. Desempenho

- A janela abre em menos de 1 segundo, com os cartões se preenchendo.
- Coleta completa em menos de 10 segundos numa estação comum, com administrador.
- A coleta não pesa na máquina do cliente: sem benchmark, sem teste de estresse, leitura dos discos só pelos comandos SMART.

## 11. Privacidade e segredos

- **O que o programa lê:** dados de hardware e de configuração do Windows. Não lê arquivos do usuário.
- **Dado pessoal:** o relatório traz o nome do computador, que muitas vezes tem o nome da pessoa ("NOTE-MARIA"), números de série e, se entrar, o usuário do Windows. A MT, no atendimento, age por instrução do cliente. O enquadramento pela LGPD e o texto de uso autorizado passam pela `legal-br` antes da primeira versão pública, no mesmo formato das verificações do MapNet e do MapDisk. Pendência.
- **O que fica fora:** chave de produto do Windows e de qualquer outro programa. O MapHard não lê nem mostra.
- **Onde o dado fica:** só onde o técnico salvar. O programa não envia nada para fora da máquina.
- **Repositório:** coleta, relatório ou imagem de tela de máquina de cliente nunca entram no git. As amostras de teste (tabela SMBIOS, página SMART) são montadas à mão ou vêm de máquina da MT com os números de série trocados por fictícios.
- **Segredos:** o programa não usa senha, chave nem token.

## 12. Ordem das fatias

| Fatia | Conteúdo |
|---|---|
| 1 | Base: solução, tema da MT, janela com faixa e navegação, estados de campo, CPUID e SMBIOS, seções Processador e Placa-mãe e firmware, JSON e linha de comando `coletar --json` (R2 a R8, R16 sem o chipset, R17 a R19, R39, R41 parcial, R42) |
| 2 | Memória completa com os alertas (R9 a R14) |
| 3 | Discos: lista, volumes, SMART ATA e NVMe, regras de saúde, "Ler como administrador" (R20 a R26) |
| 4 | Estabilidade, erros de memória, dispositivos com problema e chipset (R15, R27 a R30, chipset do R16) |
| 5 | Vídeo, monitores, bateria, rede, Windows e verificação do Windows 11 (R31 a R36) |
| 6 | Resumo com os cartões e as regras da seção 8 (R1) |
| 7 | Relatório HTML com a marca da MT, CSV, copiar seção, página `public/` (R37, R38, R40, R41 completo) |

Depois da versão 1:

| Fatia | Conteúdo |
|---|---|
| 8 | Módulo Sensores, opcional (seção 14) |

A ordem das fatias 2 a 7 pode mudar por decisão do Manfred. Em 30/09/2026, o Manfred aprovou passar o chipset da fatia 1 para a fatia dos dispositivos, porque os dois saem da mesma leitura (SetupAPI). O Resumo fica para a fatia 6 porque depende de todas as áreas lidas.

## 13. Prioridade dos testes

1. **Interpretação das fontes:** tabela SMBIOS (inclusive tabela truncada ou com texto vazio), CPUID de processadores Intel e AMD de gerações diferentes, página SMART ATA com os limites, página de log NVMe, EDID. Amostra malformada não pode travar nem derrubar o programa.
2. **Regras de saúde:** cada linha da seção 8, com o motivo escrito.
3. **Estados de campo:** nenhum campo sem leitura vira 0 ou vazio.
4. **Formatação:** tamanhos, horas, datas e porcentagens em pt-BR.
5. **Relatórios:** HTML, JSON e CSV com os mesmos dados da tela.
6. **Linha de comando:** leitura dos argumentos e nome do arquivo gerado.
7. **Convenções herdadas:** caracteres proibidos, nome de arquivo em minúsculas, fonte e licença embutidas, nenhum dado real no repositório.

Teste manual antes de cada versão: um desktop e um notebook, um com HDD e um com SSD NVMe, com e sem administrador, Windows 10 e Windows 11.

## 14. Fora da versão 1

- Driver de kernel e tudo o que depende dele: temperatura e voltagem do processador, rotação de ventoinhas, timings da memória e SPD. Vão para o módulo Sensores, descrito abaixo.
- Benchmark de processador e de disco.
- Teste de memória pelo próprio programa. O MapHard só mostra o resultado do Diagnóstico de Memória do Windows.
- Coleta de outra máquina pela rede.
- Histórico e comparação entre coletas na janela. O JSON já guarda o que for preciso para isso.
- Consulta de garantia no site do fabricante, que exigiria internet.
- Windows de 32 bits e Windows em processador ARM.

### Módulo Sensores (fatia 8, depois da versão 1)

Decisão do Manfred em 30/09/2026: a versão 1 sai sem driver, e as leituras que exigem driver entram depois, num módulo opcional.

- **O que mostra:** temperatura, clock real e voltagem por núcleo do processador, voltagens e ventoinhas da placa-mãe, conteúdo do SPD de cada pente (timings de fábrica, perfis XMP e EXPO) e os timings em uso.
- **Como lê:** pela biblioteca LibreHardwareMonitor (C#, licença MPL-2.0, compatível com a GPL-3.0), que acessa o hardware pelo driver PawnIO. Os módulos do PawnIO que a biblioteca carrega têm licença LGPL-2.1. Conferido no código da biblioteca em 30/09/2026, na versão de 29/09/2026.
- **O PawnIO é instalado no Windows,** com instalador próprio. A biblioteca só usa o driver quando ele já está instalado, e o programa de exemplo da biblioteca oferece o instalador quando falta. Isso conflita com a regra "sem instalar nada" do MapHard. Pendência para o desenho da fatia 8: o MapHard só usa o PawnIO já instalado, ou oferece a instalação com confirmação do técnico e aviso ao cliente.
- **Quando carrega:** só quando o técnico abre a seção Sensores, com administrador. Sem o PawnIO, ou com o driver bloqueado pelo Windows (Integridade da memória, antivírus), a seção diz o motivo e o restante do programa segue igual.
- **Referência de leitura:** o código do CrystalDiskInfo (licença MIT) serve de consulta para a fatia de discos, principalmente pontes USB e atributos de cada fabricante. Código aproveitado de lá leva o aviso de licença original junto.

## 15. Perguntas em aberto

| # | Pergunta |
|---|---|
| 1 | Windows Server entra nos testes da versão 1? |
| 2 | Abrir como usuário comum com o botão "Ler como administrador" (padrão do MapDisk) ou pedir administrador já na abertura, já que o SMART precisa? Respondida em 01/10/2026: abre como usuário comum, com o botão "Ler como administrador" |
| 3 | O JSON deve seguir o formato do projeto `inventario-estacoes`, para as coletas entrarem direto lá? |
| 4 | Endereço da página: `maphard.manfred.com.br`? Respondida em 02/10/2026: sim, `maphard.manfred.com.br` |
| 5 | Arte do símbolo do MapHard, no mesmo estilo dos símbolos do MapNet e do MapDisk. [PREENCHER] |
| 6 | Quando o repositório passa a público. Respondida em 02/10/2026: passou a público nesse dia, antes da publicação da página |
