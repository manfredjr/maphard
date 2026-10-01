using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MapHard.Nucleo.Campos;

namespace MapHard.Nucleo.Processador;

/// <summary>Leituras de clock e de uso que vêm do Windows. Os testes trocam por valores fixos.</summary>
public interface IFonteClocks
{
    /// <summary>
    /// MaxMhz da CallNtPowerInformation(ProcessorInformation). Apesar do nome, é o clock nominal
    /// (o base), sem o turbo. É o mesmo valor que o msinfo32 mostra ao lado do nome do processador.
    /// </summary>
    int? ClockNominalWindowsMhz();

    /// <summary>
    /// Amostra de 1 segundo dos contadores <c>% Processor Performance</c> (total) e
    /// <c>% Processor Utility</c> (por processador lógico). Null quando os contadores falham.
    /// </summary>
    AmostraDesempenho? Amostrar();
}

public sealed record AmostraDesempenho(double DesempenhoPercentual, IReadOnlyList<double> UsoPorProcessador);

public sealed record ClocksCpu(Campo<int> Base, Campo<int> Maximo, Campo<int> Atual, Campo<double> UsoTotal, Campo<IReadOnlyList<double>> UsoPorProcessador);

/// <summary>
/// Junta as fontes de clock na ordem de confiança e calcula o clock atual.
/// Com o hipervisor do Windows ativo (segurança baseada em virtualização), a folha 0x16 do CPUID
/// volta zerada e as fontes reservas entram: o base vem do clock nominal do Windows e o máximo,
/// do SMBIOS. O <c>~MHz</c> do registro não entra: é uma medição, não o clock base.
/// </summary>
public static class CalculoClocks
{
    public static ClocksCpu Montar(int? baseCpuid, int? maximoCpuid, int? maximoSmbios, IFonteClocks fonte)
    {
        var baseMhz = baseCpuid is > 0
            ? Campo<int>.Lido(baseCpuid.Value, FonteDado.Cpuid)
            : fonte.ClockNominalWindowsMhz() is > 0 and var nominal
                ? Campo<int>.Lido(nominal, FonteDado.Windows)
                : Campo<int>.NaoInformado(FonteDado.Cpuid);

        // Há placa que grava no SMBIOS um máximo abaixo do base. Esse valor não serve.
        var maximo = maximoCpuid is > 0
            ? Campo<int>.Lido(maximoCpuid.Value, FonteDado.Cpuid)
            : maximoSmbios is not > 0
                ? Campo<int>.NaoInformado(FonteDado.Cpuid)
                : baseMhz.FoiLido && maximoSmbios < baseMhz.Valor
                    ? Campo<int>.NaoInformado(FonteDado.Smbios, $"o SMBIOS informa {maximoSmbios} MHz, abaixo do clock base")
                    : Campo<int>.Lido(maximoSmbios.Value, FonteDado.Smbios);

        var amostra = fonte.Amostrar();
        if (amostra is null)
        {
            const string motivo = "contador de desempenho do Windows indisponível";
            return new ClocksCpu(baseMhz, maximo, Campo<int>.Erro(FonteDado.Contador, motivo), Campo<double>.Erro(FonteDado.Contador, motivo),
                Campo<IReadOnlyList<double>>.Erro(FonteDado.Contador, motivo));
        }

        // O contador mede o desempenho em relação ao clock base: 125% sobre 3600 MHz dá 4500 MHz.
        var atual = baseMhz.FoiLido
            ? Campo<int>.Lido((int)Math.Round(baseMhz.Valor * amostra.DesempenhoPercentual / 100.0), FonteDado.Contador)
            : Campo<int>.NaoInformado(FonteDado.Contador, "sem o clock base para calcular");

        var uso = amostra.UsoPorProcessador;
        return new ClocksCpu(
            baseMhz,
            maximo,
            atual,
            uso.Count > 0 ? Campo<double>.Lido(uso.Average(), FonteDado.Contador) : Campo<double>.NaoInformado(FonteDado.Contador),
            uso.Count > 0 ? Campo<IReadOnlyList<double>>.Lido(uso, FonteDado.Contador) : Campo<IReadOnlyList<double>>.NaoInformado(FonteDado.Contador));
    }
}

