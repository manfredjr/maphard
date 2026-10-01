using System.Buffers.Binary;

namespace MapHard.Nucleo.Smart;

/// <summary>Um atributo SMART ATA com o limite do fabricante, quando a página de limites traz o mesmo id.</summary>
public sealed record AtributoSmart(byte Id, ushort Flags, byte Atual, byte Pior, byte? Limite, ulong Bruto);

/// <summary>O que a leitura real devolve de um disco ATA: os quatro blocos de 512 bytes e os registradores do status.</summary>
public sealed record SmartAtaBruto(byte[]? Identificacao, byte[]? Valores, byte[]? Limites, byte? CilindroBaixo, byte? CilindroAlto, string? Falha);

/// <summary>A leitura interpretada. <see cref="Falha"/> traz o motivo quando o SMART não foi lido.</summary>
public sealed record LeituraSmartAta(
    IReadOnlyList<AtributoSmart> Atributos,
    bool? FalhaPrevista,
    int? RotacaoRpm,
    bool? Ssd,
    string? VelocidadeSata,
    string? Observacao,
    string? Falha);

/// <summary>Os únicos comandos ATA que o MapHard envia. Nenhum grava no disco nem muda configuração.</summary>
public enum ComandoAta
{
    Identificar,
    LerValores,
    LerLimites,
    LerStatus,
}

/// <summary>
/// Registradores de cada comando, pelo ntdddisk.h do SDK: ID_CMD 0xEC, SMART_CMD 0xB0, READ_ATTRIBUTES 0xD0,
/// READ_THRESHOLDS 0xD1, RETURN_SMART_STATUS 0xDA, SMART_CYL_LOW 0x4F e SMART_CYL_HI 0xC2.
/// </summary>
public static class ComandosAta
{
    public const byte Identify = 0xEC;
    public const byte Smart = 0xB0;
    public const byte CilindroBaixoSmart = 0x4F;
    public const byte CilindroAltoSmart = 0xC2;

    public static IReadOnlyList<ComandoAta> Permitidos { get; } = [ComandoAta.Identificar, ComandoAta.LerValores, ComandoAta.LerLimites, ComandoAta.LerStatus];

    /// <summary>CurrentTaskFile do ATA_PASS_THROUGH_EX: Features, SectorCount, SectorNumber, CylLow, CylHigh, Device/Head, Command, Reserved.</summary>
    public static byte[] Registradores(ComandoAta comando) => comando switch
    {
        ComandoAta.Identificar => [0, 1, 0, 0, 0, 0, Identify, 0],
        ComandoAta.LerValores => [0xD0, 1, 0, CilindroBaixoSmart, CilindroAltoSmart, 0, Smart, 0],
        ComandoAta.LerLimites => [0xD1, 1, 0, CilindroBaixoSmart, CilindroAltoSmart, 0, Smart, 0],
        ComandoAta.LerStatus => [0xDA, 0, 0, CilindroBaixoSmart, CilindroAltoSmart, 0, Smart, 0],
        _ => throw new ArgumentOutOfRangeException(nameof(comando)),
    };

    public static bool TemDados(ComandoAta comando) => comando != ComandoAta.LerStatus;
}

/// <summary>
/// Interpreta o SMART ATA. Formato dos blocos pelo smartmontools (include/smartmon/ata.h, GPL-2.0): valores com
/// a revisão (2 bytes) e 30 atributos de 12 bytes (id, flags de 2 bytes, atual, pior, bruto de 6 bytes,
/// reservado); limites com a revisão e 30 entradas de 12 bytes (id, limite, 10 reservados). Soma de verificação:
/// os 512 bytes somam zero (ata_checksum). Status: 0x4F/0xC2 bom, 0xF4/0x2C falha prevista (ata_get_smart_status).
/// IDENTIFY: palavra 77, bits 1 a 3, velocidade atual SATA (ataprint.cpp); palavra 217, rotação (ata_get_rotation_rate).
/// </summary>
public static class LeitorSmartAta
{
    private const int Tamanho = 512;
    private const int Itens = 30;
    private const int TamanhoItem = 12;
    private const byte CilindroBaixoFalha = 0xF4;
    private const byte CilindroAltoFalha = 0x2C;

