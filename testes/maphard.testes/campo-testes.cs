using MapHard.Nucleo.Campos;

namespace MapHard.Testes;

public class CampoTestes
{
    [Fact]
    public void Lido_guarda_valor_e_fonte()
    {
        var campo = Campo<string>.Lido("Intel", FonteDado.Cpuid);

        Assert.True(campo.FoiLido);
        Assert.Equal("Intel", campo.Valor);
        Assert.Equal(FonteDado.Cpuid, campo.Fonte);
    }

    [Fact]
    public void Lido_nao_aceita_valor_nulo()
    {
        Assert.Throws<ArgumentNullException>(() => Campo<string>.Lido(null!, FonteDado.Cpuid));
    }

    [Fact]
    public void Estados_sem_leitura_nunca_carregam_valor()
    {
        Campo<int>[] campos =
        [
            Campo<int>.NaoInformado(FonteDado.Smbios),
            Campo<int>.RequerAdministrador(FonteDado.Tpm),
            Campo<int>.NaoSuportado(FonteDado.Firmware),
            Campo<int>.Erro(FonteDado.Registro, "chave ausente"),
        ];

        Assert.All(campos, c => Assert.False(c.FoiLido));
        Assert.All(campos, c => Assert.Equal(0, c.Valor));
        Assert.Equal("chave ausente", campos[3].Motivo);
    }

    [Theory]
    [InlineData(EstadoCampo.NaoInformado, "não informado pelo fabricante")]
    [InlineData(EstadoCampo.RequerAdministrador, "requer administrador")]
    [InlineData(EstadoCampo.NaoSuportado, "não disponível neste equipamento")]
    [InlineData(EstadoCampo.ErroLeitura, "erro de leitura")]
    public void Texto_de_cada_estado_e_o_do_desenho(EstadoCampo estado, string esperado)
    {
        Assert.Equal(esperado, TextosEstado.Texto(estado));
    }

    [Theory]
    [InlineData("To Be Filled By O.E.M.")]
    [InlineData("To be filled by O.E.M.")]
    [InlineData("Default string")]
    [InlineData("System Product Name")]
    [InlineData("System manufacturer")]
    [InlineData("Not Applicable")]
    [InlineData("None")]
    [InlineData("0123456789")]
    [InlineData("Not Specified")]
    [InlineData("INVALID")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("00000000")]
    [InlineData("FFFFFFFF")]
    public void Texto_de_fabrica_vira_nao_informado(string texto)
    {
        var campo = Campo.Texto(texto, FonteDado.Smbios);

        Assert.Equal(EstadoCampo.NaoInformado, campo.Estado);
        Assert.Null(campo.Valor);
    }

    [Fact]
    public void Texto_valido_com_default_no_meio_e_lido()
    {
        var campo = Campo.Texto("  Placa Default Line X  ", FonteDado.Smbios);

        Assert.True(campo.FoiLido);
        Assert.Equal("Placa Default Line X", campo.Valor);
    }

    [Fact]
    public void Texto_nulo_vira_nao_informado()
    {
        Assert.Equal(EstadoCampo.NaoInformado, Campo.Texto(null, FonteDado.Smbios).Estado);
    }

    [Fact]
    public void Mapear_transforma_so_o_valor_lido()
    {
        var lido = Campo<int>.Lido(3600, FonteDado.Cpuid).Mapear(v => v / 1000.0);
        var erro = Campo<int>.Erro(FonteDado.Contador, "sem contador").Mapear(v => v / 1000.0);

        Assert.Equal(3.6, lido.Valor);
        Assert.Equal(EstadoCampo.ErroLeitura, erro.Estado);
        Assert.Equal("sem contador", erro.Motivo);
    }

    [Fact]
    public void Opcional_nulo_vira_nao_informado()
    {
        Assert.Equal(EstadoCampo.NaoInformado, Campo.DeOpcional<int>(null, FonteDado.Smbios).Estado);
        Assert.Equal(8, Campo.DeOpcional<int>(8, FonteDado.Smbios).Valor);
    }
}
