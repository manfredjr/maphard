using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Video;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class EdidTestes
{
    /// <summary>Bloco 0 do exemplo Monsamp.inf da página "Overriding monitor EDIDs" da Microsoft.</summary>
    internal static readonly byte[] Exemplo =
    [
        0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x35, 0xEE, 0x34, 0x12, 0x01, 0x00, 0x00, 0x00, 0x0A, 0x0E, 0x01, 0x03, 0x68, 0x22, 0x1B,
        0x78, 0xEA, 0xAE, 0xA5, 0xA6, 0x54, 0x4C, 0x99, 0x26, 0x14, 0x50, 0x54, 0xA5, 0x4B, 0x00, 0x71, 0x4F, 0x81, 0x80, 0xA9, 0x40, 0x01, 0x01,
        0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x30, 0x2A, 0x00, 0x98, 0x51, 0x00, 0x2A, 0x40, 0x30, 0x70, 0x13, 0x00, 0x52, 0x0E, 0x11,
        0x00, 0x00, 0x1E, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x41, 0x42, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0x0A, 0x00, 0x00,
        0x00, 0xFC, 0x00, 0x4D, 0x53, 0x20, 0x31, 0x32, 0x33, 0x34, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x0A, 0x00, 0x00, 0x00, 0xFD, 0x00, 0x38, 0x4C,
        0x1F, 0x50, 0x12, 0x00, 0x0A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x00, 0xDB,
    ];

    [Fact]
    public void Exemplo_da_microsoft()
    {
        var m = LeitorEdid.Interpretar(Exemplo)!;

        Assert.Equal("MON", m.Fabricante.Valor);
        Assert.Equal("MS 1234", m.Modelo.Valor);
        Assert.Equal("AB0000000001", m.NumeroSerie.Valor);
        Assert.Equal(2004, m.AnoFabricacao.Valor);
        Assert.Equal(17.1, m.Polegadas.Valor);
        Assert.Equal("34 x 27 cm", m.Polegadas.Motivo);
        Assert.Equal("1280 x 1024", m.ResolucaoNativa.Valor);
    }

    [Fact]
    public void Soma_de_verificacao_errada_vai_na_observacao()
    {
        var edid = (byte[])Exemplo.Clone();
        edid[127]++;

        Assert.Equal("soma de verificação do EDID não confere", LeitorEdid.Interpretar(edid)!.Fabricante.Motivo);
    }

    [Fact]
    public void Exemplo_tem_a_soma_certa()
    {
        Assert.Null(LeitorEdid.Interpretar(Exemplo)!.Fabricante.Motivo);
    }

    [Fact]
    public void Cabecalho_errado_ou_bloco_curto_vira_nulo()
    {
        var edid = (byte[])Exemplo.Clone();
        edid[0] = 1;

        Assert.Null(LeitorEdid.Interpretar(edid));
        Assert.Null(LeitorEdid.Interpretar(Exemplo[..100]));
        Assert.Null(LeitorEdid.Interpretar(null));
    }

    [Fact]
    public void Sem_nome_usa_o_codigo_do_produto()
    {
        var edid = (byte[])Exemplo.Clone();
        edid[93] = 0xFE;

        var m = LeitorEdid.Interpretar(edid)!;

        Assert.Equal("produto 1234", m.Modelo.Valor);
        Assert.Equal("o monitor não grava o nome no EDID", m.Modelo.Motivo);
    }

    [Fact]
    public void Tamanho_zero_fica_nao_informado()
    {
        var edid = (byte[])Exemplo.Clone();
        edid[21] = 0;

        Assert.Equal(EstadoCampo.NaoInformado, LeitorEdid.Interpretar(edid)!.Polegadas.Estado);
    }

    [Fact]
    public void Semana_0xff_e_ano_do_modelo()
    {
        var edid = (byte[])Exemplo.Clone();
        edid[16] = 0xFF;

        Assert.Equal("ano do modelo", LeitorEdid.Interpretar(edid)!.AnoFabricacao.Motivo);
    }

    [Theory]
    [InlineData((ushort)0x4C2D, "SAM")]
    [InlineData((ushort)0x10AC, "DEL")]
    [InlineData((ushort)0x0000, null)]
    public void Codigo_de_tres_letras_do_fabricante(ushort codigo, string? esperado)
    {
        Assert.Equal(esperado, LeitorEdid.Fabricante(codigo));
    }

    [FatoWindows]
    public void Leitura_real_dos_monitores_nao_falha()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var edids = new FonteMonitoresWindows().Edids();

        Assert.All(edids, e => Assert.NotNull(LeitorEdid.Interpretar(e)));
    }
}
