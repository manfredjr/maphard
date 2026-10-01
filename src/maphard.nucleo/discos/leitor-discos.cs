using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Saude;
using MapHard.Nucleo.Smart;
using MapHard.Nucleo.Tabelas;

namespace MapHard.Nucleo.Discos;

/// <summary>
/// Uma linha da tabela SMART. Em ATA, o id em hexadecimal, os valores normalizados e o bruto; em NVMe, o
/// campo do log de saúde, com Atual, Pior e Limite nulos e o valor em <see cref="Bruto"/>.
/// </summary>
public sealed record LinhaSmart(string Id, string Nome, string NomeOriginal, int? Atual, int? Pior, int? Limite, decimal Bruto);

/// <summary>Um volume com letra. O BitLocker é texto porque o Windows informa três estados.</summary>
public sealed record VolumeTela(
    Campo<string> Letra,
    Campo<string> Rotulo,
    Campo<string> SistemaArquivos,
    Campo<long> Livre,
    Campo<long> Total,
    Campo<string> BitLocker);

/// <summary>Um disco físico (R20 a R25), com a saúde pelas regras da seção 8.</summary>
public sealed record DiscoTela(
    int Numero,
    Campo<string> Modelo,
    Campo<string> Firmware,
    Campo<string> NumeroSerie,
    Campo<long> Tamanho,
    Campo<string> Tipo,
    Campo<string> Interface,
    Campo<int> Rotacao,
    Campo<bool> Trim,
    Campo<string> EstiloParticao,
    SaudeDisco Saude,
    Campo<int> Temperatura,
    Campo<long> HorasLigado,
    Campo<long> CiclosEnergia,
    Campo<decimal> DadosGravadosBytes,
    Campo<int> VidaUsada,
    Campo<IReadOnlyList<LinhaSmart>> Smart,
    IReadOnlyList<VolumeTela> Volumes);

/// <summary>Seção Discos. Volume que não se ligou a nenhum disco (unidade virtual, volume em vários discos) fica à parte.</summary>
public sealed record SecaoDiscos(Campo<IReadOnlyList<DiscoTela>> Discos, IReadOnlyList<VolumeTela> VolumesSemDisco);

/// <summary>O que o coletor leu de um disco: o bruto, e o SMART ATA ou o log NVMe conforme o barramento.</summary>
public sealed record LeituraDisco(DiscoBruto Bruto, SmartAtaBruto? Ata, byte[]? LogNvme, byte[]? IdentificacaoNvme, string? FalhaSmart);

/// <summary>Junta as leituras de disco na seção, com o estado de cada campo e a saúde de cada disco.</summary>
public static class LeitorDiscos
{
    private const byte HorasAta = 0x09;
    private const byte CiclosAta = 0x0C;
    private const string GravadosAta = "o atributo de dados gravados muda de unidade conforme o fabricante; veja a tabela SMART";

    public static SecaoDiscos Montar(IReadOnlyList<LeituraDisco>? discos, string? falhaDiscos, IReadOnlyList<VolumeBruto> volumes, AtributosSmart nomes)
    {
        if (discos is null)
        {
            return new SecaoDiscos(Campo<IReadOnlyList<DiscoTela>>.Erro(FonteDado.Armazenamento, falhaDiscos ?? "lista de discos indisponível"), volumes.Select(Volume).ToList());
        }

        var lista = discos.Select(d => Disco(d, Volumes.DoDisco(volumes, d.Bruto.Numero), nomes)).ToList();
        var semDisco = Volumes.SemDisco(volumes, discos.Select(d => d.Bruto.Numero)).Select(Volume).ToList();
        return new SecaoDiscos(
            lista.Count == 0
                ? Campo<IReadOnlyList<DiscoTela>>.NaoInformado(FonteDado.Armazenamento, "o Windows não listou nenhum disco")
                : Campo<IReadOnlyList<DiscoTela>>.Lido(lista, FonteDado.Armazenamento),
            semDisco);
    }

