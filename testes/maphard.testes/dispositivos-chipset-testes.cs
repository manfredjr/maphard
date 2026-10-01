using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Dispositivos;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class DispositivosChipsetTestes
{
    private static readonly TabelaChipsets Tabela = TabelaChipsets.Ler(
        "fabricante;dispositivo;nome;fonte\n8086;5182;Alder Lake PCH eSPI Controller;f\n8086;3E0F;8th Gen Core Host Bridge;f\n");

    private static DispositivoBruto D(string nome, params string[] ids) => new(nome, "System", ids, 0, null);

    [Fact]
    public void Controlador_lpc_ou_espi_da_o_chipset_pela_tabela()
    {
        var c = Tabela.Identificar(
        [
            D("Ponte do processador", @"PCI\VEN_8086&DEV_3E0F&SUBSYS_0B641028&REV_01", @"PCI\VEN_8086&DEV_3E0F&CC_0600"),
            D("Controlador eSPI", @"PCI\VEN_8086&DEV_5182&SUBSYS_0B641028&REV_01", @"PCI\VEN_8086&DEV_5182&CC_0601"),
        ]);

        Assert.Equal("Alder Lake PCH eSPI Controller", c.Valor);
        Assert.Equal(FonteDado.Tabela, c.Fonte);
        Assert.Equal("dispositivo PCI 8086:5182", c.Motivo);
    }

    [Fact]
    public void Sem_lpc_vale_a_ponte_do_processador()
    {
        var c = Tabela.Identificar([D("Ponte do processador", @"PCI\VEN_8086&DEV_3E0F&SUBSYS_0B641028&REV_01", @"PCI\VEN_8086&DEV_3E0F&CC_0600")]);

        Assert.Equal("8th Gen Core Host Bridge", c.Valor);
    }

    [Fact]
    public void Fora_da_tabela_usa_o_nome_do_windows_com_a_observacao()
    {
        var c = Tabela.Identificar([D("Controlador LPC Exemplo", @"PCI\VEN_1022&DEV_FFFF&REV_51", @"PCI\VEN_1022&DEV_FFFF&CC_060100")]);

        Assert.Equal("Controlador LPC Exemplo", c.Valor);
        Assert.Equal("dispositivo PCI 1022:FFFF, fora da tabela do MapHard", c.Motivo);
    }

    [Fact]
    public void Maquina_virtual_sem_ponte_conhecida_fica_nao_informada()
    {
        var c = Tabela.Identificar([D("Adaptador de vídeo", @"PCI\VEN_1234&DEV_1111&CC_0300")]);

        Assert.Equal(EstadoCampo.NaoInformado, c.Estado);
    }

    [Theory]
    [InlineData("fabricante;dispositivo;nome;fonte\n8086;5182;A;f\n8086;5182;B;f\n")]
    [InlineData("fabricante;dispositivo;nome;fonte\n8086;5182;A; \n")]
    [InlineData("fabricante;dispositivo;nome;fonte\nXYZW;5182;A;f\n")]
    public void Tabela_com_linha_repetida_sem_fonte_ou_id_invalido_e_erro(string texto)
    {
        Assert.Throws<FormatException>(() => TabelaChipsets.Ler(texto));
    }

    [Fact]
    public void Tabela_embutida_e_integra()
    {
        Assert.True(TabelaChipsets.Embutida.Quantidade > 100);
        Assert.Equal("Alder Lake PCH eSPI Controller", TabelaChipsets.Embutida.Buscar("8086", "5182"));
    }

    [FatoWindows]
    public void Maquina_real_tem_chipset_identificado()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var c = TabelaChipsets.Embutida.Identificar(new FonteDispositivosWindows().Ler());

        Assert.NotEqual(EstadoCampo.ErroLeitura, c.Estado);
    }
}
