# Plano da fatia 7: relatório HTML, CSV, copiar seção, linha de comando completa e página

> **Para quem executa:** siga tarefa por tarefa, na ordem. Cada passo tem caixa de seleção (`- [ ]`). Cada tarefa termina com teste verde e commit. Leia o `AGENTS.md` antes de começar.

**Objetivo:** o que o MapHard lê sai da tela e chega a quem precisa. O cliente recebe um relatório HTML com a marca da MT, que abre em qualquer navegador e vira PDF pela impressão. A MT junta numa planilha o CSV de várias máquinas. O técnico copia uma seção para o chamado ou o e-mail. Num cliente com muitas estações, um roteiro de logon grava a coleta de cada uma numa pasta de rede. Com isso, a versão 1 fica completa (R37, R38, R40 e R41).

**Arquitetura:** os relatórios saem do mesmo `MontadorSecoes` que monta a tela, para o relatório e a janela mostrarem sempre a mesma coisa, com os mesmos textos de estado. O HTML é um arquivo único: o estilo e o logo da MT vão dentro dele, sem nada buscado na internet. O CSV segue o MapDisk e o MapNet: ponto e vírgula, UTF-8 com BOM e campo não lido em branco, nunca com zero. A página do programa fica em `public/`, no modelo das páginas do MapNet e do MapDisk, e só é publicada com autorização.

**Tecnologia:** C# com .NET 8, WPF e xUnit. Nenhum pacote NuGet novo.

Desenho: `docs/superpowers/specs/2026-09-30-maphard-design.md`. Requisitos desta fatia: R37, R38, R40 e R41 completo, e a pasta `public/` (seção 4 do desenho).

## Restrições globais

- As mesmas das fatias anteriores. Nesta fatia pesam a regra do produto 2 (nunca 0 ou vazio onde não houve leitura) e a 6 (nada vai para a internet).
- **Relatório sem internet.** O HTML não busca fonte, estilo, imagem nem script fora do próprio arquivo. O teste confere que não há `http` em `src`, `href` de folha de estilo nem `@import`.
- **Texto da máquina sempre codificado.** Nome do computador, modelo e número de série são escritos por quem configurou a máquina e passam por `WebUtility.HtmlEncode` no HTML e pelas aspas do CSV.
- **Dado de cliente fora do git.** Imagem de tela da página sai só do `--demonstracao`. Relatório gerado em máquina real nunca entra em commit.
- **Texto jurídico pela `legal-br`.** A página de privacidade e o aviso de uso autorizado não saem de memória.
- **Publicar só com autorização.** A página fica pronta em `public/`, mas o envio para o servidor espera a frase do Manfred.

## Fontes do padrão

| O quê | Onde foi lido |
|---|---|
| CSV com ponto e vírgula, UTF-8 com BOM, aspas quando o campo tem `;`, `"` ou quebra de linha, campo não lido em branco | `manfredjr/mapdisk`, `src/mapdisk.nucleo/relatorios/exportador-csv.cs`, lido em 02/10/2026 |
| Relatório em arquivo único, estilo dentro do arquivo, texto da máquina codificado, formato escolhido pela extensão do arquivo | `manfredjr/mapnet`, `src/mapnet.nucleo/relatorios/relatorio-html.cs` e `relatorio-dados.cs`, lidos em 02/10/2026 |
| Página `public/` com `index.html`, `privacidade.html`, `css/site.css` e imagens | `manfredjr/mapdisk` e `manfredjr/mapnet`, pasta `public/` |

## Decisões que o plano pede ao Manfred

Respondidas em 02/10/2026: o Manfred aceitou as três recomendações. Vale um CSV por máquina, o `--pasta` sem formato gravando JSON e CSV, e o endereço `maphard.manfred.com.br` nos links da página.

1. **CSV de várias máquinas.** Recomendação: um arquivo por máquina, com o nome do computador e a data, como o JSON. A planilha junta os arquivos (no Excel, Dados, Obter Dados, De uma Pasta). Assim duas estações gravando na mesma pasta de rede ao mesmo tempo nunca brigam pelo mesmo arquivo. A outra opção é acrescentar uma linha num CSV comum, o que pede trava de arquivo e pode perder linha se duas máquinas gravarem juntas.
2. **`--pasta` sem formato.** Recomendação: `maphard coletar --pasta \\servidor\inventario` grava JSON e CSV, que é o que o inventário usa. Com `--html`, `--json` ou `--csv` junto, grava só os pedidos, na pasta.
3. **Endereço da página (pergunta 4 do desenho).** Recomendação: `maphard.manfred.com.br`, no mesmo padrão das outras ferramentas. O endereço só entra nos links da página; a publicação continua esperando autorização.