    internal static DiscoTela Disco(LeituraDisco l, IReadOnlyList<VolumeBruto> volumes, AtributosSmart nomes)
    {
        var d = l.Bruto;
        var descritor = DescritorArmazenamento.Interpretar(d.Descritor);
        var barramento = descritor?.Barramento ?? -1;
        var tipo = DescritorArmazenamento.Classificar(barramento, d.PenalidadeBusca);
        const FonteDado a = FonteDado.Armazenamento;

        var modelo = Campo.Texto(descritor?.Modelo, a);
        var firmware = Campo.Texto(descritor?.Firmware, a);
        var serie = Campo.Texto(descritor?.NumeroSerie, a);
        var tamanho = Campo.DeOpcional(d.TamanhoBytes, a);
        var trim = Campo.DeOpcional(d.Trim, a);
        var particao = Campo.Texto(DescritorArmazenamento.NomeEstiloParticao(d.EstiloParticao), a);
        var volumesTela = volumes.Select(Volume).ToList();

        if (barramento == DescritorArmazenamento.BarramentoNvme)
        {
            var saude = LeitorSaudeNvme.Interpretar(l.LogNvme, l.IdentificacaoNvme);
            var falhaNvme = l.FalhaSmart ?? FonteDiscosWindows.ControladoraSemSmart;
            Campo<T> DoLog<T>(Func<SaudeNvme, T?> ler)
                where T : struct => saude is null
                    ? Campo<T>.NaoSuportado(FonteDado.Smart, falhaNvme)
                    : ler(saude) is { } v ? Campo<T>.Lido(v, FonteDado.Smart) : Campo<T>.NaoInformado(FonteDado.Smart);

            var link = DescritorArmazenamento.LinkPcie(d.VelocidadeLinkPcie, d.LarguraLinkPcie);
            return new DiscoTela(
                d.Numero, modelo, firmware, serie, tamanho,
                Campo<string>.Lido(DescritorArmazenamento.NomeTipo(tipo), a),
                Campo<string>.Lido(link is null ? "NVMe" : $"NVMe, {link}", a),
                Campo<int>.NaoSuportado(a, "SSD não tem rotação"),
                trim, particao,
                RegrasDisco.Nvme(saude, l.FalhaSmart),
                DoLog(s => s.TemperaturaC),
                DoLog(s => (long?)Math.Min(s.HorasLigado, long.MaxValue)),
                DoLog(s => (long?)Math.Min(s.CiclosEnergia, long.MaxValue)),
                DoLog(s => (decimal?)s.DadosGravadosBytes),
                DoLog(s => (int?)s.PercentualUsado),
                saude is null ? Campo<IReadOnlyList<LinhaSmart>>.NaoSuportado(FonteDado.Smart, falhaNvme) : Campo<IReadOnlyList<LinhaSmart>>.Lido(LinhasNvme(saude), FonteDado.Smart),
                volumesTela);
        }

        var leitura = l.Ata is null
            ? new LeituraSmartAta([], null, null, null, null, null, l.FalhaSmart ?? FonteDiscosWindows.ControladoraSemSmart)
            : LeitorSmartAta.Interpretar(l.FalhaSmart is { } f ? l.Ata with { Falha = f } : l.Ata);
        // Sem administrador, "requer administrador"; com a controladora sem SMART, "não disponível" com o motivo.
        var falhaSmart = leitura.Falha;
        Campo<T> SemSmart<T>() => falhaSmart == Volumes.RequerAdministrador
            ? Campo<T>.RequerAdministrador(FonteDado.Smart)
            : Campo<T>.NaoSuportado(FonteDado.Smart, falhaSmart);

        Campo<T> DoSmart<T>(Func<Campo<T>> ler) => falhaSmart is null ? ler() : SemSmart<T>();

        var interfaceTexto = leitura.VelocidadeSata ?? DescritorArmazenamento.NomeBarramento(barramento);
        return new DiscoTela(
            d.Numero, modelo, firmware, serie, tamanho,
            Campo<string>.Lido(DescritorArmazenamento.NomeTipo(tipo), a),
            interfaceTexto is null ? Campo<string>.NaoInformado(a) : Campo<string>.Lido(interfaceTexto, leitura.VelocidadeSata is null ? a : FonteDado.Smart),
            falhaSmart is not null && tipo == TipoDisco.Hdd
                ? SemSmart<int>()
                : leitura.RotacaoRpm is { } rpm
                    ? Campo<int>.Lido(rpm, FonteDado.Smart)
                    : tipo == TipoDisco.SsdSata || leitura.Ssd == true ? Campo<int>.NaoSuportado(a, "SSD não tem rotação") : Campo<int>.NaoInformado(FonteDado.Smart),
            trim, particao,
            RegrasDisco.Ata(leitura, tipo, nomes),
            DoSmart(() => RegrasDisco.TemperaturaAta(leitura) is { } t ? Campo<int>.Lido(t, FonteDado.Smart) : Campo<int>.NaoInformado(FonteDado.Smart)),
            DoSmart(() => Atributo(leitura, HorasAta, 0xFF_FFFF)),
            DoSmart(() => Atributo(leitura, CiclosAta, 0xFFFF_FFFF_FFFF)),
            DoSmart(() => Campo<decimal>.NaoInformado(FonteDado.Smart, GravadosAta)),
            DoSmart(() => Campo<int>.NaoInformado(FonteDado.Smart, "em disco SATA, a vida usada depende do fabricante; veja a tabela SMART")),
            DoSmart<IReadOnlyList<LinhaSmart>>(() => Campo<IReadOnlyList<LinhaSmart>>.Lido(LinhasAta(leitura, nomes), FonteDado.Smart, leitura.Observacao)),
            volumesTela);
    }

