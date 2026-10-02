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

## Fatia 5

| Item | Motivo | O que fecha |
|---|---|---|
| Identificador do Windows na consulta da ativação | O identificador de aplicativo do Windows (55c92734-d682-4d71-983e-d6ec3f16059f) não está na página do SLIsGenuineLocal. Foi conferido numa máquina ativada, onde é o ApplicationID das licenças do Windows e a consulta devolve "ativado" | Conferir numa máquina não ativada (deve dar "licença inválida") e achar uma fonte oficial |
| Nome do valor do EDID no registro | A página "Overriding monitor EDIDs" diz que o EDID fica na chave de hardware do monitor, mas não dá o nome do valor; "EDID" só aparece em fórum. A tela embutida do notebook do teste foi lida certo | Teste do Manfred num desktop com monitor externo, comparando com a etiqueta |
| Lista de processadores do Windows 11 por série | Na versão 25H2, a Microsoft publica a lista por série ("12th Generation Core i5", "Ryzen 7000 Series"), não por modelo. O MapHard segue a lista ao pé da letra. Processador lançado depois da lista fica "não consta na lista do MapHard", nunca "não aceita" | Rodar `ferramentas\gerar-processadores-windows11.ps1` a cada versão nova do Windows 11 |
| Secure Boot desligado | O plano pede Secure Boot ligado; a Microsoft pede a máquina capaz de Secure Boot. Desligado com UEFI fica em Atenção, com "ligar no firmware", nunca em Ruim | Revisão do Manfred |
| Velocidade da rede sem fio | A velocidade mostrada é a de recepção. No Wi-Fi, a de envio pode ser outra, e é a que o `Get-NetAdapter` mostra | Nada, se a de recepção bastar; senão mostrar as duas |
| Ciclos de carga | Bateria que não conta ciclos devolve zero, e o campo fica "a bateria não informa" | Comparar com o `powercfg /batteryreport` em outros notebooks |
| Verificação do Windows 11 numa máquina com Windows 10 | A máquina do teste já tem Windows 11. Os casos que não atendem só foram vistos nos testes automáticos e na demonstração | Teste do Manfred numa máquina com Windows 10, comparando item por item com o PC Integridade do Computador |
| Janela não vista na sessão | As seções novas foram conferidas pelos testes do painel e pela linha de comando | Conferência do Manfred na janela e na `--demonstracao` |

## Fatia 6

| Item | Motivo | O que fecha |
|---|---|---|
| Serviços do Hyper-V e do WSL | O Hyper-V conta como pedido quando o serviço `vmms` ou o `WslService` existe no registro, decisão do Manfred de 02/10/2026. Os dois nomes foram vistos numa máquina com os dois recursos ligados; a ausência deles numa máquina sem os recursos não foi vista | Conferir numa máquina sem Hyper-V e sem WSL |
| Processador em Ruim pelo WHEA | O desenho pede o cartão Processador em Ruim com erro de processador no WHEA. Sem saber o componente do erro (pendência da fatia 4), o cartão não usa esse motivo | Fechar a pendência da fatia 4 sobre o componente do erro |
| JSON sem a saúde | O JSON grava as leituras; os estados do Resumo saem das regras e não vão no arquivo | Incluir os cartões no JSON, se o Manfred quiser, junto com o relatório HTML da fatia 7 |
| Teclado no cartão | O cartão do Resumo abre a seção com Enter ou espaço, depois de receber o foco pelo Tab. Só o clique com o mouse foi visto na janela | Conferência do Manfred |

## Fatia 7

| Item | Motivo | O que fecha |
|---|---|---|
| Publicação da página | A página em `public/` está pronta para `maphard.manfred.com.br`, mas publicar é ação que pede autorização | Frase do Manfred para publicar, pelo roteiro do cPanel, depois da primeira versão no GitHub Releases, que o botão de download usa |
| Primeira versão no GitHub Releases | O link "Baixar para Windows" aponta para a última versão publicada, que ainda não existe | Autorização do Manfred para criar a marca `v1.0.0` |
| Inventário em cliente pela pasta de rede | A verificação jurídica (`docs/legal/verificacao-pagina-maphard.md`, seção 4.2) recomenda, fora do programa, registrar o pedido do cliente na ordem de serviço, gravar numa pasta com acesso restrito e entregar o relatório só ao contato que pediu | Decisão de processo do Manfred |
| Biblioteca da `legal-br` sem relatório de fontes | A cópia clonada não tem o `STATUS-FONTES.md` e a última coleta é de 21/08/2026. Os artigos citados foram conferidos direto no Planalto em 02/10/2026 | Rodar o `VERIFICAR-FONTES.bat` no repositório da `legal-br` |
| Arte do símbolo do MapHard | A página e o ícone usam o logo da MT, como a janela, até a arte chegar (pergunta 5 do desenho) | Arte do Manfred |
| Imagem do relatório na página | Feita pelo Edge sem janela, a partir do relatório da demonstração | Nada, se o Manfred aprovar a imagem |

## Licença e titularidade

| Item | Motivo | O que fecha |
|---|---|---|
| Cessão do autor para a MT | O aviso de copyright põe a MANFRED TECNOLOGIA LTDA como titular, mas a transferência dos direitos patrimoniais do Manfred para a empresa exige documento escrito (Lei nº 9.610/1998, arts. 49 e 50). A licença GPL-3.0 não muda: a cessão muda quem é o titular, não a licença livre já concedida | Cessão única de todos os produtos, assinada antes da primeira venda, com o MapHard no Anexo I |

## Depois da versão 1

| Item | Motivo | O que fecha |
|---|---|---|
| Instalação do PawnIO no módulo Sensores | O driver precisa ser instalado no Windows, o que conflita com a regra "sem instalar nada" | Decisão do Manfred no desenho da fatia 8: usar só o PawnIO já instalado, ou oferecer a instalação com confirmação |
