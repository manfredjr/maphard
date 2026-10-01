# Passagem para a fase desktop

Data: 30/09/2026. Autor: Manfred Heil Junior.

A fase de nuvem termina aqui, por decisão do Manfred em 30/09/2026. O trabalho segue numa sessão no computador dele, na pasta `C:\COWORK\CODE\MAPHARD-MT`. Este arquivo é o ponto de partida dessa sessão: diz onde o projeto está e o que vem a seguir. A conversa da nuvem não é registro; o que vale está no repositório.

## Onde o projeto está

| Fatia | Situação | Onde |
|---|---|---|
| 0 | Regras, licença e desenho da versão 1 | `AGENTS.md`, `docs/superpowers/specs/2026-09-30-maphard-design.md` |
| 1 | Concluída: processador, placa-mãe e firmware, JSON, linha de comando e janela | Plano no PR #1, código no PR #2 |
| 2 | Plano aprovado; código não começou | `docs/superpowers/plans/2026-09-30-maphard-fatia-2-memoria.md`, PR #3 |
| 3 a 7 | Só no desenho | Seção 12 do desenho |
| 8 | Módulo Sensores, depois da versão 1 | Seção 14 do desenho |

O `main` tem tudo o que foi aprovado. O CI em Windows rodou verde em todos os commits da fatia 1.

## Primeira coisa a fazer na sessão local

1. Ler o `AGENTS.md` inteiro.
2. Preparar a pasta e rodar os portões:

```bat
cd /d C:\COWORK\CODE
git clone https://github.com/manfredjr/maphard.git MAPHARD-MT
cd MAPHARD-MT
git config user.name "Manfred Heil Junior"
git config user.email "manfred@manfred.com.br"
git config core.hooksPath .githooks
dotnet build maphard.sln -c Release
dotnet test maphard.sln -c Release
ferramentas\publicar.cmd
```

Se a pasta já existir, trocar o `git clone` por `git switch main` e `git pull`.

3. Fazer o teste da fatia 1 na máquina real, pela seção "Como testar" do PR #2: `publicar\maphard.exe`, `publicar\maphard.exe --demonstracao` e `start /wait publicar\maphard.exe coletar --json`. Comparar com o `msinfo32`, o `tpm.msc` e o programa de referência. O resultado do teste e o JSON da máquina ficam na conversa, nunca no git.

## O que a nuvem não conseguiu conferir

Estes itens estão em `docs/superpowers/pendencias.md`, seção "Fatia 1", e fecham na fase desktop:

- **Constantes de firmware** marcadas com `[CONFERIR]` em `src/maphard.nucleo/firmware/leitor-firmware.cs`: versões do TPM (1 para 1.2, 2 para 2.0), a chave `UEFISecureBootEnabled` e o formato do valor `Update Revision` do microcódigo. Conferir comparando a tela do MapHard com o `msinfo32` e o `tpm.msc`.
- **Janela WPF:** na nuvem, ela só compilava no CI. No desktop, compila e roda direto.
- **Documentação da Microsoft e da DMTF:** a nuvem leu a da Microsoft pelo repositório `MicrosoftDocs/sdk-api` e os deslocamentos do SMBIOS pelo `dmidecode`. No desktop, `learn.microsoft.com` está acessível.
- **Litografia do processador:** a coluna `litografia` de `src/maphard.nucleo/tabelas/processadores.csv` está vazia por falta de fonte. Preencher com a página de especificação do fabricante, citada na coluna `fonte`.

## Próximo passo depois do teste

1. Corrigir o que o teste da fatia 1 apontar, num ramo novo a partir do `main`, com Pull Request.
2. Começar o código da fatia 2 pelo plano já aprovado, num ramo `memoria`.

## Decisões pendentes do Manfred

Seção 15 do desenho: Windows Server nos testes, abertura com ou sem administrador, formato do JSON para o projeto `inventario-estacoes`, endereço da página, arte do símbolo e quando o repositório passa a público.

## Cuidado com Pull Requests

A ferramenta que cria o PR pode acrescentar sozinha um rodapé com o nome dela e o link da sessão. Isso fere a regra de autoria. Depois de criar ou editar um PR, reler o corpo no GitHub e tirar o rodapé, se aparecer. Aconteceu nos PRs #1 e #3, e o rodapé foi retirado nos dois.
