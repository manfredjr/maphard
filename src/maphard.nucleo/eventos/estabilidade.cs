using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Formatacao;

namespace MapHard.Nucleo.Eventos;

/// <summary>Um grupo do R27: quantos eventos, o mais recente e os detalhes (até <see cref="Estabilidade.DetalhesPorGrupo"/>).</summary>
public sealed record GrupoEventos(string Codigo, string Titulo, int Quantidade, DateTimeOffset? Ultimo, IReadOnlyList<string> Detalhes);

/// <summary>Seção Estabilidade (R15 na parte do Diagnóstico de Memória, R27 a R29).</summary>
public sealed record SecaoEstabilidade(
    int Dias,
    Campo<IReadOnlyList<GrupoEventos>> Grupos,
    Campo<string> DiagnosticoMemoria,
    Campo<double> IndiceEstabilidade,
    Campo<TimeSpan> TempoLigado,
    Campo<DateTimeOffset> UltimoBoot,
    Campo<DateTimeOffset> InstalacaoWindows);

/// <summary>Leituras do Windows fora do log: índice de estabilidade, tempo ligado, último boot e instalação.</summary>
public interface IFonteSistema
{
    double? IndiceEstabilidade();

    long? MilissegundosLigado();

    DateTimeOffset? UltimoBoot();

    DateTimeOffset? InstalacaoWindows();
}

/// <summary>
/// Grupos do R27, pelo quadro da tarefa 2 do plano da fatia 4. A contagem de telas azuis e de desligamentos
/// sai só do Kernel-Power 41, que tem página oficial ("Advanced troubleshooting for Event ID 41",
/// learn.microsoft.com): BugcheckCode em decimal, diferente de zero na tela azul; PowerButtonTimestamp diferente
/// de zero quando o botão foi segurado; os dois zerados "podem indicar problema na fonte de energia".
/// Disco: 7, 11, 51, 153 e 157; NTFS: 50, 55, 98 e 140 ("Troubleshoot data corruption and disk errors").
/// WHEA e Diagnóstico de Memória: a gravidade vem do nível do evento, porque os ids só têm fonte de fórum.
/// </summary>
public static partial class Estabilidade
{
    public const int DetalhesPorGrupo = 5;
    public const int LimitePorConsulta = 500;

    public const string TelasAzuis = "telas-azuis";
    public const string Desligamentos = "desligamentos";
    public const string Disco = "disco";
    public const string SistemaArquivos = "sistema-arquivos";
    public const string WheaCorrigido = "whea-corrigido";
    public const string WheaNaoCorrigido = "whea-nao-corrigido";

    private const string KernelPower = "Microsoft-Windows-Kernel-Power";
    private const string ProvedorDisco = "disk";
    private const string Whea = "Microsoft-Windows-WHEA-Logger";
    private const string DiagnosticoMemoriaProvedor = "Microsoft-Windows-MemoryDiagnostics-Results";

    private static readonly int[] _idsDisco = [7, 11, 51, 153, 157];
    private static readonly int[] _idsNtfs = [50, 55, 98, 140];

    public static IReadOnlyList<FiltroEvento> Filtros { get; } =
    [
        new(KernelPower, [41]),
        new(ProvedorDisco, _idsDisco),
        new("Ntfs", _idsNtfs),
        new("Microsoft-Windows-Ntfs", _idsNtfs),
        new(Whea),
        new(DiagnosticoMemoriaProvedor),
    ];

    public static IReadOnlyList<GrupoEventos> Agrupar(IReadOnlyList<EventoSistema> eventos)
    {
        var kp41 = eventos.Where(e => Eh(e, KernelPower, 41)).ToList();
        var telasAzuis = kp41.Where(e => Numero(e, "BugcheckCode") is > 0).ToList();
        var desligamentos = kp41.Where(e => Numero(e, "BugcheckCode") is null or 0).ToList();
        var disco = eventos.Where(e => e.Provedor.Equals(ProvedorDisco, StringComparison.OrdinalIgnoreCase) && _idsDisco.Contains(e.Id)).ToList();
        var ntfs = eventos.Where(e => e.Provedor.Contains("Ntfs", StringComparison.OrdinalIgnoreCase) && _idsNtfs.Contains(e.Id)).ToList();
        var whea = eventos.Where(e => e.Provedor.Equals(Whea, StringComparison.OrdinalIgnoreCase)).ToList();

        return
        [
            Grupo(TelasAzuis, "Telas azuis", telasAzuis, e => $"{Data(e)}: código 0x{Numero(e, "BugcheckCode"):X}"),
            Grupo(Desligamentos, "Desligamentos inesperados", desligamentos, e => $"{Data(e)}: {CausaDesligamento(e)}"),
            GrupoDisco(disco),
            Grupo(SistemaArquivos, "Erros do sistema de arquivos", ntfs, Mensagem),
            Grupo(WheaCorrigido, "Erros de hardware corrigidos", whea.Where(e => e.Nivel == LeitorEvento.Aviso).ToList(), Mensagem),
            Grupo(WheaNaoCorrigido, "Erros de hardware não corrigidos", whea.Where(e => e.Nivel is LeitorEvento.Critico or LeitorEvento.Erro).ToList(), Mensagem),
        ];
    }