    private static Campo<long> Atributo(LeituraSmartAta leitura, byte id, ulong mascara) =>
        leitura.Atributos.FirstOrDefault(x => x.Id == id) is { } atributo
            ? Campo<long>.Lido((long)(atributo.Bruto & mascara), FonteDado.Smart)
            : Campo<long>.NaoInformado(FonteDado.Smart);

    private static IReadOnlyList<LinhaSmart> LinhasAta(LeituraSmartAta leitura, AtributosSmart nomes) =>
        leitura.Atributos.Select(x => new LinhaSmart(
            $"{x.Id:X2}h",
            nomes.Nome(x.Id),
            nomes.Buscar(x.Id)?.NomeOriginal ?? string.Empty,
            x.Atual,
            x.Pior,
            x.Limite,
            x.Bruto)).ToList();

    private static IReadOnlyList<LinhaSmart> LinhasNvme(SaudeNvme s) =>
    [
        Nvme("Alerta crítico", "Critical Warning", s.AlertaCritico),
        Nvme("Temperatura (°C)", "Composite Temperature", s.TemperaturaC ?? 0),
        Nvme("Reserva disponível (%)", "Available Spare", s.ReservaDisponivel),
        Nvme("Limite da reserva (%)", "Available Spare Threshold", s.LimiteReserva),
        Nvme("Vida usada (%)", "Percentage Used", s.PercentualUsado),
        Nvme("Dados lidos (bytes)", "Data Units Read", s.DadosLidosBytes),
        Nvme("Dados gravados (bytes)", "Data Units Written", s.DadosGravadosBytes),
        Nvme("Ciclos de energia", "Power Cycles", s.CiclosEnergia),
        Nvme("Horas ligado", "Power On Hours", s.HorasLigado),
        Nvme("Desligamentos inseguros", "Unsafe Shutdowns", s.DesligamentosInseguros),
        Nvme("Erros de mídia", "Media and Data Integrity Errors", s.ErrosMidia),
        Nvme("Entradas no registro de erros", "Number of Error Information Log Entries", s.EntradasRegistroErros),
    ];

    private static LinhaSmart Nvme(string nome, string original, decimal valor) => new(original, nome, original, null, null, null, valor);

    private static VolumeTela Volume(VolumeBruto v)
    {
        const FonteDado a = FonteDado.Armazenamento;
        return new VolumeTela(
            Campo<string>.Lido(v.Letra, a),
            Campo.Texto(v.Rotulo, a),
            Campo.Texto(v.SistemaArquivos, a),
            Campo.DeOpcional(v.LivreBytes, a),
            Campo.DeOpcional(v.TotalBytes, a),
            Volumes.NomeBitLocker(v.ProtecaoBitLocker) is { } b
                ? Campo<string>.Lido(b, FonteDado.Windows)
                : v.FalhaBitLocker == Volumes.RequerAdministrador
                    ? Campo<string>.RequerAdministrador(FonteDado.Windows)
                    : Campo<string>.NaoInformado(FonteDado.Windows, v.FalhaBitLocker));
    }
}
