using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapHard.Nucleo.Processador;

/// <summary>
/// Um grupo de cache: tamanho de cada unidade e quantas unidades existem. No processador híbrido,
/// o mesmo nível aparece em mais de um grupo, um por tamanho.
/// </summary>
public sealed record CacheCpu(int Nivel, TipoCache Tipo, long TamanhoPorUnidade, int Unidades, int? Associatividade, int TamanhoLinha)
{
    public long TamanhoTotal => TamanhoPorUnidade * Unidades;

    /// <summary>Rótulo para a tela: "L1 de dados", "L1 de instruções", "L2", "L3".</summary>
    public string Rotulo => Tipo switch
    {
        TipoCache.Dados => $"L{Nivel} de dados",
        TipoCache.Instrucoes => $"L{Nivel} de instruções",
        _ => $"L{Nivel}",
    };
}

/// <summary>PROCESSOR_CACHE_TYPE, na ordem da documentação: CacheUnified, CacheInstruction, CacheData, CacheTrace.</summary>
public enum TipoCache
{
    Unificado = 0,
    Instrucoes = 1,
    Dados = 2,
    Rastreamento = 3,
}

/// <summary>Núcleos, threads e caches do processador, como o Windows os vê.</summary>
public sealed record TopologiaCpu(int Pacotes, int Nucleos, int Threads, int NucleosDesempenho, int NucleosEficiencia, IReadOnlyList<CacheCpu> Caches)
{
    public bool Hibrido => NucleosEficiencia > 0;
}

/// <summary>De onde vem o buffer do GetLogicalProcessorInformationEx. Os testes trocam por um buffer montado à mão.</summary>
public interface IFonteTopologia
{
    byte[]? LerBruta();
}

/// <summary>
/// Interpreta o buffer do <c>GetLogicalProcessorInformationEx(RelationAll)</c>: registros
/// SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX de tamanho variável, com o tipo (4 bytes), o tamanho
/// (4 bytes) e, a partir do deslocamento 8, a estrutura do tipo.
/// Ordem e tipos dos campos pela documentação da Microsoft (MicrosoftDocs/sdk-api, winnt):
/// PROCESSOR_RELATIONSHIP (Flags, EfficiencyClass, Reserved[20], GroupCount, GroupMask[]) e
/// CACHE_RELATIONSHIP (Level, Associativity, LineSize, CacheSize, Type, Reserved, GroupMask).
/// </summary>
public static class LeitorTopologia
{
    // LOGICAL_PROCESSOR_RELATIONSHIP, na ordem da documentação.
    internal const int RelacaoNucleo = 0;
    internal const int RelacaoCache = 2;
    internal const int RelacaoPacote = 3;

    private const int InicioUniao = 8;

    // Deslocamentos dentro da união.
    private const int NucleoEficiencia = 1;
    private const int NucleoQuantidadeGrupos = 22;
    private const int NucleoMascaras = 24;
    private const int CacheNivel = 0;
    private const int CacheAssociatividade = 1;
    private const int CacheLinha = 2;
    private const int CacheTamanho = 4;
    private const int CacheTipo = 8;

    // GROUP_AFFINITY em 64 bits: máscara de 8 bytes, grupo de 2 bytes e 6 reservados.
    private const int TamanhoAfinidade = 16;
    private const byte CacheTotalmenteAssociativo = 0xFF;