    /// <summary>O resultado mais recente do Diagnóstico de Memória: nível de informação é "sem erros"; os outros, a mensagem do Windows.</summary>
    public static Campo<string> DiagnosticoMemoria(IReadOnlyList<EventoSistema> eventos, int dias)
    {
        var ultimo = eventos.Where(e => e.Provedor.Equals(DiagnosticoMemoriaProvedor, StringComparison.OrdinalIgnoreCase)).MaxBy(e => e.Momento);
        if (ultimo is null)
        {
            return Campo<string>.NaoInformado(FonteDado.Windows, $"nenhum teste de memória nos últimos {dias} dias");
        }

        return ultimo.Nivel == LeitorEvento.Informacao
            ? Campo<string>.Lido($"sem erros, em {Data(ultimo)}", FonteDado.Windows, ultimo.Mensagem)
            : Campo<string>.Lido($"{Data(ultimo)}: {ultimo.Mensagem ?? $"evento {ultimo.Id}"}", FonteDado.Windows);
    }

    /// <summary>"desligamento sem encerramento limpo", com a pista da página da Microsoft quando os campos ajudam.</summary>
    private static string CausaDesligamento(EventoSistema e) =>
        Numero(e, "PowerButtonTimestamp") is > 0
            ? "o botão de energia foi segurado"
            : Numero(e, "BugcheckCode") is 0 && Numero(e, "PowerButtonTimestamp") is 0
                ? "sem tela azul nem botão de energia; pode ser problema na fonte de energia"
                : "desligamento sem encerramento limpo";

    /// <summary>
    /// Agrupa pelo dispositivo que a mensagem cita (\Device\HarddiskN\DRn). O número é o que o disco tinha na
    /// hora do evento; o disco pode ter sido retirado ou ter mudado de número.
    /// </summary>
    private static GrupoEventos GrupoDisco(IReadOnlyList<EventoSistema> disco)
    {
        var porDispositivo = disco
            .GroupBy(e => e.Mensagem is { } m && DispositivoDisco().Match(m) is { Success: true } d ? d.Value : "dispositivo não informado")
            .OrderByDescending(g => g.Count())
            .Take(DetalhesPorGrupo)
            .Select(g => $"{g.Key}: {Formatador.Plural(g.Count(), "evento", "eventos")} (ids {string.Join(", ", g.Select(e => e.Id).Distinct().Order())}), o último em {Data(g.MaxBy(e => e.Momento)!)}")
            .ToList();
        if (porDispositivo.Count > 0)
        {
            porDispositivo.Add("O número do disco é o da hora do evento; o disco pode ter sido retirado ou ter mudado de número.");
        }

        return new GrupoEventos(Disco, "Erros de disco", disco.Count, disco.Count > 0 ? disco.Max(e => e.Momento) : null, porDispositivo);
    }

    private static GrupoEventos Grupo(string codigo, string titulo, IReadOnlyList<EventoSistema> eventos, Func<EventoSistema, string> detalhe) =>
        new(
            codigo,
            titulo,
            eventos.Count,
            eventos.Count > 0 ? eventos.Max(e => e.Momento) : null,
            eventos.OrderByDescending(e => e.Momento).Take(DetalhesPorGrupo).Select(detalhe).ToList());

    private static string Mensagem(EventoSistema e) => $"{Data(e)}: {e.Mensagem ?? $"evento {e.Id} de {e.Provedor}"}";

    private static string Data(EventoSistema e) => Formatador.DataHora(e.Momento.ToLocalTime());

    private static bool Eh(EventoSistema e, string provedor, int id) => e.Id == id && e.Provedor.Equals(provedor, StringComparison.OrdinalIgnoreCase);

