using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Eventos;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class EventosEstabilidadeTestes
{
    private static EventoSistema E(string provedor, int id, int nivel = 3, string momento = "2026-09-20T13:00:00Z", string? mensagem = null, params (string Nome, string Valor)[] dados) =>
        LeitorEvento.Interpretar(EventosLeitorTestes.Xml(provedor, id, nivel, momento, dados.Select(d => ((string?)d.Nome, d.Valor)).ToArray()), mensagem)!;

    private static GrupoEventos Grupo(IReadOnlyList<EventoSistema> eventos, string codigo) => Estabilidade.Agrupar(eventos).Single(g => g.Codigo == codigo);

    private sealed class SistemaSimulado(double? indice = 6.24) : IFonteSistema
    {
        public double? IndiceEstabilidade() => indice;

        public long? MilissegundosLigado() => 90_061_000;

        public DateTimeOffset? UltimoBoot() => new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.FromHours(-3));

        public DateTimeOffset? InstalacaoWindows() => new DateTimeOffset(2024, 3, 2, 10, 0, 0, TimeSpan.FromHours(-3));
    }

    [Fact]
    public void Kernel_power_41_com_codigo_e_tela_azul_com_o_codigo_em_hexadecimal()
    {
        var g = Grupo([E("Microsoft-Windows-Kernel-Power", 41, 1, dados: [("BugcheckCode", "159"), ("PowerButtonTimestamp", "0")])], Estabilidade.TelasAzuis);

        Assert.Equal(1, g.Quantidade);
        Assert.EndsWith("código 0x9F", g.Detalhes[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Kernel_power_41_sem_codigo_e_desligamento_e_nao_tela_azul()
    {
        var eventos = new[] { E("Microsoft-Windows-Kernel-Power", 41, 1, dados: [("BugcheckCode", "0"), ("PowerButtonTimestamp", "0")]) };

        Assert.Equal(0, Grupo(eventos, Estabilidade.TelasAzuis).Quantidade);
        var g = Grupo(eventos, Estabilidade.Desligamentos);
        Assert.Equal(1, g.Quantidade);
        Assert.EndsWith("sem tela azul nem botão de energia; pode ser problema na fonte de energia", g.Detalhes[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Botao_de_energia_segurado_aparece_no_detalhe()
    {
        var g = Grupo([E("Microsoft-Windows-Kernel-Power", 41, 1, dados: [("BugcheckCode", "0"), ("PowerButtonTimestamp", "133700000000000000")])], Estabilidade.Desligamentos);

        Assert.EndsWith("o botão de energia foi segurado", g.Detalhes[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Outro_evento_do_kernel_power_nao_entra()
    {
        var eventos = new[] { E("Microsoft-Windows-Kernel-Power", 42, 4) };

        Assert.Equal(0, Grupo(eventos, Estabilidade.TelasAzuis).Quantidade);
        Assert.Equal(0, Grupo(eventos, Estabilidade.Desligamentos).Quantidade);
    }

    [Fact]
    public void Erros_de_disco_agrupados_pelo_dispositivo_da_mensagem_com_o_aviso_do_numero()
    {
        var g = Grupo(
        [
            E("disk", 51, mensagem: @"Erro detectado no dispositivo \Device\Harddisk1\DR10 durante uma operação de paginação."),
            E("disk", 51, momento: "2026-09-21T13:00:00Z", mensagem: @"Erro detectado no dispositivo \Device\Harddisk1\DR10 durante uma operação de paginação."),
            E("disk", 7, mensagem: @"O dispositivo \Device\Harddisk0\DR0 tem um bloco danificado."),
            E("disk", 15, mensagem: @"\Device\Harddisk0\DR0 não está pronto"),
        ], Estabilidade.Disco);

        Assert.Equal(3, g.Quantidade);
        Assert.StartsWith(@"\Device\Harddisk1\DR10: 2 eventos (ids 51)", g.Detalhes[0], StringComparison.Ordinal);
        Assert.StartsWith(@"\Device\Harddisk0\DR0: 1 evento (ids 7)", g.Detalhes[1], StringComparison.Ordinal);
        Assert.Contains("pode ter sido retirado", g.Detalhes[^1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ntfs", 55, 1)]
    [InlineData("Microsoft-Windows-Ntfs", 98, 1)]
    [InlineData("Ntfs", 12, 0)]
    public void Sistema_de_arquivos_pelos_ids_do_ntfs(string provedor, int id, int quantidade)
    {
        Assert.Equal(quantidade, Grupo([E(provedor, id, 2, mensagem: "A estrutura do sistema de arquivos está corrompida.")], Estabilidade.SistemaArquivos).Quantidade);
    }

    [Theory]
    [InlineData(3, Estabilidade.WheaCorrigido)]
    [InlineData(2, Estabilidade.WheaNaoCorrigido)]
    [InlineData(1, Estabilidade.WheaNaoCorrigido)]
    public void Whea_pelo_nivel_do_evento(int nivel, string codigo)
    {
        var eventos = new[] { E("Microsoft-Windows-WHEA-Logger", 999, nivel, mensagem: "Ocorreu um erro de hardware.") };

        Assert.Equal(1, Grupo(eventos, codigo).Quantidade);
        Assert.EndsWith("Ocorreu um erro de hardware.", Grupo(eventos, codigo).Detalhes[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Whea_de_informacao_nao_conta()
    {
        var eventos = new[] { E("Microsoft-Windows-WHEA-Logger", 999, 4) };

        Assert.Equal(0, Grupo(eventos, Estabilidade.WheaCorrigido).Quantidade);
        Assert.Equal(0, Grupo(eventos, Estabilidade.WheaNaoCorrigido).Quantidade);
    }

    [Fact]
    public void Grupos_sao_sempre_os_seis_mesmo_sem_eventos()
    {
        Assert.Equal(
            [Estabilidade.TelasAzuis, Estabilidade.Desligamentos, Estabilidade.Disco, Estabilidade.SistemaArquivos, Estabilidade.WheaCorrigido, Estabilidade.WheaNaoCorrigido],
            Estabilidade.Agrupar([]).Select(g => g.Codigo));
    }

    [Fact]
    public void Detalhes_limitados_aos_mais_recentes()
    {
        var eventos = Enumerable.Range(1, 9).Select(d => E("Microsoft-Windows-WHEA-Logger", 1, 3, $"2026-09-{d:00}T10:00:00Z", $"erro {d}")).ToList();

        var g = Grupo(eventos, Estabilidade.WheaCorrigido);

        Assert.Equal(9, g.Quantidade);
        Assert.Equal(Estabilidade.DetalhesPorGrupo, g.Detalhes.Count);
        Assert.EndsWith("erro 9", g.Detalhes[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostico_de_memoria_sem_evento_no_periodo()
    {
        var c = Estabilidade.DiagnosticoMemoria([], 30);

        Assert.Equal(EstadoCampo.NaoInformado, c.Estado);
        Assert.Equal("nenhum teste de memória nos últimos 30 dias", c.Motivo);
    }

    [Fact]
    public void Diagnostico_de_memoria_com_nivel_de_informacao_e_sem_erros()
    {
        var c = Estabilidade.DiagnosticoMemoria([E("Microsoft-Windows-MemoryDiagnostics-Results", 1201, 4, mensagem: "Nenhum erro foi detectado.")], 30);

        Assert.StartsWith("sem erros, em ", c.Valor, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostico_de_memoria_com_erro_mostra_a_mensagem_do_mais_recente()
    {
        var c = Estabilidade.DiagnosticoMemoria(
        [
            E("Microsoft-Windows-MemoryDiagnostics-Results", 1201, 4, "2026-09-01T10:00:00Z", "Nenhum erro foi detectado."),
            E("Microsoft-Windows-MemoryDiagnostics-Results", 1202, 2, "2026-09-20T10:00:00Z", "Foram detectados erros de hardware."),
        ], 30);

        Assert.EndsWith("Foram detectados erros de hardware.", c.Valor, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("20260930080000.000000-180", 2026, 9, 30, 8, -180)]
    [InlineData("20240302100000.500000+000", 2024, 3, 2, 10, 0)]
    public void Data_cim_do_wmi(string texto, int ano, int mes, int dia, int hora, int minutos)
    {
        var d = Estabilidade.DataCim(texto)!.Value;

        Assert.Equal(new DateTime(ano, mes, dia, hora, 0, 0), d.DateTime.AddTicks(-(d.DateTime.Ticks % TimeSpan.TicksPerSecond)));
        Assert.Equal(TimeSpan.FromMinutes(minutos), d.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("2026-09-30")]
    [InlineData("20260930080000.000000*180")]
    public void Data_cim_fora_do_formato_vira_nula(string? texto)
    {
        Assert.Null(Estabilidade.DataCim(texto));
    }

    [Fact]
    public void Secao_com_indice_arredondado_tempo_ligado_e_datas()
    {
        var s = Estabilidade.Montar(30, [], null, new SistemaSimulado(), null);

        Assert.Equal(6.2, s.IndiceEstabilidade.Valor);
        Assert.Equal(TimeSpan.FromMilliseconds(90_061_000), s.TempoLigado.Valor);
        Assert.Equal(2024, s.InstalacaoWindows.Valor.Year);
        Assert.Equal(6, s.Grupos.Valor!.Count);
        Assert.Equal("log Sistema, últimos 30 dias", s.Grupos.Motivo);
    }

    [Fact]
    public void Sem_indice_fica_nao_disponivel_com_o_motivo()
    {
        var s = Estabilidade.Montar(30, [], null, new SistemaSimulado(indice: null), null);

        Assert.Equal(EstadoCampo.NaoSuportado, s.IndiceEstabilidade.Estado);
        Assert.Contains("Monitor de Confiabilidade", s.IndiceEstabilidade.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Log_que_falhou_deixa_so_os_campos_dele_em_erro()
    {
        var s = Estabilidade.Montar(30, null, "acesso negado", new SistemaSimulado(), null);

        Assert.Equal(EstadoCampo.ErroLeitura, s.Grupos.Estado);
        Assert.Equal(EstadoCampo.ErroLeitura, s.DiagnosticoMemoria.Estado);
        Assert.Equal(6.2, s.IndiceEstabilidade.Valor);
    }

    [FatoWindows]
    public void Indice_real_entre_1_e_10_e_tempo_ligado_lido()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var fonte = new FonteSistemaWindows();
        var indice = fonte.IndiceEstabilidade();

        if (indice is { } i)
        {
            Assert.InRange(i, 1, 10);
        }

        Assert.True(fonte.MilissegundosLigado() > 0);
        Assert.NotNull(fonte.UltimoBoot());
        Assert.NotNull(fonte.InstalacaoWindows());
    }
}