    public static LeituraSmartAta Interpretar(SmartAtaBruto bruto)
    {
        if (bruto.Falha is { } falha)
        {
            return new LeituraSmartAta([], null, null, null, null, null, falha);
        }

        var (rotacao, ssd, sata) = Identificacao(bruto.Identificacao);
        if (bruto.Valores is not { Length: >= Tamanho } valores)
        {
            return new LeituraSmartAta([], null, rotacao, ssd, sata, null, "o disco não devolveu a tabela SMART");
        }

        var limites = LerLimites(bruto.Limites);
        var atributos = new List<AtributoSmart>();
        for (var i = 0; i < Itens; i++)
        {
            var item = valores.AsSpan(2 + (i * TamanhoItem), TamanhoItem);
            if (item[0] == 0)
            {
                continue;
            }

            var bruto6 = item.Slice(5, 6);
            ulong valorBruto = 0;
            for (var b = 5; b >= 0; b--)
            {
                valorBruto = (valorBruto << 8) | bruto6[b];
            }

            byte? limite = limites is not null && limites.TryGetValue(item[0], out var l) ? l : null;
            atributos.Add(new AtributoSmart(item[0], BinaryPrimitives.ReadUInt16LittleEndian(item[1..]), item[3], item[4], limite, valorBruto));
        }

        var observacao = SomaCorreta(valores) ? null : "soma de verificação da tabela SMART não confere";
        return new LeituraSmartAta(atributos, Status(bruto.CilindroBaixo, bruto.CilindroAlto), rotacao, ssd, sata, observacao, null);
    }

    /// <summary>Limite por id. Limite 0 conta como lido: a regra de saúde é que ignora limite zero.</summary>
    private static Dictionary<byte, byte>? LerLimites(byte[]? limites)
    {
        if (limites is not { Length: >= Tamanho })
        {
            return null;
        }

        var porId = new Dictionary<byte, byte>();
        for (var i = 0; i < Itens; i++)
        {
            var item = limites.AsSpan(2 + (i * TamanhoItem), TamanhoItem);
            if (item[0] != 0)
            {
                porId.TryAdd(item[0], item[1]);
            }
        }

        return porId;
    }

    internal static bool? Status(byte? cilindroBaixo, byte? cilindroAlto) => (cilindroBaixo, cilindroAlto) switch
    {
        (CilindroBaixoFalha, CilindroAltoFalha) => true,
        (ComandosAta.CilindroBaixoSmart, ComandosAta.CilindroAltoSmart) => false,
        _ => null,
    };

    private static bool SomaCorreta(byte[] bloco)
    {
        byte soma = 0;
        for (var i = 0; i < Tamanho; i++)
        {
            soma += bloco[i];
        }

        return soma == 0;
    }

    /// <summary>Rotação, SSD (rotação 1) e velocidade SATA atual a partir do IDENTIFY. Palavras de 16 bits em little-endian.</summary>
    internal static (int? Rotacao, bool? Ssd, string? Sata) Identificacao(byte[]? id)
    {
        if (id is not { Length: >= Tamanho })
        {
            return (null, null, null);
        }

        var palavra77 = BinaryPrimitives.ReadUInt16LittleEndian(id.AsSpan(77 * 2));
        var palavra217 = BinaryPrimitives.ReadUInt16LittleEndian(id.AsSpan(217 * 2));
        var velocidade = (palavra77 & 1) == 0 ? (palavra77 >> 1) & 0x7 : 0;
        var sata = velocidade switch
        {
            1 => "SATA 1,5 Gb/s",
            2 => "SATA 3 Gb/s",
            3 => "SATA 6 Gb/s",
            _ => null,
        };

        return palavra217 switch
        {
            1 => (null, true, sata),
            > 0x0400 and < 0xFFFF => (palavra217, false, sata),
            _ => (null, null, sata),
        };
    }
}
