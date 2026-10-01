# Pendências

O que ficou de fora, com o motivo e o que fecha o item.

## Dia zero

| Item | Motivo | O que fecha |
|---|---|---|
| Ramo `main` | O repositório nasceu vazio | Fechado em 30/09/2026: criado a partir do dia zero, com autorização do Manfred. Daí em diante, tudo entra por Pull Request. Falta o Manfred marcar o `main` como ramo padrão no GitHub |
| Perguntas da seção 15 do desenho | Windows Server, administrador, formato do JSON, endereço da página, arte do símbolo, repositório público | Resposta do Manfred, gravada no desenho |
| Pasta do projeto no desktop | `C:\COWORK\CODE\MAPHARD-MT` segue o padrão dos outros dois | Fechado em 30/09/2026: confirmada pelo Manfred |
| Verificação jurídica | Enquadramento pela LGPD (nome do computador, números de série, usuário) e texto de uso autorizado | `legal-br`, no formato das verificações do MapNet e do MapDisk, antes da primeira versão pública |
| Arte do MapHard | Não há símbolo nem logo do MapHard | Arte do Manfred, no estilo dos símbolos do MapNet e do MapDisk |

## Fatia 1

| Item | Motivo | O que fecha |
|---|---|---|
| Litografia do processador | A tabela de processadores só aceita linha com fonte. O kernel Linux, usado para os codinomes da Intel e as microarquiteturas da AMD, não traz a litografia. As páginas da Intel e da AMD estão fora do alcance da sessão na nuvem | Preencher a coluna `litografia` de `tabelas/processadores.csv` com a página de especificação do fabricante como fonte, na fase desktop ou com o acesso liberado |
| Constantes de firmware sem documentação lida | Os valores TPM_VERSION_12 = 1 e TPM_VERSION_20 = 2 do tbs.h, a chave `UEFISecureBootEnabled` do Secure Boot e o formato do valor `Update Revision` (microcódigo) não estão na documentação que a sessão conseguiu ler. Estão marcados com [CONFERIR] em `firmware/leitor-firmware.cs` | Em parte fechado em 01/10/2026, no teste da fase desktop: a chave do Secure Boot bate com o msinfo32, o TPM 2.0 lido bate com o Windows e o `Update Revision` de 4 bytes passou a ser lido. Os números do TPM_VERSION (1 e 2) foram conferidos no tbs.h do SDK, pela cópia do repositório `microsoft/win32metadata`. Falta conferir o formato de 8 bytes do `Update Revision`, numa máquina que grave o valor assim |
| Comparação com o programa de referência | Na máquina do primeiro teste da fase desktop, o programa de referência não estava instalado. A comparação foi feita com o msinfo32, o registro e o WMI | Repetir o teste da fatia 1 com o programa de referência instalado |
| Janela WPF não compila na nuvem | O SDK do .NET 8 do Ubuntu não traz o Microsoft.NET.Sdk.WindowsDesktop, e o SDK da Microsoft vem de um endereço bloqueado pela rede da sessão. Na nuvem, os testes rodam pelo projeto de testes (`dotnet test testes/maphard.testes/maphard.testes.csproj`), e a janela compila só no CI em Windows | Decidido pelo Manfred em 30/09/2026: segue só com o CI em Windows até a fase desktop, sem liberar o SDK da Microsoft na nuvem. Fecha na fase desktop |
| Texto "Licença e garantias" da janela Sobre | No MapDisk, esse texto saiu da verificação jurídica dele. Texto jurídico não sai de memória, então a Sobre do MapHard traz só o aviso de software livre da GPL-3.0 | Verificação da `legal-br` para o MapHard, junto com o texto de uso autorizado |
| Ícone do `.exe` | Sem a arte do MapHard, o programa sai sem ícone próprio e a faixa mostra só o nome | Arte do Manfred |
| Site da DMTF e documentação da Microsoft fora do alcance | A rede da sessão bloqueia `dmtf.org` e `learn.microsoft.com`. A documentação da Microsoft foi lida pelo repositório `MicrosoftDocs/sdk-api` no GitHub, e os deslocamentos do SMBIOS pelo `dmidecode` | Liberar os dois domínios nas configurações de rede do ambiente, ou conferir na fase desktop |

## Fatia 2

