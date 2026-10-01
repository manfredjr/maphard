using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Tabelas;

namespace MapHard.Testes;

public class FabricantesMemoriaTestes
{
    private static readonly FabricantesMemoria Tabela = FabricantesMemoria.Ler(
        "# comentario\n"
        + "banco;codigo;nome;fonte\n"
        + "1;CE;Samsung;f\n"
        + "1;AD;SK Hynix (former Hyundai Electronics);f\n"
        + "2;98;Kingston;f\n");

    [Fact]
    public void Nome_ja_em_texto_fica_como_esta()
    {
        var c = Tabela.Traduzir("Crucial", null);

        Assert.Equal("Crucial", c.Valor);
        Assert.Equal(FonteDado.Smbios, c.Fonte);
    }

    [Theory]
    [InlineData("80CE")]
    [InlineData("CE00")]
    [InlineData("0x80CE")]
    [InlineData("CE00000000000000")]
    public void Codigo_em_texto_da_o_fabricante(string texto)
    {
        var c = Tabela.Traduzir(texto, null);

        Assert.Equal("Samsung", c.Valor);
        Assert.Equal(FonteDado.Tabela, c.Fonte);
        Assert.Contains("0x80CE", c.Motivo);
    }

    [Fact]
    public void Codigo_repetido_de_12_digitos_usa_os_dois_primeiros_bytes()
    {
        Assert.Equal("SK Hynix", Tabela.Traduzir("80AD000080AD", null).Valor);
    }

    [Fact]
    public void Banco_2_com_continuacao()
    {
        Assert.Equal("Kingston", Tabela.Traduzir("0198", null).Valor);
        Assert.Equal("Kingston", Tabela.Traduzir("9801", null).Valor);
        Assert.Equal("Kingston", Tabela.Traduzir(null, 0x9801).Valor);
    }

    [Fact]
    public void Codigo_de_2_bytes_do_smbios_da_o_mesmo_resultado_que_o_texto()
    {
        Assert.Equal(Tabela.Traduzir("80CE", null), Tabela.Traduzir(null, 0xCE80));
    }

    [Fact]
    public void Codigo_do_smbios_tem_prioridade_sobre_o_texto()
    {
        Assert.Equal("Samsung", Tabela.Traduzir("Outro Nome", 0xCE80).Valor);
    }

    [Theory]
    [InlineData("80CF")]
    [InlineData("0080")]
    public void Codigo_com_paridade_errada_fica_nao_informado_com_o_motivo(string texto)
    {
        var c = Tabela.Traduzir(texto, null);

        Assert.Equal(EstadoCampo.NaoInformado, c.Estado);
        Assert.Contains("código JEDEC inválido", c.Motivo);
    }

    [Fact]
    public void Codigo_fora_da_tabela_e_lido_com_a_observacao()
    {
        var c = Tabela.Traduzir("8001", null);

        Assert.Equal(EstadoCampo.Lido, c.Estado);
        Assert.Equal("código JEDEC 0x8001", c.Valor);
        Assert.Equal("fora da tabela do MapHard", c.Motivo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0000")]
    [InlineData("Unknown")]
    [InlineData("Undefined")]
    [InlineData("Not Specified")]
    public void Sem_fabricante_fica_nao_informado(string? texto)
    {
        Assert.Equal(EstadoCampo.NaoInformado, Tabela.Traduzir(texto, null).Estado);
    }

    [Fact]
    public void Linha_repetida_e_erro()
    {
        Assert.Throws<FormatException>(() => FabricantesMemoria.Ler("banco;codigo;nome;fonte\n1;CE;Samsung;f\n1;CE;Outro;f\n"));
    }

    [Fact]
    public void Linha_sem_fonte_e_erro()
    {
        Assert.Throws<FormatException>(() => FabricantesMemoria.Ler("banco;codigo;nome;fonte\n1;CE;Samsung; \n"));
    }

    [Fact]
    public void Tabela_embutida_e_integra_e_tem_os_fabricantes_comuns()
    {
        var t = FabricantesMemoria.Embutida;

        Assert.True(t.Quantidade > 1000);
        Assert.Contains(1, t.Bancos);
        Assert.Equal("Samsung", t.Traduzir(null, 0xCE80).Valor);
        Assert.Equal("SK Hynix", t.Traduzir(null, 0xAD80).Valor);
        Assert.Equal("Micron Technology", t.Traduzir(null, 0x2C80).Valor);
        Assert.Equal("Kingston", t.Traduzir(null, 0x9801).Valor);
    }
}