## Tarefas

### Tarefa 1: copiar seção (R37)

**Arquivos:**
- Criar: `src/maphard.nucleo/relatorios/texto-secao.cs`
- Modificar: `src/maphard/janela-principal.xaml`, `src/maphard/janela-principal.xaml.cs`
- Teste: `testes/maphard.testes/relatorios-testes.cs` (novo)

**Interfaces:**
- Consome: `SecaoTela`, `CartaoTela`, `LinhaTela` (`src/maphard.nucleo/painel/secoes.cs`).
- Produz: `public static string TextoSecao.Gerar(SecaoTela secao, ColetaMaquina coleta)`.

- [ ] **Passo 1:** testes.

```csharp
[Fact]
public void Texto_da_secao_tem_titulo_cartoes_e_linhas()
{
    var c = DadosDemonstracao.Coleta();
    var secao = MontadorSecoes.Montar(c, Hoje).Single(s => s.Id == MontadorSecoes.Bateria);

    var texto = TextoSecao.Gerar(secao, c);

    Assert.StartsWith("MapHard - MT: Bateria\r\nESTACAO-EXEMPLO, coletado em 30/09/2026 10:00\r\n", texto, StringComparison.Ordinal);
    Assert.Contains("Bateria [Bom]\r\n", texto, StringComparison.Ordinal);
    Assert.Contains("  Desgaste: 12%\r\n", texto, StringComparison.Ordinal);
}

[Fact]
public void Campo_nao_lido_sai_com_o_texto_do_estado()
{
    var c = DadosDemonstracao.Coleta();
    var secao = MontadorSecoes.Montar(c, Hoje).Single(s => s.Id == MontadorSecoes.Processador);

    Assert.Contains("  Litografia: não informado pelo fabricante", TextoSecao.Gerar(secao, c), StringComparison.Ordinal);
}
```

- [ ] **Passo 2:** implementar: a primeira linha é "MapHard - MT: {título}", a segunda o computador e a data da coleta, depois cada cartão com o selo entre colchetes quando houver e cada linha como "  Rótulo: texto". Quebra de linha `\r\n`, porque o texto vai para o Bloco de Notas e para o e-mail.
- [ ] **Passo 3:** botão **Copiar** na faixa do topo, ao lado de Atualizar. Ele copia a seção aberta com `Clipboard.SetText`, e a barra de status mostra "seção Bateria copiada". Fica desligado enquanto coleta.
- [ ] **Passo 4:** testes verdes, commit "Copia a secao aberta como texto".

### Tarefa 2: relatório HTML (R38)

**Arquivos:**
- Criar: `src/maphard.nucleo/relatorios/relatorio-html.cs`, `src/maphard.nucleo/recursos/mt-logo.png` (cópia do logo que o aplicativo já usa)
- Modificar: `src/maphard.nucleo/maphard.nucleo.csproj` (o logo como recurso embutido)
- Teste: `testes/maphard.testes/relatorios-testes.cs`

**Interfaces:**
- Consome: `MontadorSecoes.Montar(ColetaMaquina, DateOnly)`, `ResumoSaude`.
- Produz: `public static string RelatorioHtml.Gerar(ColetaMaquina coleta, DateOnly hoje)` e `public static string RelatorioHtml.NomePadrao(ColetaMaquina coleta)` ("maphard-COMPUTADOR-aaaa-mm-dd-hhmm.html", pelo `ExportadorJson.NomePadrao`).

- [ ] **Passo 1:** testes.
  - O arquivo começa por `<!DOCTYPE html>`, com `lang="pt-BR"` e `charset="utf-8"`.
  - A primeira página tem o cabeçalho com o logo da MT, o nome do computador, a data da coleta e os cartões de saúde do Resumo. Depois dela vem `page-break-before` (`break-before: page`) e as outras seções, na ordem da navegação.
  - Todas as seções da tela aparecem, com os mesmos títulos de cartão e os mesmos textos de linha, inclusive os estados ("requer administrador").
  - Arquivo único: nenhum `http://` ou `https://` em `src`, nenhum `<link rel="stylesheet"`, nenhum `@import`. O logo vai como `data:image/png;base64,`.
  - Nome do computador `<script>alert(1)</script>` sai como `&lt;script&gt;`.
  - Rodapé: "Gerado pelo MapHard - MT {versão}. O MapHard só lê: não altera nada no computador."
