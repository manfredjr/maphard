using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.LinhaDeComando;
using MapHard.Nucleo.Relatorios;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class LinhaDeComandoTestes
{
    [Fact]
    public void Sem_argumentos_abre_a_janela()
    {
        var a = ArgumentosCli.Interpretar([]);

        Assert.Equal(ComandoCli.Janela, a.Comando);
        Assert.True(a.Valido);
    }

    [Fact]
    public void Coletar_com_json_e_arquivo()
    {
        var a = ArgumentosCli.Interpretar(["coletar", "--json", "estacao.json"]);

        Assert.Equal(ComandoCli.Coletar, a.Comando);
        Assert.True(a.GravarJson);
        Assert.Equal("estacao.json", a.ArquivoJson);
    }

    [Theory]
    [InlineData("coletar", "--json")]
    [InlineData("--json", "coletar")]
    public void Json_sem_arquivo_usa_o_nome_padrao(string primeiro, string segundo)
    {
        var a = ArgumentosCli.Interpretar([primeiro, segundo]);

        Assert.True(a.Valido);
        Assert.True(a.GravarJson);
        Assert.Null(a.ArquivoJson);
    }

    [Theory]
    [InlineData("--xyz")]
    [InlineData("apagar")]
    public void Argumento_desconhecido_e_erro(string arg)
    {
        Assert.False(ArgumentosCli.Interpretar(["coletar", arg]).Valido);
    }

    [Fact]
    public void Json_repetido_e_erro()
    {
        Assert.False(ArgumentosCli.Interpretar(["coletar", "--json", "a.json", "--json", "b.json"]).Valido);
    }

    [Fact]
    public void Json_sem_coletar_e_erro()
    {
        Assert.False(ArgumentosCli.Interpretar(["--json"]).Valido);
    }

    [Fact]
    public void Dois_comandos_e_erro()
    {
        Assert.False(ArgumentosCli.Interpretar(["coletar", "--versao"]).Valido);
    }

    [Fact]
    public void Demonstracao_so_na_janela()
    {
        Assert.True(ArgumentosCli.Interpretar(["--demonstracao"]).Demonstracao);
        Assert.False(ArgumentosCli.Interpretar(["coletar", "--demonstracao"]).Valido);
    }

    [Fact]
    public async Task Argumento_invalido_nao_coleta_nem_grava()
    {
        var coletou = false;
        var erro = new StringWriter();

        var codigo = await ExecutorCli.ExecutarAsync(
            ArgumentosCli.Interpretar(["coletar", "--xyz"]),
            _ =>
            {
                coletou = true;
                return Task.FromResult<ColetaMaquina>(null!);
            },
            AppContext.BaseDirectory,
            new StringWriter(),
            erro);

        Assert.Equal(ExecutorCli.CodigoArgumentos, codigo);
        Assert.False(coletou);
        Assert.Contains("Opção desconhecida: --xyz", erro.ToString());
        Assert.Contains("--ajuda", erro.ToString());
    }

    [Fact]
    public async Task Ajuda_e_versao()
    {
        var saida = new StringWriter();
        Assert.Equal(0, await ExecutorCli.ExecutarAsync(ArgumentosCli.Interpretar(["--ajuda"]), _ => throw new InvalidOperationException(), ".", saida, new StringWriter()));
        Assert.Contains("maphard coletar --json", saida.ToString());

        saida = new StringWriter();
        Assert.Equal(0, await ExecutorCli.ExecutarAsync(ArgumentosCli.Interpretar(["--versao"]), _ => throw new InvalidOperationException(), ".", saida, new StringWriter()));
        Assert.StartsWith("MapHard - MT ", saida.ToString());
    }

    [Fact]
    public async Task Coletar_mostra_o_resumo_com_os_estados()
    {
        var saida = new StringWriter();

        var codigo = await ExecutorCli.ExecutarAsync(ArgumentosCli.Interpretar(["coletar"]), Coletar, AppContext.BaseDirectory, saida, new StringWriter());

        var texto = saida.ToString();
        Assert.Equal(ExecutorCli.CodigoSucesso, codigo);
        Assert.Contains("Computador:  ESTACAO-TESTE", texto);
        Assert.Contains("Núcleos:     8 núcleos, 16 threads", texto);
        Assert.Contains("base 3,60 GHz, máximo 4,70 GHz", texto);
        Assert.Contains("Memória:     16 GB DDR4-3200, 2 de 4 slots (15,8 GB utilizáveis)", texto);
        Assert.DoesNotContain("Atenção:", texto);
        Assert.Contains("F.10 de 15/03/2021, UEFI", texto);
    }

    [Fact]
    public void Elevado_so_vale_para_a_janela_da_maquina_real()
    {
        var a = ArgumentosCli.Interpretar(["--elevado"]);

        Assert.True(a.Valido);
        Assert.True(a.Elevado);
        Assert.Equal(ComandoCli.Janela, a.Comando);
        Assert.False(ArgumentosCli.Interpretar(["coletar", "--elevado"]).Valido);
        Assert.False(ArgumentosCli.Interpretar(["--demonstracao", "--elevado"]).Valido);
        Assert.Contains("--elevado", ArgumentosCli.TextoAjuda, StringComparison.Ordinal);
    }

    [Fact]
    public void Resumo_mostra_os_alertas_de_memoria()
    {
        var linhas = ExecutorCli.Resumo(MapHard.Nucleo.Painel.DadosDemonstracao.Coleta()).ToList();

        Assert.Contains("Memória:     16 GB DDR4-2666 (o módulo aceita 3200), 2 de 4 slots (15,75 GB utilizáveis)", linhas);
        Assert.Contains(linhas, l => l.StartsWith("  Memória:      Atenção (rodando a 2666 MT/s", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Coletar_com_json_grava_o_arquivo_com_nome_padrao()
    {
        var pasta = Path.Combine(AppContext.BaseDirectory, "teste-cli-json");
        Directory.CreateDirectory(pasta);
        try
        {
            var saida = new StringWriter();

            var codigo = await ExecutorCli.ExecutarAsync(ArgumentosCli.Interpretar(["coletar", "--json"]), Coletar, pasta, saida, new StringWriter());

            var arquivo = Assert.Single(Directory.GetFiles(pasta));
            Assert.Equal(ExecutorCli.CodigoSucesso, codigo);
            Assert.Equal("maphard-ESTACAO-TESTE-2026-09-30-1005.json", Path.GetFileName(arquivo));
            Assert.Contains($"Coleta gravada em {arquivo}", saida.ToString());
            Assert.Equal("ESTACAO-TESTE", ExportadorJson.Desserializar(File.ReadAllText(arquivo))!.Identificacao.Computador.Valor);
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task Falha_ao_gravar_devolve_o_codigo_2()
    {
        var erro = new StringWriter();
        var destino = Path.Combine(AppContext.BaseDirectory, "pasta-que-nao-existe", "coleta.json");

        var codigo = await ExecutorCli.ExecutarAsync(ArgumentosCli.Interpretar(["coletar", "--json", destino]), Coletar, AppContext.BaseDirectory, new StringWriter(), erro);

        Assert.Equal(ExecutorCli.CodigoGravacao, codigo);
        Assert.Contains("Não foi possível gravar", erro.ToString());
    }

    private static Task<ColetaMaquina> Coletar(CancellationToken cancelar) =>
        new Coletor(FontesSimuladas.Completas(), TimeSpan.FromSeconds(5), agora: () => new DateTimeOffset(2026, 9, 30, 10, 5, 0, TimeSpan.FromHours(-3))).ColetarAsync(cancelar: cancelar);

    [Fact]
    public void Resumo_comeca_pelo_bloco_de_saude()
    {
        var linhas = ExecutorCli.Resumo(MapHard.Nucleo.Painel.DadosDemonstracao.Coleta()).ToList();

        Assert.Equal("Saúde", linhas[0]);
        Assert.Equal("  Discos:       Atenção (Disco 1: 3 setores realocados)", linhas[1]);
        Assert.Equal("  Processador:  Bom", linhas[3]);
        Assert.Equal("  Bateria:      Bom", linhas[7]);
        Assert.Equal(string.Empty, linhas[8]);
        Assert.DoesNotContain(linhas, l => l.StartsWith("Atenção:", StringComparison.Ordinal));
    }

    [Fact]
    public void Html_csv_e_pasta_sao_interpretados()
    {
        var a = ArgumentosCli.Interpretar(["coletar", "--html", "estacao.html", "--csv"]);

        Assert.True(a.Valido);
        Assert.Equal("estacao.html", a.Formatos[MapHard.Nucleo.Relatorios.FormatoRelatorio.Html]);
        Assert.Null(a.Formatos[MapHard.Nucleo.Relatorios.FormatoRelatorio.Csv]);
        Assert.Equal([MapHard.Nucleo.Relatorios.FormatoRelatorio.Html, MapHard.Nucleo.Relatorios.FormatoRelatorio.Csv], a.Gravacoes().Select(g => g.Formato));
    }

    [Fact]
    public void Pasta_sem_formato_grava_json_e_csv()
    {
        var a = ArgumentosCli.Interpretar(["coletar", "--pasta", @"\\servidor\inventario"]);

        Assert.True(a.Valido);
        Assert.Equal(@"\\servidor\inventario", a.Pasta);
        Assert.Equal([MapHard.Nucleo.Relatorios.FormatoRelatorio.Json, MapHard.Nucleo.Relatorios.FormatoRelatorio.Csv], a.Gravacoes().Select(g => g.Formato));
    }

    [Theory]
    [InlineData("coletar", "--pasta")]
    [InlineData("coletar", "--pasta", "x", "--json", "a.json")]
    [InlineData("--html")]
    [InlineData("coletar", "--csv", "--csv")]
    public void Combinacoes_invalidas_dao_erro(params string[] args)
    {
        Assert.False(ArgumentosCli.Interpretar(args).Valido);
    }

    [Fact]
    public void Ajuda_mostra_os_exemplos_do_desenho()
    {
        Assert.Contains("maphard coletar --html estacao.html", ArgumentosCli.TextoAjuda, StringComparison.Ordinal);
        Assert.Contains("maphard coletar --json estacao.json --csv estacao.csv", ArgumentosCli.TextoAjuda, StringComparison.Ordinal);
        Assert.Contains(@"maphard coletar --pasta \\servidor\inventario", ArgumentosCli.TextoAjuda, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Coletar_com_pasta_grava_json_e_csv_com_o_nome_padrao()
    {
        var pasta = Path.Combine(AppContext.BaseDirectory, $"inventario-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pasta);
        try
        {
            var saida = new StringWriter();
            var codigo = await ExecutorCli.ExecutarAsync(ArgumentosCli.Interpretar(["coletar", "--pasta", pasta]), Coletar, AppContext.BaseDirectory, saida, new StringWriter());

            Assert.Equal(ExecutorCli.CodigoSucesso, codigo);
            Assert.Equal(
                ["maphard-ESTACAO-TESTE-2026-09-30-1005.csv", "maphard-ESTACAO-TESTE-2026-09-30-1005.json"],
                Directory.GetFiles(pasta).Select(Path.GetFileName).Order());
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task Pasta_que_nao_existe_da_erro_sem_gravar_nada()
    {
        var pasta = Path.Combine(AppContext.BaseDirectory, $"nao-existe-{Guid.NewGuid():N}");
        var erro = new StringWriter();

        var codigo = await ExecutorCli.ExecutarAsync(ArgumentosCli.Interpretar(["coletar", "--pasta", pasta]), Coletar, AppContext.BaseDirectory, new StringWriter(), erro);

        Assert.Equal(ExecutorCli.CodigoGravacao, codigo);
        Assert.StartsWith($"Não foi possível gravar em {pasta}", erro.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(pasta));
    }

    [Fact]
    public async Task Html_gravado_pela_linha_de_comando()
    {
        var arquivo = Path.Combine(AppContext.BaseDirectory, $"estacao-{Guid.NewGuid():N}.html");
        try
        {
            var codigo = await ExecutorCli.ExecutarAsync(ArgumentosCli.Interpretar(["coletar", "--html", arquivo]), Coletar, AppContext.BaseDirectory, new StringWriter(), new StringWriter());

            Assert.Equal(ExecutorCli.CodigoSucesso, codigo);
            Assert.StartsWith("<!DOCTYPE html>", File.ReadAllText(arquivo), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }
}
