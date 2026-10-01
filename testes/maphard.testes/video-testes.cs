using System.Buffers.Binary;
using System.Text;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Dispositivos;
using MapHard.Nucleo.Tabelas;
using MapHard.Nucleo.Video;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class VideoTestes
{
    private static readonly FabricantesPci Fabricantes = FabricantesPci.Ler("fabricante;nome;fonte\n10DE;Fabricante de Vídeo Exemplo;f\n8086;Fabricante Integrado Exemplo;f\n");

    /// <summary>DXGI_ADAPTER_DESC1 de 64 bits montado à mão.</summary>
    internal static byte[] Descricao(string nome, uint fabricante, uint dispositivo, long dedicada, long compartilhada = 8L << 30, uint sinais = 0)
    {
        var d = new byte[LeitorVideo.TamanhoDescricao];
        Encoding.Unicode.GetBytes(nome).CopyTo(d, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(256), fabricante);
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(260), dispositivo);
        BinaryPrimitives.WriteUInt64LittleEndian(d.AsSpan(272), (ulong)dedicada);
        BinaryPrimitives.WriteUInt64LittleEndian(d.AsSpan(288), (ulong)compartilhada);
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(304), sinais);
        return d;
    }

    private static readonly DispositivoBruto Driver = new("Placa de Vídeo Exemplo", "Display", [@"PCI\VEN_10DE&DEV_25A2&SUBSYS_00000000&REV_A1"], 0, "32.0.15.1234", new DateOnly(2026, 3, 30));

    [Fact]
    public void Placa_com_nome_fabricante_memoria_e_driver()
    {
        var p = Assert.Single(LeitorVideo.Montar([Descricao("Placa de Vídeo Exemplo", 0x10DE, 0x25A2, 4L << 30)], [Driver], Fabricantes));

        Assert.Equal("Placa de Vídeo Exemplo", p.Nome.Valor);
        Assert.Equal("Fabricante de Vídeo Exemplo", p.Fabricante.Valor);
        Assert.Equal(4L << 30, p.MemoriaDedicada.Valor);
        Assert.Equal("32.0.15.1234", p.VersaoDriver.Valor);
        Assert.Equal(new DateOnly(2026, 3, 30), p.DataDriver.Valor);
    }

    [Fact]
    public void Memoria_acima_de_4_gb_nao_e_cortada()
    {
        Assert.Equal(12L << 30, LeitorVideo.Montar([Descricao("Placa Exemplo", 0x10DE, 0x25A2, 12L << 30)], [], Fabricantes)[0].MemoriaDedicada.Valor);
    }

    [Fact]
    public void Adaptador_de_software_fica_de_fora()
    {
        Assert.Empty(LeitorVideo.Montar([Descricao("Adaptador de Software Exemplo", 0x1414, 0x008C, 0, sinais: 2)], [], Fabricantes));
    }

    [Fact]
    public void Video_integrado_sem_memoria_dedicada_e_driver_nao_achado()
    {
        var p = LeitorVideo.Montar([Descricao("Vídeo Integrado Exemplo", 0x8086, 0x46A6, 0)], [Driver], Fabricantes)[0];

        Assert.Equal(EstadoCampo.NaoInformado, p.MemoriaDedicada.Estado);
        Assert.Contains("memória do sistema", p.MemoriaDedicada.Motivo, StringComparison.Ordinal);
        Assert.Equal("dispositivo não achado na SetupAPI", p.VersaoDriver.Motivo);
    }

    [Fact]
    public void Id_acpi_fica_sem_fabricante()
    {
        var p = LeitorVideo.Montar([Descricao("Vídeo ACPI Exemplo", 0x51434F4D, 1, 1L << 30)], [], Fabricantes)[0];

        Assert.Equal(EstadoCampo.NaoInformado, p.Fabricante.Estado);
        Assert.Equal("ID ACPI, não PCI", p.Fabricante.Motivo);
    }

    [Fact]
    public void Descricao_curta_vira_nula()
    {
        Assert.Null(LeitorVideo.Interpretar(new byte[100]));
        Assert.Null(LeitorVideo.Interpretar(null));
    }

    [Fact]
    public void Tabela_de_fabricantes_pci_embutida_e_integra()
    {
        Assert.True(FabricantesPci.Embutida.Quantidade > 1000);
        Assert.Equal("NVIDIA Corporation", FabricantesPci.Embutida.Nome(0x10DE));
        Assert.Equal("Intel Corporation", FabricantesPci.Embutida.Nome(0x8086));
    }

    [FatoWindows]
    public void Maquina_real_tem_ao_menos_uma_placa_com_nome()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var placas = LeitorVideo.Montar(new FonteVideoWindows().Adaptadores(), new FonteDispositivosWindows().Ler(), FabricantesPci.Embutida);

        Assert.Contains(placas, p => p.Nome.FoiLido);
    }
}