- [ ] **Passo 2:** implementar. Estilo dentro do `<style>`: as cores do tema da MT (`#006B2D`, `#0F8F2F`, `#43A92C`, `#9AD52B`, `#202020`), as cores de Atenção (`#B7791F`) e Ruim (`#B42318`) da janela, a fonte Montserrat quando instalada e, sem ela, a fonte do sistema. Regras de `@media print` para o PDF: margens, sem sombra, cartão sem quebra no meio (`break-inside: avoid`).
- [ ] **Passo 3:** testes verdes, commit "Cria o relatorio HTML com a marca da MT".

### Tarefa 3: CSV (R40)

**Arquivos:**
- Criar: `src/maphard.nucleo/relatorios/exportador-csv.cs`
- Teste: `testes/maphard.testes/relatorios-testes.cs`

**Interfaces:**
- Produz: `public const string ExportadorCsv.Cabecalho`, `public static string ExportadorCsv.Gerar(ColetaMaquina c)` (cabeçalho e uma linha), `public static void ExportadorCsv.Gravar(ColetaMaquina c, string caminho)` (UTF-8 com BOM).

Colunas, nesta ordem:

```
Computador;Coletado em;Fabricante;Modelo;Número de série;Processador;Núcleos;Threads;Memória instalada (GB);Tipo de memória;Discos;Windows;Versão;Compilação;Arquitetura;Ativação;Windows 11;Saúde dos discos;Saúde da memória;Saúde do processador;Estabilidade;Dispositivos;Bateria (desgaste %);Placa-mãe;BIOS;Data da BIOS;Administrador
```

- [ ] **Passo 1:** testes.
  - Cabeçalho igual ao acima e uma linha com a demonstração, com os números no formato do Excel em português ("15,75").
  - Campo não lido sai em branco, nunca 0 nem o texto do estado (a planilha soma a coluna).
  - Campo com `;` ou aspas sai entre aspas, com as aspas dobradas.
  - O arquivo gravado começa pelos bytes `EF BB BF`.
  - Saúde sai como na tela: "Bom", "Atenção", "Ruim" ou "Desconhecido". Desktop sem bateria: coluna da bateria em branco.
- [ ] **Passo 2:** implementar, no padrão do `exportador-csv.cs` do MapDisk.
- [ ] **Passo 3:** testes verdes, commit "Cria o CSV de uma linha por maquina".

### Tarefa 4: salvar relatório na janela

**Arquivos:**
- Criar: `src/maphard.nucleo/relatorios/relatorios.cs`
- Modificar: `src/maphard.nucleo/painel/painel-principal.cs`, `src/maphard/janela-principal.xaml`, `src/maphard/janela-principal.xaml.cs`
- Teste: `testes/maphard.testes/painel-testes.cs`

**Interfaces:**
- Produz: `public enum FormatoRelatorio { Html, Json, Csv }`, `Relatorios.FormatoDe(string caminho)` (pela extensão; o que não for `.json` nem `.csv` vira HTML), `Relatorios.Gravar(ColetaMaquina, string caminho, DateOnly hoje)`, `PainelPrincipal.SalvarRelatorio(string caminho)` e `PainelPrincipal.NomeSugerido(FormatoRelatorio)`.

- [ ] **Passo 1:** testes do painel: salvar com `.html`, `.json` e `.csv` grava o formato certo numa pasta temporária do teste; o nome sugerido segue o padrão do JSON com a extensão do formato.
- [ ] **Passo 2:** o botão "Salvar JSON..." vira **Salvar relatório...**, com o filtro "Relatório para o cliente (*.html)", "Coleta completa (*.json)" e "Planilha (*.csv)", o HTML primeiro. Pasta sem permissão de gravação: a mensagem que já existe, e a coleta continua na tela.
- [ ] **Passo 3:** testes verdes; abrir `--demonstracao`, salvar os três formatos e abrir o HTML no navegador; commit "Salva o relatorio em HTML, JSON ou CSV pela janela".

### Tarefa 5: linha de comando completa (R41)

