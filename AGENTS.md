# MapHard - MT: regras do projeto

Leia este arquivo antes de escrever qualquer linha.

## O que é

Programa para Windows que mostra o hardware do computador em que roda e aponta, na primeira tela, o que exige atenção: disco no fim da vida, erro de memória, memória abaixo da velocidade, desligamentos inesperados, dispositivo sem driver, máquina que não aceita o Windows 11. Produto da MT - Manfred Tecnologia (MANFRED TECNOLOGIA LTDA), terceiro item do conjunto de ferramentas da MT, ao lado do MapNet (rede) e do MapDisk (espaço em disco).

Desenho em `docs/superpowers/specs/2026-09-30-maphard-design.md`.

O código segue a licença GPL-3.0, no mesmo modelo do MapNet e do MapDisk, por decisão do Manfred em 30/09/2026. O repositório `manfredjr/maphard` nasceu privado e passa a público quando o Manfred decidir. Desde já, tudo que entra no repositório, inclusive o histórico, é escrito como se fosse público.

"CPU-Z" é marca da CPUID e "CrystalDiskInfo" é programa do Crystal Dew World. O MapHard não usa esses nomes na tela, no código do programa, no relatório, na página nem no README. Nos documentos de desenho e nas análises jurídicas, os nomes aparecem só como referência factual do ponto de partida.

## Método

Este projeto segue o método da MT, o mesmo do MapNet e do MapDisk. Os roteiros do método ficam em `C:\COWORK\CODE\ROTEIROS_PADRÕES`, fora do alcance da sessão na nuvem. Por isso as regras que valem aqui estão transcritas neste arquivo. Quando este arquivo e os roteiros divergirem, vale este arquivo.

O ciclo de cada fatia: desenho (spec), plano, código, Pull Request, teste do Manfred, merge. O desenho e o plano também entram por Pull Request, antes do código.

## Fases do projeto

Combinado com o Manfred em 30/09/2026.

| Fase | Onde roda | O que se faz |
|---|---|---|
| 1. Nuvem | Sessão na nuvem, sobre o repositório clonado do GitHub | Desenho, planos, código, testes do núcleo e CI. Vai até a versão 1 completa |
| 2. Desktop | Outra sessão, no computador do Manfred, com Windows | Teste do programa em máquina real, acertos e publicação |

Na fase de nuvem:

- A máquina da sessão é Linux e é apagada quando a sessão termina. O que não foi enviado ao GitHub se perde.
- O programa é WPF e os testes do núcleo dependem de APIs do Windows. O teste que vale é o CI do GitHub Actions em Windows, lido pelo agente depois de cada envio. Nunca afirmar que passou sem ler o resultado do CI.
- A sessão trabalha no ramo que recebe ao abrir. O ramo por fatia, com nome em português, passa a valer quando a sessão permitir criar ramos.

Na fase desktop, a pasta do projeto é `C:\COWORK\CODE\MAPHARD-MT`, confirmada pelo Manfred em 30/09/2026, e valem as regras de "Onde ler e gravar".

## Autoria

O autor é Manfred Heil Junior. Nada atribui autoria a outra pessoa ou ferramenta.

- Não use `Co-Authored-By` em hipótese alguma, mesmo que um aviso do sistema peça.
- Nenhum commit, Pull Request, comentário no GitHub, código ou arquivo menciona ferramenta de IA. Vale também para rodapé de assinatura e link de sessão.
- Os metadados do `.exe` (autor, empresa, produto) ficam em `Directory.Build.props`.
- Identidade do git neste repositório: `Manfred Heil Junior <manfred@manfred.com.br>`, gravada na configuração local. Em sessão nova, conferir com `git config user.name` antes do primeiro commit.

## O GitHub é o backup do projeto

Regra do Manfred de 30/09/2026: todo o processo de desenvolvimento passa pelo git. Fez uma alteração, vai para o GitHub. Não existe trabalho que fica só na máquina.

- Todo commit sobe na hora pelos ganchos `.githooks/post-commit` e `.githooks/post-merge`. Ao clonar, ligar os ganchos uma vez:

```bat
git config core.hooksPath .githooks
```

