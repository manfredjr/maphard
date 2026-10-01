namespace MapHard.Nucleo.Smbios;

/// <summary>
/// Tipo 16, conjunto de memória física. Seção 7.17 da especificação. Deslocamentos e tamanhos
/// mínimos conferidos no dmidecode (dmidecode.c, dmi_decode, caso 16).
/// </summary>
public sealed record ConjuntoMemoriaSmbios(byte? Uso, byte? CorrecaoErro, long? CapacidadeMaximaBytes, int? Slots)
{
    /// <summary>Uso 0x03, tabela 7.17.2: memória do sistema.</summary>
    public const byte UsoMemoriaSistema = 0x03;

    private const uint CapacidadeNoEstendido = 0x80000000;

    /// <summary>Só os conjuntos de memória do sistema. Os de vídeo, flash e cache não contam.</summary>
    public static IReadOnlyList<ConjuntoMemoriaSmbios> DoSistema(TabelaSmbios tabela) =>
        tabela.DoTipo(16).Where(e => e.Tamanho >= 0x0F).Select(De).Where(c => c.Uso == UsoMemoriaSistema).ToList();

    private static ConjuntoMemoriaSmbios De(EstruturaSmbios e)
    {
        // Capacidade em KB. 0x80000000 manda ler o campo estendido, em bytes, que só existe com 0x17 ou mais.
        var capacidade = e.PalavraDupla(0x07) switch
        {
            CapacidadeNoEstendido => e.Tamanho >= 0x17 && e.PalavraQuadrupla(0x0F) is { } bytes and > 0 ? (long?)bytes : null,
            null or 0 => null,
            var kb => (long)kb.Value * 1024,
        };

        var slots = e.Palavra(0x0D);
        return new ConjuntoMemoriaSmbios(e.Byte(0x05), e.Byte(0x06), capacidade, slots is null or 0 ? null : slots);
    }

    /// <summary>Tabela 7.17.3, a partir de 0x03 (0x01 outro e 0x02 desconhecido viram null).</summary>
    public static string? NomeCorrecaoErro(byte? codigo) => codigo switch
    {
        0x03 => "nenhuma",
        0x04 => "paridade",
        0x05 => "ECC de um bit",
        0x06 => "ECC de vários bits",
        0x07 => "CRC",
        _ => null,
    };
}

