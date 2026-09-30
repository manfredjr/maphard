using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Cpuid;
using MapHard.Nucleo.Firmware;
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
        public int? ClockRegistroMhz() => 3600;

        public int? ClockMaximoWindowsMhz() => 3600;

        public AmostraDesempenho? Amostrar() => amostrar();
    }

    internal sealed class Firmware : IFonteFirmware
    {
        public int? TipoFirmware() => 2;

        public bool VirtualizacaoLigadaNoFirmware() => true;

        public RespostaTpm? Tpm() => new(0, 2);
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
        CpuidSimulado.Base(maiorBasica: 0x16).Com(0x16, 3600, 4700).NomeComercial("Processador de Teste 3.60GHz").Bit(1, 'c', 5);

    public static FontesColeta Completas(
        IFonteSmbios? smbios = null,
        IFonteCpuid? cpuid = null,
        IFonteTopologia? topologia = null,
        IFonteClocks? clocks = null) => new(
        smbios ?? new Smbios(() => TabelaSmbios()),
        cpuid ?? Cpuid(),
        topologia ?? new Topologia(TabelaTopologia),
        clocks ?? new Clocks(() => new AmostraDesempenho(125, [20, 40])),
        new Firmware(),
        new Registro(),
        () => "ESTACAO-TESTE",
        () => 16L * 1024 * 1024 * 1024,
        () => false);
}