/// <summary>
/// Leituras reais. Documentação: CallNtPowerInformation e PROCESSOR_POWER_INFORMATION
/// (MicrosoftDocs/sdk-api e MicrosoftDocs/win32), PdhAddEnglishCounterW, PdhCollectQueryData e
/// PdhGetFormattedCounterArrayW (MicrosoftDocs/sdk-api, pdh). PDH_FMT_DOUBLE e PDH_FMT_NOCAP100
/// conferidos no pdh.h do Wine. Os contadores entram pelo nome em inglês, que vale em qualquer idioma.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteClocksWindows : IFonteClocks
{
    private const int InformacaoProcessador = 11;
    private const int TamanhoInformacaoProcessador = 6 * 4;
    private const uint FormatoDouble = 0x00000200;
    private const uint SemTeto100 = 0x00008000;
    private const uint MaisDados = 0x800007D2;

    private const string CaminhoDesempenho = @"\Processor Information(_Total)\% Processor Performance";
    private const string CaminhoUso = @"\Processor Information(*)\% Processor Utility";

    public int? ClockNominalWindowsMhz()
    {
        var quantidade = Environment.ProcessorCount;
        var buffer = new byte[quantidade * TamanhoInformacaoProcessador];
        if (CallNtPowerInformation(InformacaoProcessador, IntPtr.Zero, 0, buffer, (uint)buffer.Length) != 0)
        {
            return null;
        }

        // MaxMhz é o segundo ULONG de cada PROCESSOR_POWER_INFORMATION.
        var maximo = Enumerable.Range(0, quantidade).Max(i => BitConverter.ToUInt32(buffer, (i * TamanhoInformacaoProcessador) + 4));
        return maximo > 0 ? (int)maximo : null;
    }

    public AmostraDesempenho? Amostrar()
    {
        if (PdhOpenQueryW(null, IntPtr.Zero, out var consulta) != 0)
        {
            return null;
        }

        try
        {
            if (PdhAddEnglishCounterW(consulta, CaminhoDesempenho, IntPtr.Zero, out var desempenho) != 0
                || PdhAddEnglishCounterW(consulta, CaminhoUso, IntPtr.Zero, out var uso) != 0
                || PdhCollectQueryData(consulta) != 0)
            {
                return null;
            }

            Thread.Sleep(1000);
            if (PdhCollectQueryData(consulta) != 0)
            {
                return null;
            }

            var valores = LerArray(desempenho, FormatoDouble | SemTeto100);
            var usos = LerArray(uso, FormatoDouble);
            if (valores is null || usos is null || valores.Count == 0)
            {
                return null;
            }

            var porProcessador = usos
                .Where(v => !v.Nome.Contains("_Total", StringComparison.Ordinal))
                .OrderBy(v => OrdemInstancia(v.Nome))
                .Select(v => Math.Clamp(v.Valor, 0, 100))
                .ToList();
            return new AmostraDesempenho(valores[0].Valor, porProcessador);
        }
        finally
        {
            _ = PdhCloseQuery(consulta);
        }
    }

    /// <summary>Instância "grupo,número" do contador: ordena pelo grupo e depois pelo número.</summary>
    private static (int, int) OrdemInstancia(string nome)
    {
        var partes = nome.Split(',');
        return partes.Length == 2 && int.TryParse(partes[0], out var grupo) && int.TryParse(partes[1], out var numero) ? (grupo, numero) : (int.MaxValue, 0);
    }

    /// <summary>Lê o array de PDH_FMT_COUNTERVALUE_ITEM_W: ponteiro do nome (8 bytes) e o valor (status de 4 bytes, alinhamento, double).</summary>
    private static List<(string Nome, double Valor)>? LerArray(IntPtr contador, uint formato)
    {
        uint tamanho = 0;
        uint quantidade = 0;
        if (PdhGetFormattedCounterArrayW(contador, formato, ref tamanho, ref quantidade, IntPtr.Zero) != MaisDados)
        {
            return null;
        }

        var memoria = Marshal.AllocHGlobal((int)tamanho);
        try
        {
            if (PdhGetFormattedCounterArrayW(contador, formato, ref tamanho, ref quantidade, memoria) != 0)
            {
                return null;
            }

            const int tamanhoItem = 24;
            var itens = new List<(string, double)>();
            for (var i = 0; i < quantidade; i++)
            {
                var item = memoria + (i * tamanhoItem);
                var nome = Marshal.PtrToStringUni(Marshal.ReadIntPtr(item)) ?? string.Empty;
                var status = (uint)Marshal.ReadInt32(item + 8);
                if (status > 1)
                {
                    continue;
                }

                itens.Add((nome, BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item + 16))));
            }

            return itens;
        }
        finally
        {
            Marshal.FreeHGlobal(memoria);
        }
    }

    [LibraryImport("powrprof.dll")]
    private static partial uint CallNtPowerInformation(int nivel, IntPtr entrada, uint tamanhoEntrada, byte[] saida, uint tamanhoSaida);

    [LibraryImport("pdh.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint PdhOpenQueryW(string? origem, IntPtr usuario, out IntPtr consulta);

    [LibraryImport("pdh.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint PdhAddEnglishCounterW(IntPtr consulta, string caminho, IntPtr usuario, out IntPtr contador);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhCollectQueryData(IntPtr consulta);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhGetFormattedCounterArrayW(IntPtr contador, uint formato, ref uint tamanho, ref uint quantidade, IntPtr itens);

    [LibraryImport("pdh.dll")]
    private static partial uint PdhCloseQuery(IntPtr consulta);
}
