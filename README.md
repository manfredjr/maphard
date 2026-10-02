# MapHard - MT

Programa para Windows que mostra o hardware do computador (processador, memória, placa-mãe, discos, vídeo, bateria) e aponta, logo na primeira tela, o que exige atenção: disco no fim da vida, erro de memória, desligamentos inesperados, dispositivo sem driver, compatibilidade com o Windows 11. Da MT - Manfred Tecnologia.

Software livre, sob licença [GPL-3.0](LICENSE).

As regras do projeto estão no [`AGENTS.md`](AGENTS.md) e o desenho em [`docs/superpowers/specs/2026-09-30-maphard-design.md`](docs/superpowers/specs/2026-09-30-maphard-design.md).

## Uso

Baixe o `maphard.exe` e abra. Não precisa instalar nem ser administrador. O programa só lê: não altera nada no computador e não envia nada para a internet.

A janela mostra, pela navegação à esquerda:

- **Resumo:** primeiro, um cartão de saúde por área (Discos, Memória, Processador, Estabilidade, Dispositivos, Windows 11 e Bateria), com Bom, Atenção, Ruim ou Desconhecido e o motivo numa frase; o clique no cartão abre a seção. Depois, nome do computador, fabricante, modelo, número de série, processador, memória instalada com o tipo, memória utilizável, discos, versão e ativação do Windows, e se a máquina aceita o Windows 11.
- **Processador:** nome, codinome, família, modelo e revisão, soquete, microcódigo, núcleos e threads (com os núcleos de desempenho e de eficiência no processador híbrido), clocks base, máximo e atual, uso, caches, instruções e virtualização.
- **Memória:** instalada, utilizável e reservada pelo hardware, tipo, slots ocupados e livres, capacidade máxima e ECC. Um cartão por slot, inclusive os vazios, com tamanho, tipo e velocidade ("DDR4-3200"), formato, fabricante, part number, número de série, ranks e voltagem. Avisa quando os módulos são diferentes, quando a memória roda abaixo da velocidade, quando o canal único é provável e quando o Windows usa bem menos que o instalado. Responde quanto cabe a mais e quantos slots estão livres, e mostra o uso agora.
- **Discos:** um cartão por disco com a saúde em destaque (Bom, Atenção, Ruim ou Desconhecido) e o motivo escrito ("3 setores realocados", "92% da vida útil usada"), temperatura, horas ligado, ciclos de energia, dados gravados, tipo (HDD, SSD SATA, SSD NVMe, USB), interface ("SATA 6 Gb/s", "NVMe, PCIe 4.0 x4"), capacidade, rotação, TRIM, partição e volumes com espaço livre e BitLocker. Logo abaixo, a tabela SMART com os nomes em português.

Como usuário comum, o MapHard já lê a saúde dos SSDs NVMe. O SMART dos discos SATA e o BitLocker pedem administrador: o botão **Ler como administrador**, na faixa do topo, reabre o programa elevado, com a confirmação do Windows. Disco atrás de adaptador USB ou de controladora RAID que não repassa o SMART aparece com "SMART indisponível por esta controladora", nunca com saúde Bom.
- **Placa-mãe e firmware:** equipamento, placa-mãe com o chipset, BIOS com a data e a idade, UEFI ou legado, Secure Boot e TPM.
- **Estabilidade:** telas azuis (com o código), desligamentos inesperados, erros de disco e do sistema de arquivos e erros de hardware corrigidos e não corrigidos dos últimos 30 dias (ou 90, na escolha da faixa do topo), com a saúde em destaque e os eventos mais recentes; índice de estabilidade do Windows, tempo ligado, último boot e data de instalação. O resultado do Diagnóstico de Memória do Windows aparece na seção Memória.
- **Vídeo e monitores:** um cartão por placa de vídeo, com fabricante, memória dedicada e compartilhada, versão e data do driver; um por monitor ligado, com fabricante, modelo, número de série, ano de fabricação, tamanho em polegadas e resolução nativa, lidos do próprio monitor.
- **Bateria:** desgaste em destaque (Atenção acima de 30%, Ruim acima de 50%), capacidade de projeto e carga total de hoje, ciclos de carga, química e fabricante. No desktop, "não disponível neste equipamento".
- **Windows:** edição, versão, compilação, arquitetura e ativação, sem ler a chave de produto; a verificação do Windows 11 item por item (processador na lista da Microsoft, TPM 2.0, UEFI, Secure Boot, 4 GB de memória e 64 GB no disco do Windows), com o que se resolve no firmware e o que pede troca de peça; e as placas de rede físicas, com MAC e velocidade.
- **Dispositivos:** os que estão com problema no Gerenciador de Dispositivos, com o código e o que ele quer dizer ("O driver do dispositivo não está instalado."), e os desativados à parte.

