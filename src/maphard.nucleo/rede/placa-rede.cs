using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using MapHard.Nucleo.Campos;

namespace MapHard.Nucleo.Rede;

/// <summary>Uma placa de rede física (R36). Só a lista: o detalhe de rede é assunto do MapNet.</summary>
public sealed record PlacaRede(
    Campo<string> Nome,
    Campo<string> Descricao,
    Campo<string> Mac,
    Campo<string> Tipo,
    Campo<long> VelocidadeBps,
    Campo<bool> Conectada);

/// <summary>
/// Interpreta cada MIB_IF_ROW2 (netioapi.h, página "MIB_IF_ROW2") de 64 bits, com IF_MAX_STRING_SIZE 256 e
/// IF_MAX_PHYS_ADDRESS_LENGTH 32 do ifdef.h: Alias em 28 e Description em 542 (257 caracteres cada),
/// PhysicalAddressLength em 1056, PhysicalAddress em 1060, Type em 1128, PhysicalMediumType em 1140, os sinais
/// InterfaceAndOperStatusFlags em 1152 (bit 0 HardwareInterface, bit 1 FilterInterface), MediaConnectState em 1164
/// e ReceiveLinkSpeed em 1200; 1352 bytes ao todo. Placa física é a que tem HardwareInterface e não é filtro.
/// </summary>
public static class LeitorRede
{
    public const int TamanhoLinha = 1352;

    private const int TipoEthernet = 6;
    private const int TipoWifi = 71;
    private const int MeioBluetooth = 10;
    private const int Conectado = 1;
    private const byte Hardware = 0x01;
    private const byte Filtro = 0x02;
    private const int CaracteresTexto = 257;

    public static IReadOnlyList<PlacaRede> Montar(IEnumerable<byte[]> linhas) =>
        linhas.Select(Interpretar).OfType<PlacaRede>().ToList();

    /// <summary>Nulo para adaptador virtual, filtro ou linha curta.</summary>
    public static PlacaRede? Interpretar(byte[]? linha)
    {
        if (linha is null || linha.Length < TamanhoLinha)
        {
            return null;
        }

        var sinais = linha[1152];
        if ((sinais & Hardware) == 0 || (sinais & Filtro) != 0)
        {
            return null;
        }

        const FonteDado f = FonteDado.Windows;
        var tamanhoMac = BinaryPrimitives.ReadUInt32LittleEndian(linha.AsSpan(1056));
        var tipo = BinaryPrimitives.ReadInt32LittleEndian(linha.AsSpan(1128));
        var meio = BinaryPrimitives.ReadInt32LittleEndian(linha.AsSpan(1140));
        var conectada = BinaryPrimitives.ReadInt32LittleEndian(linha.AsSpan(1164)) == Conectado;
        var velocidade = BinaryPrimitives.ReadUInt64LittleEndian(linha.AsSpan(1200));

        return new PlacaRede(
            Campo.Texto(Texto(linha.AsSpan(28)), f),
            Campo.Texto(Texto(linha.AsSpan(542)), f),
            tamanhoMac is 0 or > 32 ? Campo<string>.NaoInformado(f) : Campo<string>.Lido(Convert.ToHexString(linha, 1060, (int)tamanhoMac).Chunk(2).Select(c => new string(c)).Aggregate((a, b) => $"{a}-{b}"), f),
            Campo<string>.Lido(meio == MeioBluetooth ? "Bluetooth" : tipo switch
            {
                TipoEthernet => "Ethernet",
                TipoWifi => "Wi-Fi",
                _ => $"outro (tipo {tipo})",
            }, f),
            !conectada ? Campo<long>.NaoInformado(f, "desconectada")
                : velocidade is 0 or > long.MaxValue ? Campo<long>.NaoInformado(f)
                : Campo<long>.Lido((long)velocidade, f, "de recepção; no Wi-Fi a de envio pode ser outra"),
            Campo<bool>.Lido(conectada, f));
    }

    /// <summary>"1 Gb/s", "721 Mb/s", "2,5 Gb/s".</summary>
    public static string TextoVelocidade(long bps)
    {
        var br = CultureInfo.GetCultureInfo("pt-BR");
        return bps >= 1_000_000_000 ? $"{(bps / 1e9).ToString("0.#", br)} Gb/s"
            : bps >= 1_000_000 ? $"{(bps / 1e6).ToString("0.#", br)} Mb/s"
            : $"{(bps / 1e3).ToString("0.#", br)} kb/s";
    }

    private static string Texto(ReadOnlySpan<byte> bytes)
    {
        var texto = Encoding.Unicode.GetString(bytes[..(CaracteresTexto * 2)]);
        var fim = texto.IndexOf('\0', StringComparison.Ordinal);
        return fim < 0 ? texto : texto[..fim];
    }
}