    private static long? Numero(EventoSistema e, string campo) =>
        e.Dados.TryGetValue(campo, out var texto) && long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    /// <summary>
    /// Data do WMI no formato CIM_DATETIME (learn.microsoft.com, wmisdk/cim-datetime): yyyymmddHHMMSS.mmmmmmsUUU,
    /// com UUU em minutos de diferença para o UTC. Formato fora disso vira nulo.
    /// </summary>
    public static DateTimeOffset? DataCim(string? texto)
    {
        if (texto is not { Length: 25 } || texto[14] != '.' || texto[21] is not ('+' or '-')
            || !DateTime.TryParseExact(texto[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            || !int.TryParse(texto[15..21], NumberStyles.None, CultureInfo.InvariantCulture, out var micro)
            || !int.TryParse(texto[22..], NumberStyles.None, CultureInfo.InvariantCulture, out var minutos))
        {
            return null;
        }

        var deslocamento = TimeSpan.FromMinutes(texto[21] == '-' ? -minutos : minutos);
        return new DateTimeOffset(data.AddTicks(micro * 10L), deslocamento);
    }

    public static SecaoEstabilidade Montar(int dias, IReadOnlyList<EventoSistema>? eventos, string? falhaEventos, IFonteSistema? sistema, string? falhaSistema)
    {
        var grupos = eventos is null
            ? Campo<IReadOnlyList<GrupoEventos>>.Erro(FonteDado.Windows, falhaEventos ?? "log Sistema indisponível")
            : Campo<IReadOnlyList<GrupoEventos>>.Lido(Agrupar(eventos), FonteDado.Windows, $"log Sistema, últimos {dias} dias");
        var memoria = eventos is null ? Campo<string>.Erro(FonteDado.Windows, falhaEventos ?? "log Sistema indisponível") : DiagnosticoMemoria(eventos, dias);

        Campo<T> DoSistema<T>(Func<IFonteSistema, T?> ler, string semValor)
            where T : struct => sistema is null
                ? Campo<T>.Erro(FonteDado.Windows, falhaSistema ?? "leitura do Windows indisponível")
                : ler(sistema) is { } v ? Campo<T>.Lido(v, FonteDado.Windows) : Campo<T>.NaoSuportado(FonteDado.Windows, semValor);

        return new SecaoEstabilidade(
            dias,
            grupos,
            memoria,
            DoSistema(s => s.IndiceEstabilidade() is { } i ? Math.Round(i, 1) : (double?)null, "o Monitor de Confiabilidade não tem índice (desligado por política ou no Windows Server)"),
            DoSistema(s => s.MilissegundosLigado() is { } ms ? TimeSpan.FromMilliseconds(ms) : (TimeSpan?)null, "tempo ligado indisponível"),
            DoSistema(s => s.UltimoBoot(), "último boot não informado"),
            DoSistema(s => s.InstalacaoWindows(), "data de instalação não informada"));
    }

    [GeneratedRegex(@"\\Device\\Harddisk\d+\\DR\d+", RegexOptions.IgnoreCase)]
    private static partial Regex DispositivoDisco();
}

/// <summary>
/// Leitura real. Índice pelo Win32_ReliabilityStabilityMetrics (SystemStabilityIndex de 1 a 10, TimeGenerated);
/// último boot e instalação pelo Win32_OperatingSystem (LastBootUpTime, InstallDate), os dois pelo moniker do
/// WMI, sem pacote externo; tempo ligado pelo GetTickCount64, em milissegundos desde o boot.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteSistemaWindows : IFonteSistema
{
    private const string Moniker = @"winmgmts:{impersonationLevel=impersonate}!\\.\root\cimv2";

    /// <summary>
    /// Pela API de script do WMI, o provedor de confiabilidade só entrega as propriedades do primeiro item da
    /// consulta; nos outros, "erro não especificado" (conferido numa máquina real em 01/10/2026). Por isso a
    /// consulta vai em janelas crescentes a partir de agora e lê só o primeiro item da menor janela com
    /// resultado. O índice é calculado de hora em hora, então o valor é o das últimas horas.
    /// </summary>
    public double? IndiceEstabilidade()
    {
        foreach (var horas in new[] { 2, 24, 24 * 7, 24 * 31 })
        {
            var desde = DateTime.UtcNow.AddHours(-horas).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".000000+000";
            foreach (dynamic m in Consultar($"SELECT SystemStabilityIndex, TimeGenerated FROM Win32_ReliabilityStabilityMetrics WHERE TimeGenerated >= '{desde}'"))
            {
                return m.SystemStabilityIndex is null ? null : Convert.ToDouble(m.SystemStabilityIndex, CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    public long? MilissegundosLigado() => (long)GetTickCount64();

    public DateTimeOffset? UltimoBoot() => SistemaOperacional("LastBootUpTime");

    public DateTimeOffset? InstalacaoWindows() => SistemaOperacional("InstallDate");

    private static DateTimeOffset? SistemaOperacional(string propriedade)
    {
        foreach (dynamic so in Consultar($"SELECT {propriedade} FROM Win32_OperatingSystem"))
        {
            return Estabilidade.DataCim(propriedade == "InstallDate" ? so.InstallDate as string : so.LastBootUpTime as string);
        }

        return null;
    }

    private static IEnumerable<dynamic> Consultar(string consulta)
    {
        dynamic servico = Marshal.BindToMoniker(Moniker);
        foreach (dynamic item in servico.ExecQuery(consulta))
        {
            yield return item;
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial ulong GetTickCount64();
}