**Arquivos:**
- Modificar: `src/maphard.nucleo/linha-de-comando/argumentos.cs`, `src/maphard.nucleo/linha-de-comando/executor-cli.cs`
- Teste: `testes/maphard.testes/linha-de-comando-testes.cs`

- [ ] **Passo 1:** testes.
  - `--html [arquivo]` e `--csv [arquivo]`, como o `--json` de hoje: sem nome, o nome padrão na pasta atual.
  - `--pasta <pasta>` sem formato grava JSON e CSV com o nome padrão nessa pasta (decisão 2). Com formato junto, grava só os pedidos, na pasta.
  - `--pasta` sem o nome da pasta, ou com um nome de arquivo junto (`--pasta x --json a.json`), é erro de argumento, com a mensagem.
  - Pasta que não existe ou sem permissão: "Não foi possível gravar em {pasta}: {motivo}", código 2, e nada pela metade.
  - A ajuda mostra os três exemplos do desenho.
- [ ] **Passo 2:** implementar.
- [ ] **Passo 3:** testes verdes; rodar `maphard coletar --pasta` numa pasta temporária e conferir os dois arquivos; commit "Completa a linha de comando com HTML, CSV e pasta".

### Tarefa 6: página do programa (`public/`)

**Arquivos:**
- Criar: `public/index.html`, `public/privacidade.html`, `public/css/site.css`, `public/imagens/janela.png`, `public/imagens/relatorio.png`, `public/imagens/mt-logo.png`, `public/.htaccess`
- Criar: `docs/legal/verificacao-pagina-maphard.md`

- [ ] **Passo 1:** rodar a `legal-br` sobre o que a página diz: o que o programa lê, que não envia nada, o dado pessoal no relatório (nome do computador, número de série), a MT agindo por instrução do cliente e a licença GPL-3.0. O resultado vai para `docs/legal/verificacao-pagina-maphard.md`, no formato das verificações do MapNet e do MapDisk. Ponto que pedir advogado vira `CONSULTA-ADVOGADO-*`, e a página espera a resposta.
- [ ] **Passo 2:** `index.html` no modelo das páginas do MapNet e do MapDisk: o que o programa mostra, a primeira tela com os cartões, o relatório, a linha de comando, o download pelo GitHub Releases, a licença. Textos pela `humanizar-ptbr`. Sem os nomes dos programas de referência.
- [ ] **Passo 3:** imagens de tela só do `--demonstracao`: a janela no Resumo e a primeira página do relatório.
- [ ] **Passo 4:** teste `PaginaTestes`: as páginas não citam os nomes proibidos do `AGENTS.md`, não têm caracteres proibidos e não apontam para fora do domínio, a não ser o GitHub do projeto e o site da MT.
- [ ] **Passo 5:** commit "Cria a pagina do MapHard". Publicação: só com autorização.

### Tarefa 7: fechamento

- [ ] **Passo 1:** README: os relatórios e a cópia no "Uso", a linha de comando completa, e a linha da fatia 7 na "Situação do projeto".
- [ ] **Passo 2:** pendências: as decisões 1 a 3 com a resposta do Manfred e o que ficar aberto.
- [ ] **Passo 3:** portões, envio, Pull Request do código e leitura do CI até ficar verde.

## Como o Manfred testa

1. Baixar o `maphard.exe` do PR, ou gerar com `ferramentas\publicar.cmd`.
2. Abrir uma seção, clicar em **Copiar** e colar no Bloco de Notas.
3. **Salvar relatório...** como HTML, abrir no navegador, imprimir como PDF e conferir o Resumo na primeira página.
4. Salvar como CSV e abrir no Excel: acentos certos, uma coluna por campo.
5. `maphard coletar --pasta C:\temp\inventario` e conferir o JSON e o CSV com o nome do computador e a data.
6. Abrir `public/index.html` no navegador e ler a página e a política de privacidade.

## Riscos

| Risco | O que fazer |
|---|---|
| Relatório e tela mostrarem coisas diferentes | O relatório sai do mesmo `MontadorSecoes`; o teste compara os títulos e os textos |
| Texto da máquina quebrar o HTML ou o CSV | Codificação no HTML e aspas no CSV, com teste de cada caso |
| CSV com zero onde não houve leitura | Campo não lido em branco, com teste |
| Página com afirmação jurídica sem base | `legal-br` antes, e consulta ao advogado no que ela apontar |
