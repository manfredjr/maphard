using MapHard.Nucleo.Eventos;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class EventosLeitorTestes
{
    /// <summary>XML de evento no formato do EvtRender, com valores fictícios.</summary>
    internal static string Xml(string provedor, int id, int nivel, string momentoUtc, params (string? Nome, string Valor)[] dados)
    {
        var dadosXml = string.Concat(dados.Select(d => d.Nome is null ? $"<Data>{d.Valor}</Data>" : $"<Data Name='{d.Nome}'>{d.Valor}</Data>"));
        return $"<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><Provider Name='{provedor}'/>"
            + $"<EventID Qualifiers='0'>{id}</EventID><Level>{nivel}</Level><TimeCreated SystemTime='{momentoUtc}'/>"
            + $"<Channel>System</Channel><Computer>ESTACAO-TESTE</Computer></System><EventData>{dadosXml}</EventData></Event>";
    }

    [Fact]
    public void Le_provedor_id_nivel_momento_e_dados_por_nome()
    {
        var e = LeitorEvento.Interpretar(Xml("Microsoft-Windows-Kernel-Power", 41, 1, "2026-09-20T13:17:29.1149034Z", ("BugcheckCode", "159"), ("PowerButtonTimestamp", "0")), "  O sistema reiniciou.  ")!;

        Assert.Equal("Microsoft-Windows-Kernel-Power", e.Provedor);
        Assert.Equal(41, e.Id);
        Assert.Equal(LeitorEvento.Critico, e.Nivel);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 13, 17, 29, TimeSpan.Zero).AddTicks(1149034), e.Momento);
        Assert.Equal(TimeSpan.Zero, e.Momento.Offset);
        Assert.Equal("159", e.Dados["BugcheckCode"]);
        Assert.Equal("O sistema reiniciou.", e.Mensagem);
    }

    [Fact]
    public void Dados_sem_nome_viram_posicao()
    {
        var e = LeitorEvento.Interpretar(Xml("disk", 51, 3, "2026-09-11T13:17:29Z", (null, @"\Device\Harddisk1\DR10"), (null, "x")))!;

        Assert.Equal(@"\Device\Harddisk1\DR10", e.Dados["1"]);
        Assert.Equal("x", e.Dados["2"]);
        Assert.Null(e.Mensagem);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<Event><System>")]
    [InlineData("<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System/></Event>")]
    public void Xml_malformado_ou_incompleto_vira_nulo(string? xml)
    {
        Assert.Null(LeitorEvento.Interpretar(xml));
    }

    [Fact]
    public void Consulta_xpath_com_provedores_e_periodo_em_milissegundos()
    {
        var consultas = LeitorEvento.Consultas([new FiltroEvento("disk"), new FiltroEvento("Microsoft-Windows-Kernel-Power", [41]), new FiltroEvento("EventLog", [6008, 6006])], 30);

        Assert.Equal(["*[System[(Provider[@Name='disk'] or (Provider[@Name='Microsoft-Windows-Kernel-Power'] and (EventID=41)) or (Provider[@Name='EventLog'] and (EventID=6008 or EventID=6006))) and TimeCreated[timediff(@SystemTime) <= 2592000000]]]"], consultas);
    }

    [Fact]
    public void Muitos_provedores_viram_mais_de_uma_consulta()
    {
        var filtros = Enumerable.Range(1, 8).Select(i => new FiltroEvento($"P{i}")).ToList();

        var consultas = LeitorEvento.Consultas(filtros, 1);

        Assert.Equal(2, consultas.Count);
        Assert.Contains("Provider[@Name='P6']", consultas[0], StringComparison.Ordinal);
        Assert.Contains("Provider[@Name='P7']", consultas[1], StringComparison.Ordinal);
    }

    [FatoWindows]
    public void Log_sistema_real_abre_sem_administrador()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var eventos = new FonteEventosWindows().Ler([new FiltroEvento("disk"), new FiltroEvento("Microsoft-Windows-Kernel-Power", [41]), new FiltroEvento("EventLog", [6008])], 365, 20);

        Assert.All(eventos, e => Assert.NotNull(LeitorEvento.Interpretar(e.Xml, e.Mensagem)));
    }
}