/// <summary>
/// Tipo 17, módulo de memória (um por slot, com ou sem módulo). Seção 7.18. Deslocamentos, unidades
/// e tamanhos mínimos de cada campo conferidos no dmidecode (dmidecode.c, dmi_decode, caso 17, e as
/// funções dmi_memory_device_size, _extended_size, _speed, _form_factor, _type e _manufacturer_id).
/// </summary>
public sealed record ModuloMemoriaSmbios(
    string? Slot,
    string? Banco,
    long? TamanhoBytes,
    bool Vazio,
    byte? Formato,
    byte? Tipo,
    int? VelocidadeNominal,
    int? VelocidadeConfigurada,
    string? Fabricante,
    ushort? CodigoFabricante,
    string? NumeroSerie,
    string? PartNumber,
    int? Ranks,
    int? VoltagemConfiguradaMv)
{
    private const ushort TamanhoDesconhecido = 0xFFFF;
    private const ushort TamanhoNoEstendido = 0x7FFF;
    private const ushort TamanhoEmKb = 0x8000;
    private const ushort VelocidadeNoEstendido = 0xFFFF;

    // Tabela 7.18.1, códigos 0x01 a 0x10, na ordem do dmidecode. "Outro" e "desconhecido" ficam null.
    private static readonly string?[] _formatos =
    [
        null, null, "SIMM", "SIP", "chip", "DIP", "ZIP", "placa proprietária", "DIMM", "TSOP",
        "fileira de chips", "RIMM", "SODIMM", "SRIMM", "FB-DIMM", "die",
    ];

    // Tabela 7.18.2, códigos 0x01 a 0x24, na ordem do dmidecode. "Outro", "desconhecido" e os reservados ficam null.
    private static readonly string?[] _tipos =
    [
        null, null, "DRAM", "EDRAM", "VRAM", "SRAM", "RAM", "ROM", "Flash", "EEPROM", "FEPROM",
        "EPROM", "CDRAM", "3DRAM", "SDRAM", "SGRAM", "RDRAM", "DDR", "DDR2", "DDR2 FB-DIMM", null, null, null,
        "DDR3", "FBD2", "DDR4", "LPDDR", "LPDDR2", "LPDDR3", "LPDDR4", "dispositivo lógico não volátil",
        "HBM", "HBM2", "DDR5", "LPDDR5", "HBM3",
    ];

    public static IReadOnlyList<ModuloMemoriaSmbios> Todos(TabelaSmbios tabela) =>
        tabela.DoTipo(17).Where(e => e.Tamanho >= 0x15).Select(De).ToList();

    private static ModuloMemoriaSmbios De(EstruturaSmbios e)
    {
        var tamanhoBruto = e.Palavra(0x0C);
        var vazio = tamanhoBruto == 0;
        var slot = e.Texto(0x10);
        var banco = e.Texto(0x11);
        var formato = e.Byte(0x0E);
        var tipo = e.Byte(0x12);

        // Sem módulo, o resto da estrutura não vale, como no dmidecode.
        if (vazio)
        {
            return new ModuloMemoriaSmbios(slot, banco, null, true, formato, tipo, null, null, null, null, null, null, null, null);
        }

        var estendida = e.Tamanho >= 0x5C;
        return new ModuloMemoriaSmbios(
            slot,
            banco,
            Tamanho(tamanhoBruto, e.Tamanho >= 0x20 ? e.PalavraDupla(0x1C) : null),
            false,
            formato,
            tipo,
            e.Tamanho >= 0x17 ? Velocidade(e.Palavra(0x15), estendida ? e.PalavraDupla(0x54) : null) : null,
            e.Tamanho >= 0x22 ? Velocidade(e.Palavra(0x20), estendida ? e.PalavraDupla(0x58) : null) : null,
            e.Tamanho >= 0x1B ? e.Texto(0x17) : null,
            e.Tamanho >= 0x34 && e.Palavra(0x2C) is { } codigo and not 0 ? codigo : null,
            e.Tamanho >= 0x1B ? e.Texto(0x18) : null,
            e.Tamanho >= 0x1B ? e.Texto(0x1A) : null,
            e.Tamanho >= 0x1C && (e.Byte(0x1B) & 0x0F) is { } ranks and not 0 ? ranks : null,
            e.Tamanho >= 0x28 && e.Palavra(0x26) is { } mv and not 0 ? mv : null);
    }

    /// <summary>
    /// 0xFFFF é desconhecido. 0x7FFF manda ler o estendido (bits 0 a 30, em MB). Nos outros, o bit 15
    /// ligado quer dizer KB e desligado, MB.
    /// </summary>
    internal static long? Tamanho(ushort? bruto, uint? estendido)
    {
        if (bruto is null or 0 or TamanhoDesconhecido)
        {
            return null;
        }

        if (bruto == TamanhoNoEstendido)
        {
            return estendido is { } mb && (mb & 0x7FFFFFFF) != 0 ? (mb & 0x7FFFFFFF) * 1024L * 1024 : null;
        }

        var valor = bruto.Value & 0x7FFF;
        return (bruto.Value & TamanhoEmKb) != 0 ? valor * 1024L : valor * 1024L * 1024;
    }

    /// <summary>MT/s. 0 é desconhecido. 0xFFFF manda ler o estendido de 4 bytes.</summary>
    internal static int? Velocidade(ushort? bruto, uint? estendido) => bruto switch
    {
        null or 0 => null,
        VelocidadeNoEstendido => estendido is { } v and > 0 and <= int.MaxValue ? (int)v : null,
        _ => bruto,
    };

    public static string? NomeTipo(byte? codigo) => codigo is >= 0x01 and <= 0x24 ? _tipos[codigo.Value - 1] : null;

    public static string? NomeFormato(byte? codigo) => codigo is >= 0x01 and <= 0x10 ? _formatos[codigo.Value - 1] : null;

    public string? NomeDoTipo => NomeTipo(Tipo);

    public string? NomeDoFormato => NomeFormato(Formato);
}