    public static TopologiaCpu? Interpretar(byte[]? bruta)
    {
        if (bruta is null || bruta.Length < InicioUniao)
        {
            return null;
        }

        var pacotes = 0;
        var nucleos = new List<(int Eficiencia, int Threads)>();
        var caches = new List<(int Nivel, TipoCache Tipo, long Tamanho, int? Associatividade, int Linha)>();

        var posicao = 0;
        while (posicao + InicioUniao <= bruta.Length)
        {
            var relacao = BinaryPrimitives.ReadInt32LittleEndian(bruta.AsSpan(posicao));
            var tamanho = BinaryPrimitives.ReadInt32LittleEndian(bruta.AsSpan(posicao + 4));
            if (tamanho < InicioUniao || posicao + tamanho > bruta.Length)
            {
                break;
            }

            var registro = bruta.AsSpan(posicao + InicioUniao, tamanho - InicioUniao);
            switch (relacao)
            {
                case RelacaoNucleo when registro.Length >= NucleoMascaras:
                    nucleos.Add((registro[NucleoEficiencia], ContarThreads(registro)));
                    break;
                case RelacaoPacote:
                    pacotes++;
                    break;
                case RelacaoCache when registro.Length >= CacheTipo + 4:
                    var associatividade = registro[CacheAssociatividade];
                    caches.Add((
                        registro[CacheNivel],
                        (TipoCache)BinaryPrimitives.ReadInt32LittleEndian(registro[CacheTipo..]),
                        BinaryPrimitives.ReadUInt32LittleEndian(registro[CacheTamanho..]),
                        associatividade == CacheTotalmenteAssociativo ? null : associatividade,
                        BinaryPrimitives.ReadUInt16LittleEndian(registro[CacheLinha..])));
                    break;
            }

            posicao += tamanho;
        }

        if (nucleos.Count == 0)
        {
            return null;
        }

        // EfficiencyClass só é diferente de zero em processador com núcleos de tipos diferentes.
        // A classe mais alta é a de desempenho.
        var classeMaior = nucleos.Max(n => n.Eficiencia);
        var desempenho = nucleos.Count(n => n.Eficiencia == classeMaior);

        // No processador híbrido, o mesmo nível tem tamanhos diferentes nos núcleos de desempenho e
        // nos de eficiência. Cada tamanho vira um grupo, na ordem em que o Windows lista os núcleos.
        var agrupados = caches
            .GroupBy(c => c)
            .OrderBy(g => g.Key.Nivel)
            .ThenByDescending(g => g.Key.Tipo)
            .Select(g => new CacheCpu(g.Key.Nivel, g.Key.Tipo, g.Key.Tamanho, g.Count(), g.Key.Associatividade, g.Key.Linha))
            .ToList();

        return new TopologiaCpu(
            Math.Max(pacotes, 1),
            nucleos.Count,
            nucleos.Sum(n => n.Threads),
            desempenho,
            nucleos.Count - desempenho,
            agrupados);
    }

    private static int ContarThreads(ReadOnlySpan<byte> registro)
    {
        var grupos = BinaryPrimitives.ReadUInt16LittleEndian(registro[NucleoQuantidadeGrupos..]);
        var threads = 0;
        for (var i = 0; i < grupos; i++)
        {
            var inicio = NucleoMascaras + (i * TamanhoAfinidade);
            if (inicio + 8 > registro.Length)
            {
                break;
            }

            threads += BitOperations.PopCount(BinaryPrimitives.ReadUInt64LittleEndian(registro[inicio..]));
        }

        return Math.Max(threads, 1);
    }
}

/// <summary>Lê o buffer pelo GetLogicalProcessorInformationEx, sem administrador.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteTopologiaWindows : IFonteTopologia
{
    private const int RelacaoTodas = 0xFFFF;
    private const int ErroBufferInsuficiente = 122;

    public byte[]? LerBruta()
    {
        uint tamanho = 0;
        if (GetLogicalProcessorInformationEx(RelacaoTodas, null, ref tamanho) || Marshal.GetLastPInvokeError() != ErroBufferInsuficiente)
        {
            return null;
        }

        var buffer = new byte[tamanho];
        return GetLogicalProcessorInformationEx(RelacaoTodas, buffer, ref tamanho) ? buffer[..(int)tamanho] : null;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLogicalProcessorInformationEx(int relacao, byte[]? buffer, ref uint tamanho);
}
