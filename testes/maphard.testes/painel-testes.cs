using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Painel;
using MapHard.Nucleo.Processador;
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
    public void Caches_do_mesmo_nivel_ficam_numa_linha_so()
    {
        IReadOnlyList<CacheCpu> caches =
        [
            new(1, TipoCache.Dados, 48 * 1024, 4, 12, 64),
            new(1, TipoCache.Dados, 32 * 1024, 8, 8, 64),
            new(3, TipoCache.Unificado, 18 * 1024 * 1024, 1, 12, 64),
        ];

        var linhas = MontadorSecoes.LinhasCaches(Campo<IReadOnlyList<CacheCpu>>.Lido(caches, FonteDado.Topologia));

        Assert.Equal(["L1 de dados", "L3"], linhas.Select(l => l.Rotulo));
        Assert.Equal($"4 x {Formatador.Bytes(48 * 1024)} + 8 x {Formatador.Bytes(32 * 1024)}", linhas[0].Texto);
        Assert.Contains("12 vias, linha de 64 bytes; 8 vias, linha de 64 bytes", linhas[0].Dica);
    }

    [Fact]
    public async Task Painel_comeca_coletando_e_termina_com_as_sete_secoes()
    {
        var painel = new PainelPrincipal(Coletar, hoje: () => Hoje);
        Assert.Equal(EstadoPainel.Coletando, painel.Estado);
        Assert.Equal("coletando...", painel.TextoStatus);
        Assert.False(painel.PodeSalvar);

        await painel.AtualizarAsync();

        Assert.Equal(EstadoPainel.Pronto, painel.Estado);
        Assert.Equal(["Resumo", "Processador", "Memória", "Discos", "Placa-mãe e firmware", "Estabilidade", "Dispositivos"], painel.Secoes.Select(s => s.Titulo));
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
        Assert.Equal("16 GB DDR4", Texto("Memória"));
        Assert.Equal("15,8 GB", Texto("Memória utilizável"));
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
    public async Task Secao_memoria_tem_resumo_um_cartao_por_slot_ampliacao_e_uso()
    {
        var painel = new PainelPrincipal(Coletar, hoje: () => Hoje);
        await painel.AtualizarAsync();

        var memoria = painel.Secoes.Single(s => s.Id == MontadorSecoes.Memoria);
        Assert.Equal(["Resumo", "ChannelA-DIMM0", "ChannelA-DIMM1", "ChannelB-DIMM0", "ChannelB-DIMM1", "Erros de memória", "Ampliação", "Uso agora"], memoria.Cartoes.Select(c => c.Titulo));

        string Texto(string cartao, string rotulo) => memoria.Cartoes.Single(c => c.Titulo == cartao).Linhas.Single(l => l.Rotulo == rotulo).Texto;
        Assert.Equal("16 GB", Texto("Resumo", "Instalada"));
        Assert.Equal("200 MB", Texto("Resumo", "Reservada pelo hardware"));
        Assert.Equal("2 de 4 ocupados", Texto("Resumo", "Slots"));
        Assert.Equal("64 GB", Texto("Resumo", "Capacidade máxima"));
        Assert.Equal("DDR4-3200", Texto("ChannelA-DIMM0", "Tipo e velocidade"));
        Assert.Equal("Samsung", Texto("ChannelA-DIMM0", "Fabricante"));
        Assert.Equal("1,2 V", Texto("ChannelA-DIMM0", "Voltagem"));
        Assert.Equal("vazio", Texto("ChannelA-DIMM1", "Situação"));
        Assert.Equal("cabem até 64 GB (informado pelo firmware); 2 slots livres; tipo DDR4, formato DIMM", Texto("Ampliação", "Resposta"));
        Assert.Equal("48%", Texto("Uso agora", "Carga"));
    }

    [Fact]
    public void Demonstracao_mostra_o_cartao_atencao_da_memoria()
    {
        var secoes = MontadorSecoes.Montar(DadosDemonstracao.Coleta(), Hoje);

        var atencao = secoes.Single(s => s.Id == MontadorSecoes.Memoria).Cartoes.Single(c => c.Titulo == "Atenção");
        var linha = Assert.Single(atencao.Linhas);
        Assert.Equal("Velocidade", linha.Rotulo);
        Assert.StartsWith("rodando a 2666 MT/s; os módulos aceitam 3200", linha.Texto);
    }

    [Fact]
    public void Velocidade_abaixo_da_nominal_aparece_no_slot()
    {
        var modulo = DadosDemonstracao.Coleta().Memoria.Modulos.Valor![0];

        Assert.Equal("DDR4-2666 (o módulo aceita 3200)", MontadorSecoes.TipoVelocidade(modulo).Valor);
    }

    [Fact]
    public async Task Botao_de_administrador_aparece_depois_da_coleta_como_usuario_comum()
    {
        var painel = new PainelPrincipal(Coletar, hoje: () => Hoje);
        Assert.False(painel.PodeElevar);

        await painel.AtualizarAsync();

        Assert.True(painel.PodeElevar);
        Assert.Contains("usuário comum", painel.TextoStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Botao_de_administrador_some_quando_ja_elevado_e_na_demonstracao()
    {
        var elevado = new PainelPrincipal(async c => (await Coletar(c)) with { Administrador = true }, hoje: () => Hoje);
        await elevado.AtualizarAsync();
        var demonstracao = PainelPrincipal.ComDemonstracao();
        await demonstracao.AtualizarAsync();

        Assert.False(elevado.PodeElevar);
        Assert.Contains("administrador", elevado.TextoStatus, StringComparison.Ordinal);
        Assert.False(demonstracao.PodeElevar);
    }

    [Fact]
    public async Task Elevacao_cancelada_vai_para_o_status_e_sai_na_proxima_coleta()
    {
        var painel = new PainelPrincipal(Coletar, hoje: () => Hoje);
        await painel.AtualizarAsync();

        painel.AvisarElevacaoCancelada();
        Assert.EndsWith("leitura como administrador cancelada", painel.TextoStatus, StringComparison.Ordinal);

        await painel.AtualizarAsync();
        Assert.DoesNotContain("cancelada", painel.TextoStatus, StringComparison.Ordinal);
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
