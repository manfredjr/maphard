namespace MapHard.Nucleo.Painel;

/// <summary>Tamanho da janela ao abrir, já ajustado à área de trabalho da tela.</summary>
public sealed record TamanhoJanela(double Largura, double Altura, double LarguraMinima, double AlturaMinima)
{
    /// <summary>Folga para a borda e a barra de título do Windows, que ficam fora do tamanho do conteúdo.</summary>
    public const double Folga = 16;

    /// <summary>
    /// O WPF não encolhe a janela para caber na tela. Em tela pequena, ou com escala de 125% ou mais,
    /// a janela centralizada ficava maior que a área de trabalho, com a barra de título acima da tela,
    /// sem os botões de minimizar e maximizar. Aqui o tamanho e o mínimo ficam dentro da área livre.
    /// </summary>
    public static TamanhoJanela Ajustar(double largura, double altura, double larguraMinima, double alturaMinima,
        double areaLargura, double areaAltura)
    {
        var maxLargura = Math.Max(1, areaLargura - Folga);
        var maxAltura = Math.Max(1, areaAltura - Folga);
        var minLargura = Math.Min(larguraMinima, maxLargura);
        var minAltura = Math.Min(alturaMinima, maxAltura);
        return new TamanhoJanela(
            Math.Clamp(largura, minLargura, maxLargura),
            Math.Clamp(altura, minAltura, maxAltura),
            minLargura,
            minAltura);
    }
}
