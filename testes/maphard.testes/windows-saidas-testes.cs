using System.Text.Json;
using MapHard.Nucleo.Baterias;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.LinhaDeComando;
using MapHard.Nucleo.Painel;
using MapHard.Nucleo.Relatorios;
using MapHard.Nucleo.Saude;
using MapHard.Nucleo.Video;
using MapHard.Nucleo.Windows11;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class WindowsSaidasTestes
{
    private static readonly DateOnly Hoje = new(2026, 10, 2);

    private static Task<ColetaMaquina> Coletar(FontesColeta? fontes = null) =>
        new Coletor(fontes ?? FontesSimuladas.Completas(), TimeSpan.FromSeconds(5), agora: () => new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(-3))).ColetarAsync();

    private static VerificacaoWindows11 Verificacao(params (string Item, EstadoRequisito Estado)[] itens) =>
        new(itens.Select(i => new ItemWindows11(i.Item, i.Estado, $"detalhe de {i.Item}")).ToList(), "25H2");

    [Fact]
    public void Windows11_tudo_atendido_e_bom()
    {
        var s = RegrasWindows.Windows11(Verificacao(("Processador", EstadoRequisito.Atende), ("TPM", EstadoRequisito.Atende)));

        Assert.Equal(EstadoSaude.Bom, s.Estado);
        Assert.Equal("aceita", RegrasWindows.TextoWindows11(Verificacao(("TPM", EstadoRequisito.Atende))));
    }

    [Fact]
    public void Windows11_ajuste_no_firmware_e_atencao()
    {
        var v = Verificacao(("Processador", EstadoRequisito.Atende), ("Secure Boot", EstadoRequisito.Configuracao));

        Assert.Equal(EstadoSaude.Atencao, RegrasWindows.Windows11(v).Estado);
        Assert.Equal("aceita depois de ajustar o firmware: Secure Boot: detalhe de Secure Boot", RegrasWindows.TextoWindows11(v));
    }

    [Theory]
    [InlineData("Processador")]
    [InlineData("Memória")]
    public void Windows11_processador_ou_memoria_abaixo_e_ruim(string item)
    {
        var v = Verificacao((item, EstadoRequisito.NaoAtende), ("Secure Boot", EstadoRequisito.Configuracao));

        Assert.Equal(EstadoSaude.Ruim, RegrasWindows.Windows11(v).Estado);
        Assert.StartsWith($"não aceita: {item}", RegrasWindows.TextoWindows11(v), StringComparison.Ordinal);
    }

    [Fact]
    public void Windows11_processador_fora_da_lista_e_desconhecido_nunca_ruim()
    {
        var v = Verificacao(("Processador", EstadoRequisito.Desconhecido), ("TPM", EstadoRequisito.Atende));

        Assert.Equal(EstadoSaude.Desconhecido, RegrasWindows.Windows11(v).Estado);
        Assert.StartsWith("não confirmado: ", RegrasWindows.TextoWindows11(v), StringComparison.Ordinal);
    }

    private static Campo<IReadOnlyList<Bateria>> Baterias(params double[] desgastes) =>
        Campo<IReadOnlyList<Bateria>>.Lido(desgastes.Select(d => LeitorBateria.Interpretar(new BateriaBruta(BateriaTestes.Informacao(100_000, (uint)(100_000 * (1 - (d / 100)))), "Bateria", "Fabricante"))!).ToList(), FonteDado.Windows);

    [Theory]
    [InlineData(30, EstadoSaude.Bom)]
    [InlineData(34, EstadoSaude.Atencao)]
    [InlineData(50, EstadoSaude.Atencao)]
    [InlineData(51, EstadoSaude.Ruim)]
    public void Bateria_pelo_desgaste(double desgaste, EstadoSaude esperado)
    {
        Assert.Equal(esperado, RegrasWindows.Bateria(Baterias(desgaste)).Estado);
    }

    [Fact]
    public void Bateria_com_o_motivo_escrito()
    {
        Assert.Equal(["desgaste de 34%"], RegrasWindows.Bateria(Baterias(34)).Motivos);
    }

    [Fact]
    public void Sem_bateria_e_bom_e_sem_leitura_e_desconhecido()
    {
        Assert.Equal(EstadoSaude.Bom, RegrasWindows.Bateria(Baterias()).Estado);
        Assert.Equal(EstadoSaude.Desconhecido, RegrasWindows.Bateria(Campo<IReadOnlyList<Bateria>>.Erro(FonteDado.Windows, "falhou")).Estado);
    }

    [Fact]
    public async Task Coleta_traz_video_monitores_bateria_rede_windows11_e_ativacao()
    {
        var c = await Coletar();

        Assert.Equal("Placa de Vídeo Exemplo", Assert.Single(c.Video.Valor!).Nome.Valor);
        Assert.Equal("MON", Assert.Single(c.Monitores.Valor!).Fabricante.Valor);
        Assert.Equal(25.0, Assert.Single(c.Bateria.Valor!).Desgaste.Valor);
        Assert.Equal(["Ethernet", "Wi-Fi"], c.Rede.Valor!.Select(p => p.Nome.Valor));
        Assert.Equal("ativado", c.Identificacao.Windows.Ativacao.Valor);
        Assert.True(c.Identificacao.Windows.Arquitetura.FoiLido);
        Assert.Equal(6, c.Windows11.Itens.Count);
        Assert.Equal(EstadoRequisito.Atende, c.Windows11.Itens.Single(i => i.Item == "Armazenamento").Estado);
        Assert.Equal(EstadoRequisito.Desconhecido, c.Windows11.Itens.Single(i => i.Item == "Processador").Estado);
    }

    private sealed class VideoQueFalha : IFonteVideo
    {
        public IReadOnlyList<byte[]> Adaptadores() => throw new InvalidOperationException("a DXGI não abriu");
    }

    private sealed class AtivacaoQueFalha : MapHard.Nucleo.Windows.IFonteAtivacao
    {
        public int Estado() => throw new InvalidOperationException("a consulta da licença falhou");
    }

    [Fact]
    public async Task Fonte_que_falha_deixa_so_os_campos_dela_em_erro()
    {
        var c = await Coletar(FontesSimuladas.Completas(video: new VideoQueFalha(), ativacao: new AtivacaoQueFalha()));

        Assert.Equal(EstadoCampo.ErroLeitura, c.Video.Estado);
        Assert.Equal("a DXGI não abriu", c.Video.Motivo);
        Assert.Equal(EstadoCampo.ErroLeitura, c.Identificacao.Windows.Ativacao.Estado);
        Assert.True(c.Monitores.FoiLido);
    }

    [Fact]
    public async Task Json_da_fatia_5_vai_e_volta_igual()
    {
        var original = await Coletar();
        var texto = ExportadorJson.Serializar(original);
        var json = JsonDocument.Parse(texto).RootElement;

        Assert.Equal(ColetaMaquina.VersaoAtual, json.GetProperty("versaoFormato").GetInt32());
        Assert.Equal("ativado", json.GetProperty("identificacao").GetProperty("windows").GetProperty("ativacao").GetProperty("valor").GetString());
        Assert.Equal("configuracao", JsonDocument.Parse(ExportadorJson.Serializar(DadosDemonstracao.Coleta())).RootElement.GetProperty("windows11").GetProperty("itens")[3].GetProperty("estado").GetString());
        Assert.Equal(texto, ExportadorJson.Serializar(ExportadorJson.Desserializar(texto)!));
    }

    private static IReadOnlyList<SecaoTela> Secoes(ColetaMaquina c) => MontadorSecoes.Montar(c, Hoje);

    private static CartaoTela Cartao(ColetaMaquina c, string secao, string titulo) =>
        Secoes(c).Single(s => s.Id == secao).Cartoes.Single(k => k.Titulo == titulo);

    [Fact]
    public async Task Painel_mostra_video_monitor_bateria_e_windows()
    {
        var c = await Coletar();

        var placa = Cartao(c, MontadorSecoes.Video, "Placa de Vídeo Exemplo");
        Assert.Equal("4 GB", placa.Linhas.Single(l => l.Rotulo == "Memória dedicada").Texto);
        var monitor = Cartao(c, MontadorSecoes.Video, "Monitor 1: MS 1234");
        Assert.Equal("17,1 polegadas", monitor.Linhas.Single(l => l.Rotulo == "Tamanho").Texto);

        var bateria = Cartao(c, MontadorSecoes.Bateria, "Bateria");
        Assert.Equal("Bom", bateria.Selo);
        Assert.Equal("56,0 Wh", bateria.Linhas.Single(l => l.Rotulo == "Capacidade de projeto").Texto);

        var w11 = Cartao(c, MontadorSecoes.Windows, "Windows 11, item por item");
        Assert.Equal("Desconhecido", w11.Selo);
        Assert.StartsWith("não confirmado", w11.Linhas.Single(l => l.Rotulo == "Processador").Texto, StringComparison.Ordinal);
        Assert.Equal("atende: TPM 2.0", w11.Linhas.Single(l => l.Rotulo == "TPM").Texto);

        var rede = Cartao(c, MontadorSecoes.Windows, "Placas de rede");
        Assert.Equal("Wi-Fi, 02-00-5E-10-20-AB, 721 Mb/s", rede.Linhas.Single(l => l.Rotulo == "Wi-Fi").Texto);
        Assert.Equal("ativado", Cartao(c, MontadorSecoes.Windows, "Windows").Linhas.Single(l => l.Rotulo == "Ativação").Texto);
    }

    [Fact]
    public async Task Desktop_sem_bateria_diz_que_nao_ha_bateria_sem_selo()
    {
        var c = await Coletar(FontesSimuladas.Completas(baterias: new SemBateria()));

        var cartao = Cartao(c, MontadorSecoes.Bateria, "Bateria");
        Assert.Null(cartao.Selo);
        Assert.Equal("não disponível neste equipamento", Assert.Single(cartao.Linhas).Texto);
        Assert.Contains("Bateria:     não disponível neste equipamento", ExecutorCli.Resumo(c));
    }

    private sealed class SemBateria : IFonteBaterias
    {
        public IReadOnlyList<BateriaBruta> Ler() => [];
    }

    [Fact]
    public async Task Linha_de_comando_traz_windows11_video_bateria_e_rede()
    {
        var linhas = ExecutorCli.Resumo(await Coletar()).ToList();

        Assert.Contains(linhas, l => l.StartsWith("Windows 11:  não confirmado: Processador: não consta na lista do MapHard", StringComparison.Ordinal));
        Assert.Contains("Vídeo:       Placa de Vídeo Exemplo, 4 GB", linhas);
        Assert.Contains("Bateria:     desgaste de 25%", linhas);
        Assert.Contains("Rede:        Ethernet 1 Gb/s; Wi-Fi 721 Mb/s", linhas);
        Assert.Contains(linhas, l => l.StartsWith("Windows:     Windows 11 Pro 23H2 (22631.4169), ", StringComparison.Ordinal) && l.EndsWith(", ativado", StringComparison.Ordinal));
    }

    [Fact]
    public void Demonstracao_tem_placa_monitor_bateria_duas_redes_e_windows11_em_atencao()
    {
        var c = DadosDemonstracao.Coleta();

        Assert.Single(c.Video.Valor!);
        Assert.Single(c.Monitores.Valor!);
        Assert.Single(c.Bateria.Valor!);
        Assert.Equal(2, c.Rede.Valor!.Count);
        Assert.Equal(EstadoSaude.Atencao, RegrasWindows.Windows11(c.Windows11).Estado);
        Assert.Equal("Atenção", Cartao(c, MontadorSecoes.Windows, "Windows 11, item por item").Selo);
        Assert.Contains("Windows 11:  aceita depois de ajustar o firmware: Secure Boot: Secure Boot desligado: ligar no firmware", ExecutorCli.Resumo(c));
    }
}
