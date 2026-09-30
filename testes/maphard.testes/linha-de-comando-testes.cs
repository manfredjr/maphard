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
        Assert.Contains("Memória:     16 GB utilizáveis", texto);
        Assert.Contains("F.10 de 15/03/2021, UEFI", texto);
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
}
