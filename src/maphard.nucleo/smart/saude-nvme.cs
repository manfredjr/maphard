using System.Buffers.Binary;

namespace MapHard.Nucleo.Smart;

/// <summary>O log de saúde do NVMe (página 02h), já nas unidades da tela.</summary>
public sealed record SaudeNvme(
    byte AlertaCritico,
    int? TemperaturaC,
    int ReservaDisponivel,
    int LimiteReserva,
    int PercentualUsado,
    decimal DadosLidosBytes,
    decimal DadosGravadosBytes,
    decimal CiclosEnergia,
    decimal HorasLigado,
    decimal DesligamentosInseguros,
    decimal ErrosMidia,
    decimal EntradasRegistroErros,
    int? LimiteAvisoTemperaturaC)
{
    /// <summary>Os bits ligados do alerta crítico, em texto.</summary>
    public IReadOnlyList<string> Alertas => LeitorSaudeNvme.TextosAlerta(AlertaCritico);
}

/// <summary>
/// Interpreta o NVME_HEALTH_INFO_LOG (learn.microsoft.com, nvme.h, lido em 01/10/2026): CriticalWarning (0),
/// Temperature[2] (1), AvailableSpare (3), AvailableSpareThreshold (4), PercentageUsed (5), e contadores de
/// 16 bytes a partir de 32: DataUnitRead, DataUnitWritten, HostReadCommands, HostWrittenCommands,
/// ControllerBusyTime, PowerCycle (112), PowerOnHours (128), UnsafeShutdowns (144), MediaErrors (160),
/// ErrorInfoLogEntryCount (176). Temperatura em kelvin; dados em milhares de unidades de 512 bytes.
/// O WCTEMP vem do IDENTIFY do controlador, bytes 266 e 267 (nvme.h do SDK), na mesma unidade da temperatura.
/// </summary>
public static class LeitorSaudeNvme
{
    public const int TamanhoLog = 512;
    private const int TamanhoIdentificacao = 4096;
    private const int PosicaoWctemp = 266;
    private const decimal BytesPorUnidade = 512_000m;
    private const int Kelvin = 273;

    private static readonly string[] _alertas =
    [
        "reserva abaixo do limite",
        "temperatura fora da faixa",
        "confiabilidade degradada",
        "mídia só de leitura",
        "falha na memória de reserva volátil",
    ];

    public static SaudeNvme? Interpretar(byte[]? log, byte[]? identificacao = null)
    {
        if (log is null || log.Length < TamanhoLog)
        {
            return null;
        }

        var kelvin = BinaryPrimitives.ReadUInt16LittleEndian(log.AsSpan(1));
        return new SaudeNvme(
            log[0],
            Celsius(kelvin),
            log[3],
            log[4],
            log[5],
            Contador(log, 32) * BytesPorUnidade,
            Contador(log, 48) * BytesPorUnidade,
            Contador(log, 112),
            Contador(log, 128),
            Contador(log, 144),
            Contador(log, 160),
            Contador(log, 176),
            identificacao is { Length: >= TamanhoIdentificacao } ? Celsius(BinaryPrimitives.ReadUInt16LittleEndian(identificacao.AsSpan(PosicaoWctemp))) : null);
    }

    public static IReadOnlyList<string> TextosAlerta(byte alerta) =>
        Enumerable.Range(0, _alertas.Length).Where(bit => (alerta & (1 << bit)) != 0).Select(bit => _alertas[bit]).ToList();

    /// <summary>0 kelvin é "não informado", como no campo vazio do log.</summary>
    private static int? Celsius(ushort kelvin) => kelvin == 0 ? null : kelvin - Kelvin;

    /// <summary>Contador de 16 bytes em little-endian. Acima do que o decimal guarda, satura no máximo.</summary>
    internal static decimal Contador(byte[] log, int posicao)
    {
        var valor = BinaryPrimitives.ReadUInt128LittleEndian(log.AsSpan(posicao, 16));
        return valor > (UInt128)decimal.MaxValue ? decimal.MaxValue : (decimal)valor;
    }
}
