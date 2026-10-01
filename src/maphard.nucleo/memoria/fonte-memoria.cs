using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapHard.Nucleo.Memoria;

/// <summary>Uso da memória pelo Windows, em bytes.</summary>
public sealed record EstadoMemoriaWindows(
    long TotalBytes,
    long DisponivelBytes,
    int CargaPercentual,
    long? ConfirmadaBytes,
    long? LimiteConfirmadaBytes,
    long? CacheBytes);

/// <summary>Leituras de memória que vêm do Windows. Os testes trocam por valores fixos.</summary>
public interface IFonteMemoria
{
    /// <summary>GetPhysicallyInstalledSystemMemory, em KB. Null quando a função falha.</summary>
    long? InstaladaKb();

    /// <summary>GlobalMemoryStatusEx e GetPerformanceInfo. Null quando o GlobalMemoryStatusEx falha.</summary>
    EstadoMemoriaWindows? Estado();
}

/// <summary>
/// Leituras reais, sem administrador. Fontes (learn.microsoft.com, conferidas em 01/10/2026):
/// GetPhysicallyInstalledSystemMemory (sysinfoapi), que lê o SMBIOS e falha com ERROR_INVALID_DATA
/// quando o valor fica abaixo do utilizável; MEMORYSTATUSEX (sysinfoapi), com dwLength preenchido
/// antes da chamada; e PERFORMANCE_INFORMATION (psapi), com CommitTotal, CommitLimit e SystemCache
/// em páginas e PageSize em bytes. A própria documentação do MEMORYSTATUSEX manda usar o
/// GetPerformanceInfo para o limite de memória confirmada de todo o sistema.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteMemoriaWindows : IFonteMemoria
{
    public long? InstaladaKb() =>
        GetPhysicallyInstalledSystemMemory(out var kb) && kb > 0 && kb <= long.MaxValue ? (long)kb : null;

    public EstadoMemoriaWindows? Estado()
    {
        var status = new StatusMemoria { Tamanho = (uint)Marshal.SizeOf<StatusMemoria>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return null;
        }

        var desempenho = new InformacaoDesempenho { Tamanho = (uint)Marshal.SizeOf<InformacaoDesempenho>() };
        var temDesempenho = GetPerformanceInfo(ref desempenho, desempenho.Tamanho) && desempenho.TamanhoPagina > 0;
        long? EmBytes(nuint paginas) => temDesempenho ? (long)paginas * (long)desempenho.TamanhoPagina : null;

        return new EstadoMemoriaWindows(
            (long)status.TotalFisica,
            (long)status.DisponivelFisica,
            (int)status.Carga,
            EmBytes(desempenho.ConfirmadaTotal),
            EmBytes(desempenho.LimiteConfirmada),
            EmBytes(desempenho.CacheSistema));
    }

    // MEMORYSTATUSEX, na ordem da documentação.
    [StructLayout(LayoutKind.Sequential)]
    private struct StatusMemoria
    {
        public uint Tamanho;
        public uint Carga;
        public ulong TotalFisica;
        public ulong DisponivelFisica;
        public ulong TotalArquivoPaginacao;
        public ulong DisponivelArquivoPaginacao;
        public ulong TotalVirtual;
        public ulong DisponivelVirtual;
        public ulong DisponivelVirtualEstendida;
    }

    // PERFORMANCE_INFORMATION, na ordem da documentação. SIZE_T é nuint.
    [StructLayout(LayoutKind.Sequential)]
    private struct InformacaoDesempenho
    {
        public uint Tamanho;
        public nuint ConfirmadaTotal;
        public nuint LimiteConfirmada;
        public nuint PicoConfirmada;
        public nuint TotalFisica;
        public nuint DisponivelFisica;
        public nuint CacheSistema;
        public nuint KernelTotal;
        public nuint KernelPaginado;
        public nuint KernelNaoPaginado;
        public nuint TamanhoPagina;
        public uint Identificadores;
        public uint Processos;
        public uint Threads;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetPhysicallyInstalledSystemMemory(out ulong kb);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref StatusMemoria status);

    [LibraryImport("psapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetPerformanceInfo(ref InformacaoDesempenho informacao, uint tamanho);
}
