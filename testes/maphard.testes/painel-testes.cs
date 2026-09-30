using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Painel;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class PainelTestes
{
    private static readonly DateOnly Hoje = new(2026, 9, 30);

    private static Task<ColetaMaquina> Coletar(CancellationToken cancelar) =>
        new Coletor(FontesSimuladas.Completas(), TimeSpan.FromSeconds(5), agora: () => new DateTimeOffset(2026, 9, 30, 10, 5, 0, TimeSpan.FromHours(-3))).ColetarAsync(cancelar: cancelar);

    [Theory]
    [InlineData(EstadoCampo.NaoInformado, "não informado pelo fabricante")]
    [InlineData(EstadoCampo.RequerAdministrador, "requer administrador")]
    [InlineData(EstadoCampo.NaoSuportado, "não disponível neste equipamento")]
    public void Linha_sem_leitura_mostra_o_texto_do_estado(EstadoCampo estado, string texto)
    {
        var campo = estado switch
        {
            EstadoCampo.NaoInformado => Campo<string>.NaoInformado(FonteDado.Smbios),
            EstadoCampo.RequerAdministrador => Campo<string>.RequerAdministrador(FonteDado.Smbios),
            _ => Campo<string>.NaoSuportado(FonteDado.Smbios),
        };

        var linha = MontadorSecoes.Linha("Rótulo", campo);

        Assert.Equal(texto, linha.Texto);
        Assert.Equal(estado, linha.Estado);
        Assert.Equal("Fonte: SMBIOS", linha.Dica);
    }

    [Fact]
    public void Erro_mostra_o_motivo()
    {
        var linha = MontadorSecoes.Linha("Clock atual", Campo<int>.Erro(FonteDado.Contador, "tempo esgotado"));

        Assert.Equal("erro de leitura: tempo esgotado", linha.Texto);
        Assert.Equal("Fonte: contador de desempenho do Windows", linha.Dica);
    }

    [Fact]
    public void Dica_de_campo_lido_diz_a_fonte_e_a_observacao()
    {
        var linha = MontadorSecoes.Linha("Codinome", Campo<string>.Lido("Coffee Lake", FonteDado.Tabela, "fonte X"));

        Assert.Equal("Coffee Lake", linha.Texto);
        Assert.Equal("Fonte: tabela do MapHard. Fonte X", linha.Dica);
    }

    [Fact]
    public async Task Painel_comeca_coletando_e_termina_com_as_tres_secoes()
    {
        var painel = new PainelPrincipal(Coletar, hoje: () => Hoje);
        Assert.Equal(EstadoPainel.Coletando, painel.Estado);
        Assert.Equal("coletando...", painel.TextoStatus);
        Assert.False(painel.PodeSalvar);

        await painel.AtualizarAsync();

        Assert.Equal(EstadoPainel.Pronto, painel.Estado);
        Assert.Equal(["Resumo", "Processador", "Placa-mãe e firmware"], painel.Secoes.Select(s => s.Titulo));
        Assert.Equal("ESTACAO-TESTE   |   usuário comum   |   coletado em 30/09/2026 10:05", painel.TextoStatus);
        Assert.True(painel.PodeSalvar);
    }

    [Fact]
    public async Task Textos_das_secoes()
    {
        var painel = new PainelPrincipal(Coletar, hoje: () => Hoje);
        await painel.AtualizarAsync();

        var linhas = painel.Secoes.SelectMany(s => s.Cartoes).SelectMany(c => c.Linhas).ToList();
        string Texto(string rotulo) => linhas.First(l => l.Rotulo == rotulo).Texto;

        Assert.Equal("8 núcleos, 16 threads", Texto("Núcleos e threads"));
        Assert.Equal("16 GB", Texto("Memória utilizável"));
        Assert.Equal("3,60 GHz", Texto("Clock base"));
        Assert.Equal("4,50 GHz", Texto("Clock atual"));
        Assert.Equal("8 x 48 KB", Texto("L1 de dados"));
        Assert.Equal("24 MB", Texto("L3"));
        Assert.Equal("15/03/2021 (5 anos)", Texto("Data da BIOS"));
        Assert.Equal("ligado", Texto("Secure Boot"));
        Assert.Equal("x86-64-v1", Texto("Nível x86-64"));
        Assert.All(linhas, l => Assert.False(string.IsNullOrWhiteSpace(l.Texto), l.Rotulo));
    }

    [Fact]
    public async Task Falha_da_coleta_vai_para_o_status()
    {
        var painel = new PainelPrincipal(_ => throw new InvalidOperationException("sem acesso"));

        await painel.AtualizarAsync();

        Assert.Equal(EstadoPainel.Falhou, painel.Estado);
        Assert.Equal("a coleta falhou: sem acesso", painel.TextoStatus);
        Assert.False(painel.PodeSalvar);
    }

    [Fact]
    public async Task Demonstracao_so_tem_dados_ficticios()
    {
        var painel = PainelPrincipal.ComDemonstracao();
        await painel.AtualizarAsync();

        Assert.True(painel.Demonstracao);
        Assert.EndsWith("demonstração: dados fictícios", painel.TextoStatus);
        Assert.All(ColetaTestes.Campos(painel.Coleta!), _ => { });
        Assert.Equal(FonteDado.Demonstracao, painel.Coleta!.Processador.Nome.Fonte);
        Assert.StartsWith("SERIE-TESTE", painel.Coleta.Identificacao.NumeroSerie.Valor);
    }

    [Fact]
    public async Task Nome_sugerido_e_salvar_json()
    {
        var painel = new PainelPrincipal(Coletar, hoje: () => Hoje);
        Assert.Throws<InvalidOperationException>(() => painel.SalvarJson("x.json"));

        await painel.AtualizarAsync();
        var caminho = Path.Combine(AppContext.BaseDirectory, painel.NomeSugeridoJson());
        try
        {
            painel.SalvarJson(caminho);
            Assert.True(File.Exists(caminho));
            Assert.Equal("maphard-ESTACAO-TESTE-2026-09-30-1005.json", Path.GetFileName(caminho));
        }
        finally
        {
            File.Delete(caminho);
        }
    }
}
