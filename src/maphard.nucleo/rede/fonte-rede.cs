using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapHard.Nucleo.Rede;

/// <summary>Leitura das interfaces de rede. Os testes trocam por linhas montadas à mão.</summary>
public interface IFonteRede
{
    /// <summary>Cada MIB_IF_ROW2, crua.</summary>
    IReadOnlyList<byte[]> Interfaces();
}

/// <summary>
/// Leitura real, sem administrador, pelo GetIfTable2 do iphlpapi (netioapi.h): MIB_IF_TABLE2 com NumEntries em 0 e
/// as linhas a partir do byte 8, cada uma com 1352 bytes. A tabela é devolvida com FreeMibTable.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteRedeWindows : IFonteRede
{
    private const int InicioLinhas = 8;

    public IReadOnlyList<byte[]> Interfaces()
    {
        var erro = GetIfTable2(out var tabela);
        if (erro != 0)
        {
            throw new InvalidOperationException($"a tabela de interfaces não abriu (erro {erro})");
        }

        try
        {
            var quantidade = Marshal.ReadInt32(tabela);
            var linhas = new List<byte[]>(quantidade);
            for (var i = 0; i < quantidade; i++)
            {
                var linha = new byte[LeitorRede.TamanhoLinha];
                Marshal.Copy(tabela + InicioLinhas + (i * LeitorRede.TamanhoLinha), linha, 0, linha.Length);
                linhas.Add(linha);
            }

            return linhas;
        }
        finally
        {
            FreeMibTable(tabela);
        }
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial uint GetIfTable2(out nint tabela);

    [LibraryImport("iphlpapi.dll")]
    private static partial void FreeMibTable(nint memoria);
}