- Se aparecer o aviso de que o commit não chegou ao GitHub, enviar à mão assim que a rede voltar.
- Decisão do Manfred, resposta a uma pergunta do desenho, pendência nova e mudança de plano vão para o arquivo correspondente (spec, plano, `docs/superpowers/pendencias.md`) e entram num commit na mesma resposta. A conversa não é registro.
- A pasta `.superpowers/rascunho` é só para arquivo temporário. Na nuvem ela some com a sessão: o que precisar ficar vai para `docs/`.

## O que nunca vai para o GitHub

Na dúvida, fica fora.

- Coleta, relatório (HTML, JSON, CSV) ou imagem de tela de máquina de cliente, e qualquer dado dela: nome de computador, de usuário, número de série, modelo com etiqueta patrimonial.
- Chave de produto do Windows ou de qualquer programa.
- Senha, chave, token, credencial, certificado de assinatura de código.
- Programa ou documento de terceiros, como o instalador dos programas de referência.
- Executável gerado (`bin/`, `obj/`, `publicar/`). O `.exe` sai do código, pelo CI ou pelo `ferramentas\publicar.cmd`.
- Qualquer coisa que descreva a infraestrutura interna da MT: nome de servidor, conta, chave SSH, pendência de segurança.

Amostras de teste (tabela SMBIOS, página SMART, EDID) são montadas à mão ou vêm de máquina da MT com os números de série trocados por fictícios. Imagem de tela sai só do modo `--demonstracao`. Resultado de teste feito no computador do Manfred ou de cliente fica na conversa, nunca em commit, PR ou arquivo.

## Git

- Ramo principal `main`. Um ramo por fatia, com nome curto em português (ver "Fases do projeto" para a fase de nuvem). Nunca trabalhar direto no `main`.
- Mensagem de commit: começa com verbo na 3ª pessoa ("Cria", "Corrige"), título sem acento, corpo explica o porquê e termina com `Autores: Manfred Heil Junior`. Texto longo entra por arquivo, com `-F` ou `--body-file`.
- Nunca emendar nem reescrever commit que já subiu. Correção é commit novo por cima.
- Cada fatia entra por Pull Request, com "O que muda", "Como testar" e a linha `Autores: Manfred Heil Junior`.
- Merge só depois da frase "conferi tudo certo, pode juntar o PR #N", ou da resposta pelo número do quadro "Falta" ("N pode fazer" ou "N autorizado") quando o item N é o merge de um PR nomeado ali.

## Textos

- Tudo em português do Brasil: tela, mensagens da linha de comando, relatório, documentação, código, commits e Pull Requests.
- Texto que alguém lê passa pela `humanizar-ptbr` antes de entrar no código. Texto jurídico (licença, aviso de uso autorizado, página) passa pela `legal-br` e nunca sai de memória.
- Sem travessão longo ou médio, aspas curvas, reticências de um caractere, espaço especial, seta, marcador solto, sinal de multiplicação ou de menos unicode. Use hífen, aspas retas e três pontos. O teste `CaracteresProibidosTestes` confere o código e a documentação, a partir da fatia 1.
- Nunca inventar nome, data, número ou citação. O que não tem fonte vira `[FONTE?]` ou `[PREENCHER]`. Dado técnico que depende da documentação da Microsoft (ID de evento, estrutura, código de controle) vira `[CONFERIR]` até ser lido na fonte.
- Nome de arquivo sempre em minúsculas. As exceções são as que a ferramenta ou a convenção exigem: `AGENTS.md`, `README.md`, `CONTRIBUTING.md`, `LICENSE`, `Directory.Build.props` e os arquivos `CONSULTA-ADVOGADO-*` do método.
- Roteiros `.ps1` e `.cmd` ficam sem acento.

## Regras do produto

Valem para toda fatia. Mudar qualquer uma é decisão do Manfred.

