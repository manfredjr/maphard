using MapHard.Nucleo.Painel;

namespace MapHard.Testes;

public class TamanhoJanelaTestes
{
    [Fact]
    public void Tela_grande_mantem_o_tamanho_pedido()
    {
        var t = TamanhoJanela.Ajustar(1100, 760, 760, 480, 1920, 1032);

        Assert.Equal((1100d, 760d, 760d, 480d), (t.Largura, t.Altura, t.LarguraMinima, t.AlturaMinima));
    }

    [Fact]
    public void Tela_1366x768_com_escala_de_125_cabe_na_area_de_trabalho()
    {
        // 1366 x 728 pixels livres em 125% viram 1092,8 x 582,4 unidades do WPF
        var t = TamanhoJanela.Ajustar(1100, 760, 760, 480, 1092.8, 582.4);

        Assert.True(t.Largura <= 1092.8);
        Assert.True(t.Altura <= 582.4);
        Assert.Equal(760, t.LarguraMinima);
        Assert.Equal(480, t.AlturaMinima);
    }

    [Fact]
    public void Tela_menor_que_o_minimo_reduz_tambem_o_minimo()
    {
        var t = TamanhoJanela.Ajustar(1100, 760, 760, 480, 700, 400);

        Assert.True(t.Largura <= 700 && t.LarguraMinima <= t.Largura);
        Assert.True(t.Altura <= 400 && t.AlturaMinima <= t.Altura);
    }
}
