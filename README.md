# MapHard - MT

Programa para Windows que mostra o hardware do computador (processador, memória, placa-mãe, discos, vídeo, bateria) e aponta, logo na primeira tela, o que exige atenção: disco no fim da vida, erro de memória, desligamentos inesperados, dispositivo sem driver, compatibilidade com o Windows 11. Da MT - Manfred Tecnologia.

Software livre, sob licença [GPL-3.0](LICENSE).

As regras do projeto estão no [`AGENTS.md`](AGENTS.md) e o desenho em [`docs/superpowers/specs/2026-09-30-maphard-design.md`](docs/superpowers/specs/2026-09-30-maphard-design.md).

## Uso

Baixe o `maphard.exe` e abra. Não precisa instalar nem ser administrador. O programa só lê: não altera nada no computador e não envia nada para a internet.

A janela mostra, pela navegação à esquerda:

- **Resumo:** nome do computador, fabricante, modelo, número de série, processador, memória instalada com o tipo, memória utilizável, discos e versão do Windows.
- **Processador:** nome, codinome, família, modelo e revisão, soquete, microcódigo, núcleos e threads (com os núcleos de desempenho e de eficiência no processador híbrido), clocks base, máximo e atual, uso, caches, instruções e virtualização.
- **Memória:** instalada, utilizável e reservada pelo hardware, tipo, slots ocupados e livres, capacidade máxima e ECC. Um cartão por slot, inclusive os vazios, com tamanho, tipo e velocidade ("DDR4-3200"), formato, fabricante, part number, número de série, ranks e voltagem. Avisa quando os módulos são diferentes, quando a memória roda abaixo da velocidade, quando o canal único é provável e quando o Windows usa bem menos que o instalado. Responde quanto cabe a mais e quantos slots estão livres, e mostra o uso agora.
- **Discos:** um cartão por disco com a saúde em destaque (Bom, Atenção, Ruim ou Desconhecido) e o motivo escrito ("3 setores realocados", "92% da vida útil usada"), temperatura, horas ligado, ciclos de energia, dados gravados, tipo (HDD, SSD SATA, SSD NVMe, USB), interface ("SATA 6 Gb/s", "NVMe, PCIe 4.0 x4"), capacidade, rotação, TRIM, partição e volumes com espaço livre e BitLocker. Logo abaixo, a tabela SMART com os nomes em português.

Como usuário comum, o MapHard já lê a saúde dos SSDs NVMe. O SMART dos discos SATA e o BitLocker pedem administrador: o botão **Ler como administrador**, na faixa do topo, reabre o programa elevado, com a confirmação do Windows. Disco atrás de adaptador USB ou de controladora RAID que não repassa o SMART aparece com "SMART indisponível por esta controladora", nunca com saúde Bom.
- **Placa-mãe e firmware:** equipamento, placa-mãe com o chipset, BIOS com a data e a idade, UEFI ou legado, Secure Boot e TPM.
- **Estabilidade:** telas azuis (com o código), desligamentos inesperados, erros de disco e do sistema de arquivos e erros de hardware corrigidos e não corrigidos dos últimos 30 dias (ou 90, na escolha da faixa do topo), com a saúde em destaque e os eventos mais recentes; índice de estabilidade do Windows, tempo ligado, último boot e data de instalação. O resultado do Diagnóstico de Memória do Windows aparece na seção Memória.
- **Dispositivos:** os que estão com problema no Gerenciador de Dispositivos, com o código e o que ele quer dizer ("O driver do dispositivo não está instalado."), e os desativados à parte.

Campo que não pôde ser lido aparece em cinza, com o motivo: "não informado pelo fabricante", "requer administrador", "não disponível neste equipamento" ou "erro de leitura". Parando o mouse sobre um valor, a dica diz de onde ele veio (CPUID, SMBIOS, registro do Windows...).

**Salvar JSON** grava a coleta completa, com o estado e a fonte de cada campo.

Linha de comando (só lê):

    maphard coletar
    maphard coletar --json
    maphard coletar --json estacao.json
    maphard coletar --dias 90

Sem nome depois de `--json`, o arquivo recebe o nome do computador e a data. No Prompt de Comando, use `start /wait maphard coletar` para o prompt esperar o fim. No PowerShell, termine a linha com `| Out-Host`.

`maphard --demonstracao` abre a janela com uma máquina fictícia, para imagem de tela.

## Situação do projeto

| Fatia | Conteúdo | Ramo | Pull Request | Situação |
|---|---|---|---|---|
| 0 | Regras, licença, desenho da versão 1 | Ramo da sessão na nuvem | Direto no `main`, com autorização | Concluída em 30/09/2026 |
| 1 | Base, processador, placa-mãe e firmware, JSON e linha de comando | Ramo da sessão na nuvem | Plano em [#1](https://github.com/manfredjr/maphard/pull/1), código em [#2](https://github.com/manfredjr/maphard/pull/2), correções do teste em máquina real em [#5](https://github.com/manfredjr/maphard/pull/5) | Concluída em 30/09/2026; corrigida em 01/10/2026 |
| 2 | Memória: módulos, slots, uso, alertas e ampliação | `memoria` | Plano em [#3](https://github.com/manfredjr/maphard/pull/3), código em [#6](https://github.com/manfredjr/maphard/pull/6) | Concluída em 01/10/2026 |
| 3 | Discos: lista, volumes, SMART ATA e NVMe, regras de saúde, "Ler como administrador" | `discos` | Plano em [#7](https://github.com/manfredjr/maphard/pull/7), código em [#8](https://github.com/manfredjr/maphard/pull/8) | Concluída em 01/10/2026 |
| 4 | Estabilidade, erros de memória, dispositivos com problema e chipset | `estabilidade` | Plano em [#9](https://github.com/manfredjr/maphard/pull/9), código em [#10](https://github.com/manfredjr/maphard/pull/10) | Código em revisão |