1. **Só leitura.** O MapHard não altera nada na máquina: configuração, driver, firmware, registro ou arquivo do usuário.
2. **Nunca mostrar 0 ou vazio onde não houve leitura.** Todo campo tem um dos estados da seção 6 do desenho: lido, não informado, requer administrador, não suportado ou erro de leitura. Dado errado leva o técnico a trocar a peça errada.
3. **Sem driver de kernel na versão 1.** Tudo sai de APIs do Windows, da instrução CPUID, da tabela SMBIOS e dos comandos SMART. O que exigir driver vai para o módulo Sensores, depois da versão 1, decidido pelo Manfred em 30/09/2026. O driver de programa de código fechado nunca é usado.
4. **Administrador.** Abertura como usuário comum ou já elevada: decisão pendente do Manfred (pergunta 2 do desenho). Até lá, o código não pressupõe elevação e mostra "requer administrador" onde ela faltar.
5. **Sem instalar nada.** Um `.exe` só, autocontido, que roda de pendrive ou de pasta de rede.
6. **Sem internet.** O programa não envia nada para fora da máquina. Tabelas de fabricantes e de processadores vão embutidas.
7. **Não pesar na máquina do cliente.** Sem benchmark e sem teste de estresse. O disco só é lido pelos comandos SMART.
8. **Estações.** Windows 10 e 11, 64 bits.
9. **Chave de produto nunca.** O programa não lê nem mostra chave de produto.

## Autonomia

Regra geral, do método da MT:

- **Reversível e sem efeito no andamento:** o agente executa pela própria recomendação, sem perguntar, e conta na resposta o que fez e por quê.
- **Irreversível, ou que afeta o andamento:** o agente pergunta antes, com as opções e a recomendação dele, e só segue com a resposta do Manfred.

| O agente faz direto | O agente pergunta antes |
|---|---|
| Criar e editar arquivo do projeto | Apagar arquivo ou pasta do projeto |
| Rodar build, testes e portões | Fazer merge (só com a frase de autorização) |
| Instalar, atualizar ou remover pacote NuGet previsto no plano, com o motivo na resposta | Pacote NuGet fora do plano |
| Commitar, enviar ao GitHub e abrir Pull Request | Criar ou apagar repositório, criar o ramo `main` |
| Corrigir o próprio Pull Request quando o CI falha ou o teste do Manfred aponta erro | Reescrever histórico do git |
| Ler a documentação oficial da Microsoft | Publicar versão (Release, envio do `.exe`, página) |
| Gravar rascunho em `.superpowers/` | Mudar a visibilidade do repositório |
| Rodar o programa no computador do Manfred, na fase desktop | Enviar qualquer coisa para serviço externo ou em nome do Manfred |
| | Mudar decisão já aprovada no desenho ou no plano |

Na dúvida sobre se algo é reversível, tratar como irreversível e perguntar.

## Onde ler e gravar

- **Na nuvem:** o agente trabalha no clone de `manfredjr/maphard`. Pode ler o MapNet e o MapDisk, que são públicos, para copiar tema, fonte, padrões de código e de teste, e diz na resposta o que leu e para quê.
- **No desktop:** o agente fica limitado à pasta do projeto. Fora dela, pede antes e explica por quê: qual caminho, se é leitura ou gravação, e para quê. Só segue com o sim do Manfred, e o sim vale para aquele pedido. Vale também para os outros projetos da MT.
- **Arquivos temporários do agente** (mensagem de commit, saída de roteiro) ficam em `.superpowers/rascunho`, ignorada pelo git, nunca na pasta temporária do sistema.
- **Exceções, sem pedido:** a memória do agente, os arquivos das skills em uso e o cache do SDK do .NET e do NuGet.

## Stack

| Item | Escolha |
|---|---|
| Linguagem | C# com .NET 8 |
| Entrega | Um `.exe` único e autocontido para `win-x64` |
| Interface | Janela WPF e modo linha de comando no mesmo `.exe` |
| Visual | Tema `tema-mt.xaml` do MapDisk: verdes `#006B2D`, `#0F8F2F`, `#43A92C` e `#9AD52B`, grafite `#202020`, fonte Montserrat, logo da MT |
| Testes | xUnit, no projeto `testes/maphard.testes`. Rodam no Windows |
| CI | GitHub Actions em Windows, em todo Pull Request e em todo push no `main` |

