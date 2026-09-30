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
| Constantes de firmware sem documentação lida | Os valores TPM_VERSION_12 = 1 e TPM_VERSION_20 = 2 do tbs.h, a chave `UEFISecureBootEnabled` do Secure Boot e o formato do valor `Update Revision` (microcódigo) não estão na documentação que a sessão conseguiu ler. Estão marcados com [CONFERIR] em `firmware/leitor-firmware.cs` | Conferir na documentação da Microsoft e no teste da fase desktop, comparando com o que o Windows mostra |
| Janela WPF não compila na nuvem | O SDK do .NET 8 do Ubuntu não traz o Microsoft.NET.Sdk.WindowsDesktop, e o SDK da Microsoft vem de um endereço bloqueado pela rede da sessão. Na nuvem, os testes rodam pelo projeto de testes (`dotnet test testes/maphard.testes/maphard.testes.csproj`), e a janela compila só no CI em Windows | Decidido pelo Manfred em 30/09/2026: segue só com o CI em Windows até a fase desktop, sem liberar o SDK da Microsoft na nuvem. Fecha na fase desktop |
| Texto "Licença e garantias" da janela Sobre | No MapDisk, esse texto saiu da verificação jurídica dele. Texto jurídico não sai de memória, então a Sobre do MapHard traz só o aviso de software livre da GPL-3.0 | Verificação da `legal-br` para o MapHard, junto com o texto de uso autorizado |
| Ícone do `.exe` | Sem a arte do MapHard, o programa sai sem ícone próprio e a faixa mostra só o nome | Arte do Manfred |
| Site da DMTF e documentação da Microsoft fora do alcance | A rede da sessão bloqueia `dmtf.org` e `learn.microsoft.com`. A documentação da Microsoft foi lida pelo repositório `MicrosoftDocs/sdk-api` no GitHub, e os deslocamentos do SMBIOS pelo `dmidecode` | Liberar os dois domínios nas configurações de rede do ambiente, ou conferir na fase desktop |

## Depois da versão 1

| Item | Motivo | O que fecha |
|---|---|---|
| Instalação do PawnIO no módulo Sensores | O driver precisa ser instalado no Windows, o que conflita com a regra "sem instalar nada" | Decisão do Manfred no desenho da fatia 8: usar só o PawnIO já instalado, ou oferecer a instalação com confirmação |
