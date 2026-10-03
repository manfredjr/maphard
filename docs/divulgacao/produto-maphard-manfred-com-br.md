# Briefing: MapHard - MT na página de produtos do manfred.com.br

Para o chat ou a pessoa que cuida do site `manfred.com.br`. Leia inteiro antes de mexer no site.

## O que fazer

Acrescentar o MapHard - MT aos produtos do site, no mesmo formato do MapNet - MT e do MapDisk - MT, que já estão lá:

1. **Um cartão** na lista de `https://manfred.com.br/produtos`, ao lado do MapNet e do MapDisk.
2. **Uma página própria** em `https://manfred.com.br/produtos/maphard-mt`, com a mesma estrutura de `https://manfred.com.br/produtos/mapnet-mt`: título, frase de abertura, botão, "Para que serve", "O que ele faz", "O que ele não faz" e perguntas frequentes.

Siga o padrão visual e o código das páginas do MapNet e do MapDisk no próprio site. Não mude os outros produtos.

## Sobre o produto

| Item | Valor |
|---|---|
| Nome | MapHard - MT (sempre com o " - MT") |
| O que é | Programa para Windows 10 e 11, 64 bits, que mostra o hardware do computador e aponta o que pede atenção |
| Licença | Gratuito, de código aberto, GPL-3.0 |
| Página do programa | `https://maphard.manfred.com.br/` |
| Código e download | `https://github.com/manfredjr/maphard` |
| Logo | `https://maphard.manfred.com.br/imagens/maphard-logo.png` (900 x 177, fundo transparente) |
| Símbolo (para ícone ou miniatura) | `https://maphard.manfred.com.br/imagens/maphard-simbolo.png` (64 x 64) |
| Tela do programa, com dados de exemplo | `https://maphard.manfred.com.br/imagens/janela.png` (1280 x 800) |

Se preferir guardar as imagens no próprio site, baixe dessas URLs. As imagens de tela só mostram dados fictícios do modo de demonstração.

## Texto do cartão (em /produtos)

> **MapHard - MT**
>
> O hardware do computador e o que pede atenção, logo na primeira tela: disco com problema, memória abaixo da velocidade, desligamentos inesperados, dispositivo sem driver e se a máquina aceita o Windows 11. Para Windows, gratuito, de código aberto e sem instalar.

- O título leva para `/produtos/maphard-mt`.
- Botão: **Conhecer e baixar o MapHard - MT**, com link para `https://maphard.manfred.com.br/`.

## Texto da página (em /produtos/maphard-mt)

> **MapHard - MT**
>
> Mostra o hardware do computador e aponta, logo na primeira tela, o que pede atenção, para você saber se a máquina precisa de uma peça, de um ajuste ou de troca. Gratuito, de código aberto e sem instalar.

Botão: **Conhecer e baixar o MapHard - MT**, com link para `https://maphard.manfred.com.br/`.

### Para que serve

> No atendimento, a primeira pergunta sobre uma máquina é quase sempre a mesma: o que tem nela, e o que está dando problema? O MapHard responde numa janela só, começando por um cartão por área (discos, memória, processador, estabilidade, dispositivos, Windows 11 e bateria), cada um com Bom, Atenção, Ruim ou Desconhecido e o motivo numa frase. Desde o fim do suporte ao Windows 10, ele também responde a pergunta que o cliente mais faz: esta máquina aceita o Windows 11, ou é hora de trocar?

### O que ele faz

- Mostra processador, memória slot por slot, placa-mãe, BIOS, vídeo, bateria e placas de rede
- Lê a saúde dos discos pelo SMART, em SATA e NVMe, com o motivo escrito
- Conta telas azuis, desligamentos inesperados e erros de hardware dos últimos 30 ou 90 dias
- Aponta os dispositivos com problema e o que o código do erro quer dizer
- Verifica o Windows 11 item por item: processador, TPM, UEFI, Secure Boot, memória e disco
- Gera relatório em HTML para o cliente, JSON e CSV, e grava o inventário de várias estações numa pasta de rede

### O que ele não faz

> Não altera nada no computador, não instala nada e não manda dados para a MT. Não lê a chave de produto do Windows. A saúde do disco vem do que o próprio disco informa e não é previsão de falha. Uso autorizado apenas em computadores que você administra ou atende.

### Perguntas frequentes

1. **O MapHard - MT é pago?** Não. É gratuito e de código aberto, sob licença GPL-3.0.
2. **Precisa instalar ou ser administrador?** Não precisa instalar: é um arquivo único. Abre como usuário comum; o SMART dos discos SATA pede administrador, pelo botão "Ler como administrador".
3. **Manda dados para a MT?** Não. Não tem telemetria, e os relatórios ficam só onde você gravar.

## Cuidados

- **Não mude o sentido das frases.** Elas seguem a verificação jurídica da página do programa (`docs/legal/verificacao-pagina-maphard.md` no repositório `manfredjr/maphard`). Ajuste de estilo pode; promessa nova não. Fica fora, por exemplo: "prevê quando o disco vai falhar", "garante que a máquina aceita o Windows 11", "diagnóstico completo".
- **Não cite outros programas** de diagnóstico de hardware por nome, nem para comparar.
- **Microsoft e Windows** aparecem só para dizer onde o programa roda e de onde vem a lista de processadores do Windows 11, sem logotipo.
- **Textos do site** seguem as regras da MT: português do Brasil, sem travessão, sem aspas curvas, e passando pela `humanizar-ptbr` se algo for reescrito.
- **Publicar o site** segue o roteiro de publicação do próprio site, com a autorização do Manfred.

## Ao terminar

Conferir de fora as duas páginas e os links: o botão abre `https://maphard.manfred.com.br/`, e as imagens carregam.