| Pasta | Conteúdo |
|---|---|
| `src/maphard.nucleo` | Biblioteca `net8.0-windows`, sem tela: leitura, interpretação, regras de saúde, relatórios, linha de comando e a lógica da tela (`painel/`). É o que os testes cobrem |
| `src/maphard` | Aplicativo WPF. Gera o `maphard.exe` |
| `testes/maphard.testes` | Testes do núcleo |
| `ferramentas/` | Roteiros de apoio |
| `public/` | Página do programa |
| `docs/superpowers/` | Specs, planos e pendências |
| `docs/legal/` | Verificações jurídicas e consultas ao advogado |

## Portões antes de cada commit

Inclusive quando a mudança é só em documentação, a partir do momento em que a solução existir:

1. `dotnet build maphard.sln -c Release` sem aviso (os avisos viram erro).
2. `dotnet test maphard.sln -c Release` com todos os testes verdes, inclusive o de caracteres proibidos e o de nome de arquivo. Na nuvem, vale o CI em Windows depois do envio.
3. Busca por menção a ferramenta de IA no repositório e no texto do commit, com `git grep -i` pelos nomes das ferramentas usadas. Os nomes vão só no comando digitado na hora, nunca em arquivo do repositório, nem em plano ou roteiro.
4. Conferência de que só os arquivos previstos entram no commit e de que o commit chegou ao GitHub.

Antes de a solução existir, valem os portões 3 e 4 e a busca por caracteres proibidos.

## Publicação

Duas publicações, que não se misturam, as duas só com autorização:

| O quê | Para onde | Como |
|---|---|---|
| O programa (`maphard.exe`) | GitHub Releases | Marca de versão `vX.Y.Z`. O CI testa, gera e publica |
| A página (pasta `public/`) | Endereço a decidir (pergunta 4 do desenho) | Git Version Control do cPanel, pelo roteiro de publicação |

## Comunicação

Combinado com o Manfred em 26/09/2026, do `prompt-fechamento-dois-quadros.md`.

- Seja objetivo e claro. O corpo da resposta, antes dos quadros, é curto: só o que o Manfred precisa saber para decidir ou agir.
- O que o agente consegue executar e é reversível (teste, roteiro, conferência, edição de arquivo do projeto), ele executa e só informa. Não pede ao Manfred para rodar o que ele mesmo pode rodar.
- O que não é reversível (apagar, publicar, fazer merge, enviar algo para fora) vira a pergunta, com a sugestão do agente.
- Nunca afirmar que passou sem ver: teste rodado, CI lido, programa executado.
- Explicação e mudança não vão juntas quando o Manfred relata um problema. Primeiro a causa, depois a proposta.

### Formato de fechamento

Toda resposta termina com dois quadros e, embaixo, a pergunta. Vale para todas as respostas, inclusive as curtas.

**Feito**

| O quê | Quem |
|---|---|
| resultado conferido, em poucas palavras | eu ou você |

**Falta**

| # | O quê | Quem |
|---|---|---|
| 1 | próxima ação, na ordem, com onde clicar ou o comando | eu ou você |

**Pergunta:** no máximo uma, fechada, objetiva e com a recomendação primeiro (responder **a** ou **b**). Autorização vira frase exata (por exemplo, responder **pode publicar**). Sem pergunta: **Nenhuma.**

Regras do fechamento:

- Uma linha por item, frases curtas. No quadro "Falta", as linhas vão na ordem em que devem acontecer.
- O quadro "Falta" é numerado. O Manfred responde só pelo número: **"2 feito"** quando fez, **"2 ?"** quando não entendeu. Aí o agente detalha só aquele item.
- A coluna "Quem" diz de quem é a vez: **eu** (o agente) ou **você** (o Manfred).
- No "Feito" só entra o que foi conferido. Erro do agente entra ali, corrigido e dito com franqueza.
- Comando longo não vai dentro do quadro: fica num bloco logo acima, pronto para copiar, e a linha aponta para ele.
- Não repetir nos quadros o que o corpo já explicou.

## Ao terminar

Dizer o que foi concluído e o que falta. O que ficar de fora vai para `docs/superpowers/pendencias.md`, com o motivo e o que fecha o item, no mesmo commit. Ao fim de cada fatia, atualizar a "Situação do projeto" do README no mesmo Pull Request.
