using MapHard.Nucleo.Coleta;

namespace MapHard.Nucleo.Relatorios;

/// <summary>Formatos em que a coleta pode ser gravada.</summary>
public enum FormatoRelatorio
{
    Html,
    Json,
    Csv,
}

/// <summary>Escolhe o formato pela extensão do arquivo e grava, no padrão do MapNet.</summary>
public static class Relatorios
{
    public static string Extensao(FormatoRelatorio formato) => formato switch
    {
        FormatoRelatorio.Json => ".json",
        FormatoRelatorio.Csv => ".csv",
        _ => ".html",
    };

    /// <summary>.json e .csv pelo nome; qualquer outra extensão vira HTML.</summary>
    public static FormatoRelatorio FormatoDe(string caminho) => Path.GetExtension(caminho).ToLowerInvariant() switch
    {
        ".json" => FormatoRelatorio.Json,
        ".csv" => FormatoRelatorio.Csv,
        _ => FormatoRelatorio.Html,
    };

    /// <summary>"maphard-COMPUTADOR-aaaa-mm-dd-hhmm" com a extensão do formato.</summary>
    public static string NomePadrao(ColetaMaquina c, FormatoRelatorio formato) =>
        Path.ChangeExtension(ExportadorJson.NomePadrao(c.Identificacao.Computador.Valor ?? "computador", c.ColetadoEm), Extensao(formato));

    public static void Gravar(ColetaMaquina c, string caminho, DateOnly hoje)
    {
        switch (FormatoDe(caminho))
        {
            case FormatoRelatorio.Json:
                ExportadorJson.Gravar(c, caminho);
                break;
            case FormatoRelatorio.Csv:
                ExportadorCsv.Gravar(c, caminho);
                break;
            default:
                RelatorioHtml.Gravar(c, caminho, hoje);
                break;
        }
    }
}
