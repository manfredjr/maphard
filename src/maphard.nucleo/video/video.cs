using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Dispositivos;
using MapHard.Nucleo.Tabelas;

namespace MapHard.Nucleo.Video;

/// <summary>Uma placa de vídeo (R31). A memória dedicada vem da DXGI, sem o limite de 4 GB do WMI.</summary>
public sealed record PlacaVideo(
    Campo<string> Nome,
    Campo<string> Fabricante,
    Campo<long> MemoriaDedicada,
    Campo<long> MemoriaCompartilhada,
    Campo<string> VersaoDriver,
    Campo<DateOnly> DataDriver);

/// <summary>Os campos do DXGI_ADAPTER_DESC1 que o MapHard usa.</summary>
public sealed record AdaptadorDxgi(string Nome, uint Fabricante, uint Dispositivo, long Dedicada, long Compartilhada, bool Software);

/// <summary>
/// Interpreta o DXGI_ADAPTER_DESC1 (learn.microsoft.com, dxgi.h) em 64 bits: Description WCHAR[128] (0 a 255),
/// VendorId (256), DeviceId (260), SubSysId (264), Revision (268), DedicatedVideoMemory (272),
/// DedicatedSystemMemory (280), SharedSystemMemory (288), AdapterLuid (296) e Flags (304). Memórias em bytes;
/// DXGI_ADAPTER_FLAG_SOFTWARE é 2. VendorId acima de 0xFFFF é ID ACPI, não PCI.
/// </summary>
public static partial class LeitorVideo
{
    public const int TamanhoDescricao = 312;
    private const uint Software = 2;

    public static AdaptadorDxgi? Interpretar(byte[]? d)
    {
        if (d is null || d.Length < 308)
        {
            return null;
        }

        var nome = Encoding.Unicode.GetString(d, 0, 256);
        var fim = nome.IndexOf('\0', StringComparison.Ordinal);
        return new AdaptadorDxgi(
            (fim < 0 ? nome : nome[..fim]).Trim(),
            BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(256)),
            BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(260)),
            (long)BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(272)),
            (long)BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(288)),
            (BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(304)) & Software) != 0);
    }

    /// <summary>
    /// Uma placa por adaptador de hardware; o de software fica de fora. Fabricante pela tabela PCI; versão e data
    /// do driver pelo dispositivo da SetupAPI com o mesmo VEN e DEV nos IDs de hardware.
    /// </summary>
    public static IReadOnlyList<PlacaVideo> Montar(IReadOnlyList<byte[]> descricoes, IReadOnlyList<DispositivoBruto>? dispositivos, FabricantesPci fabricantes)
    {
        const FonteDado f = FonteDado.Windows;
        return descricoes.Select(Interpretar).OfType<AdaptadorDxgi>().Where(a => !a.Software).Select(a =>
        {
            var pci = a.Fabricante <= 0xFFFF;
            var dispositivo = pci ? dispositivos?.FirstOrDefault(x => x.IdsHardware.Any(id => Casa(id, a.Fabricante, a.Dispositivo))) : null;
            var naoAchado = dispositivos is null ? "lista de dispositivos indisponível" : "dispositivo não achado na SetupAPI";
            return new PlacaVideo(
                Campo.Texto(a.Nome, f),
                pci && fabricantes.Nome(a.Fabricante) is { } nome
                    ? Campo<string>.Lido(nome, FonteDado.Tabela, $"fabricante PCI {a.Fabricante:X4}")
                    : Campo<string>.NaoInformado(FonteDado.Tabela, pci ? $"fabricante PCI {a.Fabricante:X4} fora da tabela do MapHard" : "ID ACPI, não PCI"),
                a.Dedicada > 0 ? Campo<long>.Lido(a.Dedicada, f) : Campo<long>.NaoInformado(f, "sem memória dedicada (vídeo integrado usa a memória do sistema)"),
                a.Compartilhada > 0 ? Campo<long>.Lido(a.Compartilhada, f) : Campo<long>.NaoInformado(f),
                dispositivo?.VersaoDriver is { } versao ? Campo<string>.Lido(versao, f) : Campo<string>.NaoInformado(f, naoAchado),
                dispositivo?.DataDriver is { } data ? Campo<DateOnly>.Lido(data, f) : Campo<DateOnly>.NaoInformado(f, naoAchado));
        }).ToList();
    }

    private static bool Casa(string id, uint fabricante, uint dispositivo) =>
        IdPci().Match(id) is { Success: true } m
        && Convert.ToUInt32(m.Groups[1].Value, 16) == fabricante
        && Convert.ToUInt32(m.Groups[2].Value, 16) == dispositivo;

    [GeneratedRegex(@"^PCI\\VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex IdPci();
}
