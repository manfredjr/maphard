using System.Globalization;

namespace MapHard.Nucleo.Smbios;

/// <summary>Tipo 0, BIOS. Deslocamentos conferidos no dmidecode (seção 7.1 da especificação).</summary>
public sealed record BiosSmbios(string? Fabricante, string? Versao, string? DataTexto, DateOnly? Data, bool? SuportaUefi, bool? MaquinaVirtual)
{
    public static BiosSmbios? De(TabelaSmbios tabela)
    {
        var e = tabela.PrimeiraDoTipo(0);
        if (e is null || e.Tamanho < 0x12)
        {
            return null;
        }

        var dataTexto = e.Texto(0x08);

        // Byte de extensão 2 (deslocamento 0x13): bit 3 UEFI suportado, bit 4 máquina virtual.
        var extensao2 = e.Tamanho >= 0x14 ? e.Byte(0x13) : null;
        return new BiosSmbios(
            e.Texto(0x04),
            e.Texto(0x05),
            dataTexto,
            LerData(dataTexto),
            extensao2 is null ? null : (extensao2.Value & 0x08) != 0,
            extensao2 is null ? null : (extensao2.Value & 0x10) != 0);
    }

    /// <summary>A especificação pede mm/dd/aaaa. Firmware antigo usa mm/dd/aa.</summary>
    internal static DateOnly? LerData(string? texto)
    {
        if (texto is null)
        {
            return null;
        }

        string[] formatos = ["MM/dd/yyyy", "MM/dd/yy", "M/d/yyyy"];
        return DateOnly.TryParseExact(texto.Trim(), formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data : null;
    }
}

/// <summary>Tipo 1, sistema (o equipamento). Seção 7.2.</summary>
public sealed record SistemaSmbios(string? Fabricante, string? Produto, string? Versao, string? NumeroSerie, string? Uuid, string? Sku, string? Familia)
{
    public static SistemaSmbios? De(TabelaSmbios tabela)
    {
        var e = tabela.PrimeiraDoTipo(1);
        if (e is null || e.Tamanho < 0x08)
        {
            return null;
        }

        var uuid = e.Tamanho >= 0x19 ? LerUuid(e.Bytes(0x08, 16), tabela.VersaoNumerica) : null;
        var temSku = e.Tamanho >= 0x1B;
        return new SistemaSmbios(
            e.Texto(0x04),
            e.Texto(0x05),
            e.Texto(0x06),
            e.Texto(0x07),
            uuid,
            temSku ? e.Texto(0x19) : null,
            temSku ? e.Texto(0x1A) : null);
    }

    /// <summary>
    /// A partir da versão 2.6, os três primeiros grupos do UUID vêm em little-endian.
    /// Tudo 0x00 ("não configurável") ou tudo 0xFF ("ausente") vira null. Mesma regra do dmidecode.
    /// </summary>
    internal static string? LerUuid(byte[]? p, int versao)
    {
        if (p is null || p.All(b => b == 0x00) || p.All(b => b == 0xFF))
        {
            return null;
        }

        int[] ordem = versao >= 0x0206
            ? [3, 2, 1, 0, 5, 4, 7, 6, 8, 9, 10, 11, 12, 13, 14, 15]
            : [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        var hex = string.Concat(ordem.Select(i => p[i].ToString("X2", CultureInfo.InvariantCulture)));
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}

/// <summary>Tipo 2, placa-mãe. Seção 7.3.</summary>
public sealed record PlacaSmbios(string? Fabricante, string? Produto, string? Versao, string? NumeroSerie)
{
    public static PlacaSmbios? De(TabelaSmbios tabela)
    {
        var e = tabela.PrimeiraDoTipo(2);
        if (e is null || e.Tamanho < 0x08)
        {
            return null;
        }

        return new PlacaSmbios(e.Texto(0x04), e.Texto(0x05), e.Texto(0x06), e.Texto(0x07));
    }
}

/// <summary>Tipo 3, gabinete. Seção 7.4. O tipo fica nos bits 0 a 6 do byte 0x05; o bit 7 é a trava.</summary>
public sealed record GabineteSmbios(string? Fabricante, byte? CodigoTipo, string? Tipo, string? NumeroSerie)
{
    // Tabela 7.4.1, na ordem do dmidecode (códigos 0x01 a 0x24).
    private static readonly string[] _tipos =
    [
        "outro", "desconhecido", "desktop", "desktop compacto", "pizza box", "minitorre", "torre",
        "portátil", "laptop", "notebook", "portátil de mão", "estação de acoplamento", "all-in-one",
        "subnotebook", "desktop compacto", "lunch box", "servidor", "gabinete de expansão", "subgabinete",
        "gabinete de expansão de barramento", "gabinete de periférico", "gabinete RAID", "rack",
        "PC selado", "multissistema", "CompactPCI", "AdvancedTCA", "blade", "gabinete de blades",
        "tablet", "conversível", "destacável", "gateway IoT", "PC embarcado", "mini PC", "stick PC",
    ];

    public static GabineteSmbios? De(TabelaSmbios tabela)
    {
        var e = tabela.PrimeiraDoTipo(3);
        if (e is null || e.Tamanho < 0x09)
        {
            return null;
        }

        var codigo = (byte?)(e.Byte(0x05) & 0x7F);
        return new GabineteSmbios(e.Texto(0x04), codigo, NomeTipo(codigo), e.Texto(0x07));
    }

    internal static string? NomeTipo(byte? codigo) =>
        codigo is >= 0x01 and <= 0x24 ? _tipos[codigo.Value - 1] : null;

    /// <summary>Notebook, laptop, portátil, subnotebook, tablet, conversível e destacável.</summary>
    public bool EhPortatil => CodigoTipo is 0x08 or 0x09 or 0x0A or 0x0E or 0x1E or 0x1F or 0x20;
}

/// <summary>Tipo 4, processador. Seção 7.5. Os campos de 2 bytes da versão 3.0 valem quando o de 1 byte é 0xFF.</summary>
public sealed record ProcessadorSmbios(
    string? Soquete,
    string? Fabricante,
    string? Versao,
    int? ClockExternoMhz,
    int? ClockMaximoMhz,
    int? Nucleos,
    int? NucleosAtivos,
    int? Threads)
{
    public static IReadOnlyList<ProcessadorSmbios> Todos(TabelaSmbios tabela) =>
        tabela.DoTipo(4).Where(e => e.Tamanho >= 0x1A && Instalado(e)).Select(De).ToList();

    private static bool Instalado(EstruturaSmbios e) => e.Byte(0x18) is { } status && (status & 0x40) != 0;

    private static ProcessadorSmbios De(EstruturaSmbios e) => new(
        e.Texto(0x04),
        e.Texto(0x07),
        e.Texto(0x10),
        Frequencia(e.Palavra(0x12)),
        Frequencia(e.Palavra(0x14)),
        Contagem(e, 0x23, 0x2A),
        Contagem(e, 0x24, 0x2C),
        Contagem(e, 0x25, 0x2E));

    private static int? Frequencia(ushort? mhz) => mhz is null or 0 ? null : mhz;

    private static int? Contagem(EstruturaSmbios e, int curto, int longo)
    {
        if (e.Tamanho < 0x28)
        {
            return null;
        }

        var valor = e.Byte(curto);
        if (valor is null or 0)
        {
            return null;
        }

        if (valor == 0xFF && e.Tamanho >= longo + 2)
        {
            var extenso = e.Palavra(longo);
            return extenso is null or 0 ? null : extenso;
        }

        return valor;
    }
}
