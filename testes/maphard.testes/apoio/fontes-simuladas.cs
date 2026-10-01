using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Cpuid;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Processador;
using MapHard.Nucleo.Smbios;
using MapHard.Nucleo.Windows;

namespace MapHard.Testes.Apoio;

/// <summary>Fontes de uma máquina fictícia completa, para os testes do coletor, do JSON e do painel.</summary>
internal static class FontesSimuladas
{
    internal sealed class Smbios(Func<byte[]?> ler) : IFonteSmbios
    {
        public byte[]? LerTabelaBruta() => ler();
    }

    internal sealed class Topologia(Func<byte[]?> ler) : IFonteTopologia
    {
        public byte[]? LerBruta() => ler();
    }

    internal sealed class Clocks(Func<AmostraDesempenho?> amostrar) : IFonteClocks
    {
        public int? ClockNominalWindowsMhz() => 3600;

        public AmostraDesempenho? Amostrar() => amostrar();
    }

    internal sealed class Firmware(bool virtualizacaoLigada = true) : IFonteFirmware
    {
        public int? TipoFirmware() => 2;

        public bool VirtualizacaoLigadaNoFirmware() => virtualizacaoLigada;

        public RespostaTpm? Tpm() => new(0, 2);
    }

    /// <summary>16 GB instalados, com 200 MB reservados pelo hardware.</summary>
    internal sealed class Memoria(long? instaladaKb = 16L * 1024 * 1024) : IFonteMemoria
    {
        public const long Utilizavel = (16L * 1024 * 1024 * 1024) - (200L * 1024 * 1024);

        public long? InstaladaKb() => instaladaKb;

        public EstadoMemoriaWindows? Estado() => new(Utilizavel, 8L * 1024 * 1024 * 1024, 48, 10L * 1024 * 1024 * 1024, 24L * 1024 * 1024 * 1024, 3L * 1024 * 1024 * 1024);
    }

    internal sealed class Registro : IFonteRegistro
    {
        private readonly Dictionary<(string, string), object> _valores = new()
        {
            [(LeitorWindows.Chave, "CurrentBuild")] = "22631",
            [(LeitorWindows.Chave, "UBR")] = 4169,
            [(LeitorWindows.Chave, "DisplayVersion")] = "23H2",
            [(LeitorWindows.Chave, "EditionID")] = "Professional",
            [(LeitorFirmware.ChaveSecureBoot, "UEFISecureBootEnabled")] = 1,
        };

        public object? Ler(string chave, string valor) => _valores.GetValueOrDefault((chave, valor));
    }

    public static byte[] TabelaSmbios(string fabricanteSistema = "Fabricante Exemplo", byte nucleosSmbios = 8)
    {
        var processador = new byte[0x30 - 4];
        processador[0x04 - 4] = 1;
        processador[0x07 - 4] = 2;
        processador[0x10 - 4] = 3;
        processador[0x14 - 4] = 0x5C;
        processador[0x15 - 4] = 0x12;
        processador[0x18 - 4] = 0x41;
        processador[0x23 - 4] = nucleosSmbios;
        processador[0x24 - 4] = nucleosSmbios;
        processador[0x25 - 4] = 16;

        return new ConstrutorSmbios()
            .Estrutura(0, ConstrutorSmbios.Corpo(0x18, (0x04, 1), (0x05, 2), (0x08, 3), (0x13, 0x08)), "Fabricante BIOS", "F.10", "03/15/2021")
            .Estrutura(1, ConstrutorSmbios.Corpo(0x1B, (0x04, 1), (0x05, 2), (0x07, 3)), fabricanteSistema, "Modelo Exemplo", "SERIE-TESTE-0001")
            .Estrutura(2, ConstrutorSmbios.Corpo(0x08, (0x04, 1), (0x05, 2), (0x07, 3)), "Fabricante Placa", "PLACA-X1", "SERIE-TESTE-0002")
            .Estrutura(3, ConstrutorSmbios.Corpo(0x09, (0x04, 1), (0x05, 0x03)), "Fabricante Exemplo")
            .Estrutura(4, processador, "SOQUETE 1", "Fabricante CPU", "Processador Exemplo")
            .Estrutura(16, ConstrutorMemoria.Conjunto(64 * 1024 * 1024, 4))
            .Estrutura(17, ConstrutorMemoria.Modulo(8192, formato: 0x09, codigoFabricante: 0xCE80), "ChannelA-DIMM0", "BANK 0", "Fabricante Memoria", "SERIE-MEM-0001", "PATRIMONIO-0001", "PN-TESTE-3200")
            .Estrutura(17, ConstrutorMemoria.Modulo(0, formato: 0x09), "ChannelA-DIMM1", "BANK 1")
            .Estrutura(17, ConstrutorMemoria.Modulo(8192, formato: 0x09, codigoFabricante: 0xCE80), "ChannelB-DIMM0", "BANK 2", "Fabricante Memoria", "SERIE-MEM-0002", "PATRIMONIO-0002", "PN-TESTE-3200")
            .Estrutura(17, ConstrutorMemoria.Modulo(0, formato: 0x09), "ChannelB-DIMM1", "BANK 3")
            .Fim()
            .Montar();
    }

    public static byte[] TabelaTopologia()
    {
        var c = new ConstrutorTopologia().Pacote();
        for (var i = 0; i < 8; i++)
        {
            c.Nucleo(2).Cache(1, 2, 48 * 1024).Cache(1, 1, 32 * 1024).Cache(2, 0, 1280 * 1024);
        }

        return c.Cache(3, 0, 24 * 1024 * 1024).Montar();
    }

    public static IFonteCpuid Cpuid() =>
        CpuidSimulado.Base(maiorBasica: 0x16).Com(0x16, 3600, 4700).NomeComercial("Processador de Teste 3.60GHz").Bit(1, 'c', 5).Bit(0x80000001, 'd', 29);

    public static FontesColeta Completas(
        IFonteSmbios? smbios = null,
        IFonteCpuid? cpuid = null,
        IFonteTopologia? topologia = null,
        IFonteClocks? clocks = null,
        IFonteMemoria? memoria = null) => new(
        smbios ?? new Smbios(() => TabelaSmbios()),
        cpuid ?? Cpuid(),
        topologia ?? new Topologia(TabelaTopologia),
        clocks ?? new Clocks(() => new AmostraDesempenho(125, [20, 40])),
        new Firmware(),
        new Registro(),
        () => "ESTACAO-TESTE",
        memoria ?? new Memoria(),
        () => false);
}
