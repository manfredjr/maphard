using System.Text.Json;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Dispositivos;
using MapHard.Nucleo.Eventos;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.LinhaDeComando;
using MapHard.Nucleo.Painel;
using MapHard.Nucleo.Relatorios;
using MapHard.Nucleo.Saude;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class EstabilidadeSaidasTestes
{
    private static readonly DateOnly Hoje = new(2026, 10, 1);

    private static (string, string?) Kp41(string momento, string codigo) =>
        (EventosLeitorTestes.Xml("Microsoft-Windows-Kernel-Power", 41, 1, momento, ("BugcheckCode", codigo), ("PowerButtonTimestamp", "0")), "O sistema reiniciou sem desligar corretamente.");

    private static Task<ColetaMaquina> Coletar(FontesColeta fontes, int dias = 30) =>
        new Coletor(fontes, TimeSpan.FromSeconds(5), diasEventos: dias, agora: () => new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.FromHours(-3))).ColetarAsync();

    private static SecaoEstabilidade Secao(params (string, string?)[] eventos) =>
        Estabilidade.Montar(30, eventos.Select(e => LeitorEvento.Interpretar(e.Item1, e.Item2)!).ToList(), null, (LeiturasSistema?)null, "sem sistema");

    [Theory]
    [InlineData(TimeSpan.TicksPerHour * 77, "3 dias e 5 horas")]
    [InlineData(TimeSpan.TicksPerMinute * 312, "5 horas e 12 minutos")]
    [InlineData(TimeSpan.TicksPerMinute * 8, "8 minutos")]
    [InlineData(TimeSpan.TicksPerDay * 2, "2 dias")]
    public void Duracao_com_as_duas_maiores_unidades(long ticks, string esperado)
    {
        Assert.Equal(esperado, Formatador.Duracao(TimeSpan.FromTicks(ticks)));
    }

    [Fact]
    public void Estabilidade_sem_eventos_e_bom()
    {
        Assert.Equal(EstadoSaude.Bom, RegrasEstabilidade.Estabilidade(Secao()).Estado);
    }

    [Fact]
    public void Uma_tela_azul_e_atencao_com_o_motivo()
    {
        var s = RegrasEstabilidade.Estabilidade(Secao(Kp41("2026-09-20T10:00:00Z", "159")));

        Assert.Equal(EstadoSaude.Atencao, s.Estado);
        Assert.Equal(["1 tela azul nos últimos 30 dias"], s.Motivos);
    }

    [Fact]
    public void Um_desligamento_inesperado_e_atencao()
    {
        var s = RegrasEstabilidade.Estabilidade(Secao(Kp41("2026-09-20T10:00:00Z", "0")));

        Assert.Equal(["1 desligamento inesperado nos últimos 30 dias"], s.Motivos);
        Assert.Equal(EstadoSaude.Atencao, s.Estado);
    }

    [Fact]
    public void Tres_telas_azuis_e_ruim()
    {
        var s = RegrasEstabilidade.Estabilidade(Secao(Kp41("2026-09-20T10:00:00Z", "159"), Kp41("2026-09-21T10:00:00Z", "26"), Kp41("2026-09-22T10:00:00Z", "80")));

        Assert.Equal(EstadoSaude.Ruim, s.Estado);
        Assert.Equal(["3 telas azuis nos últimos 30 dias"], s.Motivos);
    }

    [Fact]
    public void Erro_de_hardware_nao_corrigido_e_ruim_e_o_corrigido_nao_pesa()
    {
        var naoCorrigido = (EventosLeitorTestes.Xml("Microsoft-Windows-WHEA-Logger", 18, 2, "2026-09-20T10:00:00Z"), "Erro fatal de hardware.");
        var corrigido = (EventosLeitorTestes.Xml("Microsoft-Windows-WHEA-Logger", 19, 3, "2026-09-20T10:00:00Z"), "Erro corrigido.");

        Assert.Equal(EstadoSaude.Ruim, RegrasEstabilidade.Estabilidade(Secao(naoCorrigido)).Estado);
        Assert.Equal(EstadoSaude.Bom, RegrasEstabilidade.Estabilidade(Secao(corrigido)).Estado);
    }

    [Fact]
    public void Log_nao_lido_e_desconhecido()
    {
        var s = Estabilidade.Montar(30, null, "acesso negado", (LeiturasSistema?)null, "x");

        Assert.Equal(new[] { "acesso negado" }, RegrasEstabilidade.Estabilidade(s).Motivos);
        Assert.Equal(EstadoSaude.Desconhecido, RegrasEstabilidade.Estabilidade(s).Estado);
    }

    [Fact]
    public async Task Diagnostico_de_memoria_com_erro_deixa_a_memoria_ruim()
    {
        var erro = (EventosLeitorTestes.Xml("Microsoft-Windows-MemoryDiagnostics-Results", 1202, 2, "2026-09-25T10:00:00Z"), "Foram detectados erros de hardware.");
        var c = await Coletar(FontesSimuladas.Completas(eventos: new FontesSimuladas.Eventos(erro)));

        var s = RegrasEstabilidade.Memoria(c.Memoria, c.Estabilidade);

        Assert.Equal(EstadoSaude.Ruim, s.Estado);
        Assert.Equal(["o Diagnóstico de Memória do Windows encontrou erro"], s.Motivos);
    }

    [Fact]
    public async Task Dispositivo_com_problema_e_atencao_e_desativado_nao_pesa()
    {
        var semDriver = new DispositivoBruto("Leitor Exemplo", "Unknown", [], 28, null);
        var desativado = new DispositivoBruto("Placa Exemplo", "Net", [], 22, null);

        var com = RegrasEstabilidade.Dispositivos((await Coletar(FontesSimuladas.Completas(dispositivos: new FontesSimuladas.Dispositivos(semDriver)))).Dispositivos);
        var sem = RegrasEstabilidade.Dispositivos((await Coletar(FontesSimuladas.Completas(dispositivos: new FontesSimuladas.Dispositivos(desativado)))).Dispositivos);

        Assert.Equal(EstadoSaude.Atencao, com.Estado);
        Assert.Equal(["1 dispositivo com problema"], com.Motivos);
        Assert.Equal(EstadoSaude.Bom, sem.Estado);
    }

    [Fact]
    public async Task Coleta_traz_estabilidade_dispositivos_e_chipset_e_usa_o_periodo_pedido()
    {
        var eventos = new FontesSimuladas.Eventos(Kp41("2026-09-20T10:00:00Z", "159"));
        var c = await Coletar(FontesSimuladas.Completas(eventos: eventos), dias: 90);

        Assert.Equal([90], eventos.DiasPedidos);
        Assert.Equal(90, c.Estabilidade.Dias);
        Assert.Equal(1, c.Estabilidade.Grupos.Valor!.Single(g => g.Codigo == Estabilidade.TelasAzuis).Quantidade);
        Assert.Equal(8.7, c.Estabilidade.IndiceEstabilidade.Valor);
        Assert.Equal("Alder Lake PCH eSPI Controller", c.Placa.Chipset.Valor);
        Assert.Equal(2, c.Dispositivos.Total.Valor);
        Assert.Equal(ColetaMaquina.VersaoAtual, c.VersaoFormato);
    }

    [Fact]
    public async Task Json_traz_as_secoes_novas_e_volta_igual()
    {
        var original = await Coletar(FontesSimuladas.Completas(eventos: new FontesSimuladas.Eventos(Kp41("2026-09-20T10:00:00Z", "159"))));
        var texto = ExportadorJson.Serializar(original);
        var json = JsonDocument.Parse(texto).RootElement;

        Assert.Equal(4, json.GetProperty("versaoFormato").GetInt32());
        Assert.Equal(30, json.GetProperty("estabilidade").GetProperty("dias").GetInt32());
        Assert.Equal("Alder Lake PCH eSPI Controller", json.GetProperty("placa").GetProperty("chipset").GetProperty("valor").GetString());
        Assert.Equal(texto, ExportadorJson.Serializar(ExportadorJson.Desserializar(texto)!));
    }

    [Fact]
    public async Task Painel_mostra_estabilidade_dispositivos_chipset_e_erros_de_memoria()
    {
        var c = await Coletar(FontesSimuladas.Completas(
            eventos: new FontesSimuladas.Eventos(Kp41("2026-09-20T10:00:00Z", "159")),
            dispositivos: new FontesSimuladas.Dispositivos(new DispositivoBruto("Leitor Exemplo", "Unknown", [], 28, null))));
        var secoes = MontadorSecoes.Montar(c, Hoje);

        var estabilidade = secoes.Single(s => s.Id == MontadorSecoes.Estabilidade);
        Assert.Equal(["Eventos dos últimos 30 dias", "Telas azuis", "Este Windows"], estabilidade.Cartoes.Select(x => x.Titulo));
        Assert.Equal(("Atenção", "atencao"), (estabilidade.Cartoes[0].Selo, estabilidade.Cartoes[0].TomSelo));
        Assert.Equal("1 tela azul nos últimos 30 dias", estabilidade.Cartoes[0].Linhas[0].Texto);
        Assert.EndsWith("código 0x9F", estabilidade.Cartoes[1].Linhas[0].Texto, StringComparison.Ordinal);
        Assert.Equal("8,7 de 10", estabilidade.Cartoes[2].Linhas.Single(l => l.Rotulo == "Índice de estabilidade").Texto);
        Assert.Equal("1 dia e 2 horas", estabilidade.Cartoes[2].Linhas.Single(l => l.Rotulo == "Tempo ligado").Texto);

        var dispositivos = secoes.Single(s => s.Id == MontadorSecoes.Dispositivos).Cartoes[0];
        Assert.Equal("Atenção", dispositivos.Selo);
        Assert.Equal("código 28: O driver do dispositivo não está instalado.", dispositivos.Linhas.Single(l => l.Rotulo == "Leitor Exemplo").Texto);

        var placa = secoes.Single(s => s.Id == MontadorSecoes.Placa).Cartoes.SelectMany(x => x.Linhas);
        Assert.Equal("Alder Lake PCH eSPI Controller", placa.Single(l => l.Rotulo == "Chipset").Texto);

        var memoria = secoes.Single(s => s.Id == MontadorSecoes.Memoria).Cartoes.Single(x => x.Titulo == "Erros de memória");
        Assert.Equal("Bom", memoria.Selo);
        Assert.Equal("não informado pelo fabricante: nenhum teste de memória nos últimos 30 dias", memoria.Linhas[0].Texto);
        Assert.All(secoes.SelectMany(s => s.Cartoes).SelectMany(x => x.Linhas), l => Assert.False(string.IsNullOrWhiteSpace(l.Texto), l.Rotulo));
    }

    [Fact]
    public async Task Linha_de_comando_mostra_estabilidade_dispositivos_e_chipset()
    {
        var c = await Coletar(FontesSimuladas.Completas(eventos: new FontesSimuladas.Eventos(Kp41("2026-09-20T10:00:00Z", "159"), Kp41("2026-09-21T10:00:00Z", "0"))));
        var linhas = ExecutorCli.Resumo(c).ToList();

        Assert.Contains("Estabilidade: 1 tela azul, 1 desligamento inesperado em 30 dias; índice 8,7", linhas);
        Assert.Contains("Dispositivos: nenhum com problema", linhas);
        Assert.Contains(linhas, l => l.EndsWith(", chipset Alder Lake PCH eSPI Controller", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(new[] { "coletar", "--dias", "90" }, true, 90)]
    [InlineData(new[] { "coletar" }, true, 30)]
    [InlineData(new[] { "coletar", "--dias", "45" }, false, 30)]
    [InlineData(new[] { "coletar", "--dias" }, false, 30)]
    public void Opcao_dias_aceita_30_ou_90(string[] args, bool valido, int dias)
    {
        var a = ArgumentosCli.Interpretar(args);

        Assert.Equal(valido, a.Valido);
        Assert.Equal(dias, a.Dias);
    }

    [Fact]
    public async Task Painel_troca_o_periodo_e_coleta_de_novo()
    {
        var pedidos = new List<int>();
        var painel = new PainelPrincipal((d, _) =>
        {
            pedidos.Add(d);
            return Task.FromResult(DadosDemonstracao.Coleta());
        }, hoje: () => Hoje);
        await painel.AtualizarAsync();

        await painel.AlterarDiasAsync(90);
        await painel.AlterarDiasAsync(90);
        await painel.AlterarDiasAsync(45);

        Assert.Equal([30, 90], pedidos);
        Assert.Equal(90, painel.DiasEventos);
    }

    [Fact]
    public void Demonstracao_tem_estabilidade_em_atencao_e_um_dispositivo_sem_driver()
    {
        var c = DadosDemonstracao.Coleta();

        Assert.Equal(EstadoSaude.Atencao, RegrasEstabilidade.Estabilidade(c.Estabilidade).Estado);
        Assert.Equal(28, Assert.Single(c.Dispositivos.ComProblema.Valor!).Codigo);
        Assert.Equal(FonteDado.Demonstracao, c.Placa.Chipset.Fonte);
    }
}
