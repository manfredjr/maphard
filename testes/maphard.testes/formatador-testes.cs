using MapHard.Nucleo.Formatacao;

namespace MapHard.Testes;

public class FormatadorTestes
{
    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1, "1 byte")]
    [InlineData(512, "512 bytes")]
    [InlineData(49152, "48 KB")]
    [InlineData(1310720, "1,25 MB")]
    [InlineData(34359738368, "32 GB")]
    [InlineData(2000398934016, "1,82 TB")]
    public void Bytes_em_pt_br(long bytes, string esperado)
    {
        Assert.Equal(esperado, Formatador.Bytes(bytes));
    }

    [Theory]
    [InlineData(800, "800 MHz")]
    [InlineData(999.6, "1000 MHz")]
    [InlineData(1000, "1,00 GHz")]
    [InlineData(3600, "3,60 GHz")]
    [InlineData(4500, "4,50 GHz")]
    public void Mhz_em_pt_br(double mhz, string esperado)
    {
        Assert.Equal(esperado, Formatador.Mhz(mhz));
    }

    [Fact]
    public void Numero_com_ponto_de_milhar()
    {
        Assert.Equal("12.345", Formatador.Numero(12345));
    }

    [Fact]
    public void Data_no_formato_brasileiro()
    {
        Assert.Equal("15/03/2021", Formatador.Data(new DateOnly(2021, 3, 15)));
    }

    [Theory]
    [InlineData(2021, 3, 15, "5 anos")]
    [InlineData(2025, 9, 30, "1 ano")]
    [InlineData(2025, 10, 1, "menos de 1 ano")]
    [InlineData(2026, 9, 30, "menos de 1 ano")]
    public void Idade_em_anos_completos(int ano, int mes, int dia, string esperado)
    {
        Assert.Equal(esperado, Formatador.IdadeEmAnos(new DateOnly(ano, mes, dia), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void Plural_simples()
    {
        Assert.Equal("1 núcleo", Formatador.Plural(1, "núcleo", "núcleos"));
        Assert.Equal("8 núcleos", Formatador.Plural(8, "núcleo", "núcleos"));
    }
}