| Item | Motivo | O que fecha |
|---|---|---|
| Alerta "módulos diferentes" por part number | Pelo plano, part numbers diferentes disparavam o alerta mesmo com capacidade e velocidade iguais. No primeiro teste em máquina real, um notebook de fábrica com dois módulos do mesmo fabricante, de mesma capacidade e velocidade, recebeu o alerta por causa da revisão do chip no part number | Fechado em 01/10/2026: o Manfred decidiu tirar o part number da regra. O alerta vale só para capacidade ou velocidade nominal diferentes |
| Código JEDEC inválido | O plano pedia mostrar "código inválido" como valor. Pela regra 2 do produto, o campo fica "não informado", com "código JEDEC inválido" no motivo | Confirmação do Manfred no PR da fatia 2 |
| Arquivo de paginação no "Uso agora" | O R12 cita o tamanho do arquivo de paginação; o plano trocou pelo limite da memória confirmada (CommitLimit), que soma a memória física e a paginação | Decidir se o tamanho do arquivo de paginação entra separado, numa fatia futura |
| Teste em máquina com slot livre e em desktop | O teste da fase desktop rodou num notebook com os dois slots ocupados. Slot vazio, canal único e ampliação com slot livre só foram vistos nos testes automáticos e na demonstração | Teste do Manfred num desktop e numa máquina com um pente só |
| Comparação com o programa de referência | O programa de referência não estava instalado na máquina do teste. A comparação foi feita com o WMI (`Win32_PhysicalMemory` e `Win32_PhysicalMemoryArray`) | Repetir com o programa de referência instalado |

## Fatia 3

| Item | Motivo | O que fecha |
|---|---|---|
| SMART ATA, BitLocker e o botão "Ler como administrador" sem teste em máquina real | A máquina do teste da fase desktop tem só um SSD NVMe, e a sessão roda sem elevação. O pedido de elevação do Windows e a leitura elevada só foram vistos nos testes automáticos | Teste do Manfred numa máquina com disco SATA: abrir, usar o botão, aceitar o pedido do Windows e comparar a tabela SMART com o programa de referência |
| Cores de Atenção e Ruim fora do tema da MT | O `tema-mt.xaml` do MapDisk não tem amarelo nem vermelho. As duas cores ficaram nos recursos da janela principal | Revisão do Manfred: aprovar as cores ou trazer as do MapDisk, se ele tiver |
| Tabela SMART em linhas, não em grade | A tela usa o cartão de linhas das outras seções: "05h Setores realocados" e "atual 99, pior 99, limite 36, bruto 3". O desenho fala em tabela com colunas | Grade com colunas na janela, junto com o relatório HTML (fatia 7), ou antes, se o Manfred pedir |
| Dados gravados e vida usada de disco SATA | O atributo de dados gravados (F1h) e os de vida do SSD mudam de unidade e de significado conforme o fabricante. Os dois campos ficam "não informado" e a tabela SMART mostra os valores | Tabela por fabricante a partir do `drivedb.h` do smartmontools, se o Manfred quiser |
| Volume em mais de um disco | O volume que ocupa vários discos (volume dinâmico, Espaços de Armazenamento) fica em "Volumes sem disco físico ligado" | Leitura das extensões do volume, se aparecer em cliente |
| Janela não vista na sessão | A sessão não enxerga a tela. A seção Discos, o selo da saúde e o botão foram conferidos pelos testes do painel e pela linha de comando | Conferência do Manfred na janela e na `--demonstracao` |

## Fatia 4

| Item | Motivo | O que fecha |
|---|---|---|
| Ids de evento do WHEA e do Diagnóstico de Memória | Só aparecem em respostas de fórum. A gravidade vem do nível do evento e a tela mostra a mensagem do Windows. Na máquina do teste da fase desktop não havia nenhum desses eventos | Conferir num evento real: rodar o Diagnóstico de Memória numa máquina de teste e ver o evento; numa máquina com erro de hardware, conferir o nível e a mensagem |
| Erro de memória do WHEA no cartão Memória | O desenho pede o cartão Memória em Ruim com erro de memória no WHEA. Sem fonte para saber o componente do evento, esses erros contam só na Estabilidade | Achar na documentação ou num evento real o campo que diz o componente |
| Ids de disco 7 e 11 e provedor do NTFS | Os ids 7 e 11 do `disk` só têm fonte de fórum; a página da Microsoft não diz o provedor dos eventos do NTFS. O MapHard lê os dois provedores (`Ntfs` e `Microsoft-Windows-Ntfs`) | Conferir num evento real |
| Tela azul pelo 1001 e desligamento pelo 6008 | A contagem sai só do Kernel-Power 41, que tem página oficial; o 1001 do WER e o 6008 contariam a mesma queda duas vezes | Nada, se o 41 bastar nos testes do Manfred |
| Índice de estabilidade pela API de script | O provedor de confiabilidade só entrega as propriedades do primeiro item da consulta; a leitura vai em janelas crescentes e lê o primeiro item da menor janela com resultado | Ler pela API COM do WMI, se o valor divergir do Monitor de Confiabilidade |
| Comparação com o Visualizador de Eventos, o Monitor de Confiabilidade e o Gerenciador de Dispositivos | Feita na máquina do teste com o WMI e o `Get-WinEvent`; sem tela azul, desligamento ou dispositivo com problema reais | Teste do Manfred numa máquina com esses casos |

## Depois da versão 1

| Item | Motivo | O que fecha |
|---|---|---|
| Instalação do PawnIO no módulo Sensores | O driver precisa ser instalado no Windows, o que conflita com a regra "sem instalar nada" | Decisão do Manfred no desenho da fatia 8: usar só o PawnIO já instalado, ou oferecer a instalação com confirmação |
