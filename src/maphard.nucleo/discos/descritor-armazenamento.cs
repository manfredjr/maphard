using System.Buffers.Binary;
using System.Text;

namespace MapHard.Nucleo.Discos;

/// <summary>Tipo do disco, para a tela e para as regras de saúde.</summary>
public enum TipoDisco
{
    Hdd,
    SsdSata,
    SsdNvme,
    Usb,
    Raid,
    Outro,
}

/// <summary>O que a leitura real devolve de cada disco, antes da interpretação. Tudo o que falhou fica nulo.</summary>
public sealed record DiscoBruto(
    int Numero,
    byte[]? Descritor,
    bool? PenalidadeBusca,
    bool? Trim,
    long? TamanhoBytes,
    int? EstiloParticao,
    uint? VelocidadeLinkPcie,
    uint? LarguraLinkPcie);

/// <summary>Os textos e o barramento do STORAGE_DEVICE_DESCRIPTOR.</summary>
public sealed record DadosDescritor(string? Modelo, string? Firmware, string? NumeroSerie, int Barramento, bool Removivel);

/// <summary>
/// Interpreta o STORAGE_DEVICE_DESCRIPTOR (learn.microsoft.com, winioctl.h, lido em 01/10/2026): Version (0),
/// Size (4), DeviceType (8), DeviceTypeModifier (9), RemovableMedia (10), CommandQueueing (11), e os
/// deslocamentos de 4 bytes dos textos de fabricante (12), produto (16), revisão (20) e número de série (24),
/// contados do início da estrutura, com 0 para "sem texto"; BusType (28). Valores do STORAGE_BUS_TYPE
/// pela ordem do ntddstor.h do SDK.
/// </summary>
public static class DescritorArmazenamento
{
    public const int BarramentoAta = 3;
    public const int BarramentoUsb = 7;
    public const int BarramentoRaid = 8;
    public const int BarramentoSas = 10;
    public const int BarramentoSata = 11;
    public const int BarramentoNvme = 17;

    private const int TamanhoMinimo = 32;

    public static DadosDescritor? Interpretar(byte[]? bruto)
    {
        if (bruto is null || bruto.Length < TamanhoMinimo)
        {
            return null;
        }

        var fabricante = Texto(bruto, 12);
        var produto = Texto(bruto, 16);
        var modelo = string.Join(" ", new[] { fabricante, produto }.OfType<string>());
        return new DadosDescritor(
            modelo.Length == 0 ? null : modelo,
            Texto(bruto, 20),
            Texto(bruto, 24),
            BinaryPrimitives.ReadInt32LittleEndian(bruto.AsSpan(28)),
            bruto[10] != 0);
    }

    /// <summary>
    /// USB e RAID valem pelo barramento, mesmo com o disco por trás sendo SSD ou HDD. NVMe é sempre SSD.
    /// Nos outros, a penalidade de busca separa HDD de SSD; sem ela, o tipo fica "outro".
    /// </summary>
    public static TipoDisco Classificar(int barramento, bool? penalidadeBusca) => barramento switch
    {
        BarramentoUsb => TipoDisco.Usb,
        BarramentoRaid => TipoDisco.Raid,
        BarramentoNvme => TipoDisco.SsdNvme,
        BarramentoAta or BarramentoSata or BarramentoSas => penalidadeBusca switch
        {
            true => TipoDisco.Hdd,
            false => TipoDisco.SsdSata,
            null => TipoDisco.Outro,
        },
        _ => TipoDisco.Outro,
    };

    public static string NomeTipo(TipoDisco tipo) => tipo switch
    {
        TipoDisco.Hdd => "HDD",
        TipoDisco.SsdSata => "SSD SATA",
        TipoDisco.SsdNvme => "SSD NVMe",
        TipoDisco.Usb => "USB",
        TipoDisco.Raid => "RAID",
        _ => "outro",
    };

    public static string? NomeBarramento(int barramento) => barramento switch
    {
        BarramentoAta => "ATA",
        BarramentoUsb => "USB",
        BarramentoRaid => "RAID",
        BarramentoSas => "SAS",
        BarramentoSata => "SATA",
        BarramentoNvme => "NVMe",
        _ => null,
    };

    /// <summary>
    /// "PCIe 4.0 x4". A velocidade segue o campo Current Link Speed do PCIe: 1 é 2,5 GT/s e 2 é 5 GT/s
    /// (pciprop.h do SDK); 3 a 6 são 8, 16, 32 e 64 GT/s (include/uapi/linux/pci_regs.h, PCI_EXP_LNKSTA_CLS_*),
    /// que correspondem às gerações 1 a 6.
    /// </summary>
    public static string? LinkPcie(uint? velocidade, uint? largura)
    {
        if (velocidade is not (>= 1 and <= 6))
        {
            return null;
        }

        return largura is > 0 ? $"PCIe {velocidade}.0 x{largura}" : $"PCIe {velocidade}.0";
    }

    /// <summary>"GPT", "MBR" ou "sem partição". PARTITION_STYLE pela página do DRIVE_LAYOUT_INFORMATION_EX.</summary>
    public static string? NomeEstiloParticao(int? estilo) => estilo switch
    {
        0 => "MBR",
        1 => "GPT",
        2 => "sem partição",
        _ => null,
    };

    /// <summary>Texto ASCII terminado em zero no deslocamento guardado em <paramref name="posicao"/>. Fora do buffer, nulo.</summary>
    private static string? Texto(byte[] bruto, int posicao)
    {
        var inicio = BinaryPrimitives.ReadInt32LittleEndian(bruto.AsSpan(posicao));
        if (inicio <= 0 || inicio >= bruto.Length)
        {
            return null;
        }

        var fim = Array.IndexOf(bruto, (byte)0, inicio);
        var texto = new StringBuilder();
        foreach (var b in bruto.AsSpan(inicio, (fim < 0 ? bruto.Length : fim) - inicio))
        {
            texto.Append(b is >= 32 and < 127 ? (char)b : '.');
        }

        var limpo = texto.ToString().Trim();
        return limpo.Length == 0 ? null : limpo;
    }
}