Campo que não pôde ser lido aparece em cinza, com o motivo: "não informado pelo fabricante", "requer administrador", "não disponível neste equipamento" ou "erro de leitura". Parando o mouse sobre um valor, a dica diz de onde ele veio (CPUID, SMBIOS, registro do Windows...).

**Copiar** põe a seção aberta na área de transferência, como texto, para colar em chamado ou e-mail.

**Salvar relatório** grava, pela extensão escolhida: o relatório para o cliente em HTML (um arquivo só, com a marca da MT e os cartões de saúde na primeira página, que vira PDF pela impressão do navegador), a coleta completa em JSON, com o estado e a fonte de cada campo, ou uma linha da máquina em CSV, para juntar várias coletas numa planilha. No CSV, campo não lido fica em branco.

Linha de comando (só lê). O resumo começa pelo bloco "Saúde", com o estado de cada área:

    maphard coletar
    maphard coletar --json
    maphard coletar --json estacao.json
    maphard coletar --html estacao.html
    maphard coletar --json estacao.json --csv estacao.csv
    maphard coletar --pasta \\servidor\inventario
    maphard coletar --dias 90

Sem nome depois de `--html`, `--json` ou `--csv`, o arquivo recebe o nome do computador e a data. Com `--pasta` e sem formato, grava o JSON e o CSV nessa pasta, com o nome do computador e a data, para um roteiro de logon gravar todas as estações na mesma pasta de rede sem uma sobrescrever a outra. No Prompt de Comando, use `start /wait maphard coletar` para o prompt esperar o fim. No PowerShell, termine a linha com `| Out-Host`.

`maphard --demonstracao` abre a janela com uma máquina fictícia, para imagem de tela.

## Situação do projeto

| Fatia | Conteúdo | Ramo | Pull Request | Situação |
|---|---|---|---|---|
| 0 | Regras, licença, desenho da versão 1 | Ramo da sessão na nuvem | Direto no `main`, com autorização | Concluída em 30/09/2026 |
| 1 | Base, processador, placa-mãe e firmware, JSON e linha de comando | Ramo da sessão na nuvem | Plano em [#1](https://github.com/manfredjr/maphard/pull/1), código em [#2](https://github.com/manfredjr/maphard/pull/2), correções do teste em máquina real em [#5](https://github.com/manfredjr/maphard/pull/5) | Concluída em 30/09/2026; corrigida em 01/10/2026 |
| 2 | Memória: módulos, slots, uso, alertas e ampliação | `memoria` | Plano em [#3](https://github.com/manfredjr/maphard/pull/3), código em [#6](https://github.com/manfredjr/maphard/pull/6) | Concluída em 01/10/2026 |
| 3 | Discos: lista, volumes, SMART ATA e NVMe, regras de saúde, "Ler como administrador" | `discos` | Plano em [#7](https://github.com/manfredjr/maphard/pull/7), código em [#8](https://github.com/manfredjr/maphard/pull/8) | Concluída em 01/10/2026 |
| 4 | Estabilidade, erros de memória, dispositivos com problema e chipset | `estabilidade` | Plano em [#9](https://github.com/manfredjr/maphard/pull/9), código em [#10](https://github.com/manfredjr/maphard/pull/10) | Concluída em 01/10/2026 |
| 5 | Vídeo, monitores, bateria, rede, Windows e verificação do Windows 11 | `plano-windows`, `windows` | Plano em [#11](https://github.com/manfredjr/maphard/pull/11), código em [#12](https://github.com/manfredjr/maphard/pull/12) | Concluída em 02/10/2026 |
| 6 | Resumo com os cartões de saúde | `plano-resumo`, `resumo` | Plano em [#13](https://github.com/manfredjr/maphard/pull/13), código em [#14](https://github.com/manfredjr/maphard/pull/14) | Concluída em 02/10/2026 |
| 7 | Relatório HTML, CSV, copiar seção, linha de comando completa e página | `plano-relatorio`, `relatorio` | Plano em [#15](https://github.com/manfredjr/maphard/pull/15), código em Pull Request | Código em teste; página pronta, não publicada |
