using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MapHard.Nucleo.Eventos;

/// <summary>Um evento do log Sistema, já interpretado. A mensagem é a do Windows, no idioma dele.</summary>
public sealed record EventoSistema(
    string Provedor,
    int Id,
    int Nivel,
    DateTimeOffset Momento,
    IReadOnlyDictionary<string, string> Dados,
    string? Mensagem);

/// <summary>
/// Interpreta o XML que o EvtRender devolve com EvtRenderEventXml: Event/System com Provider@Name, EventID,
/// Level e TimeCreated@SystemTime (UTC), e Event/EventData com os Data, por nome ou por posição.
/// Níveis pelo winmeta.h do SDK: 1 crítico, 2 erro, 3 aviso, 4 informação.
/// </summary>
public static class LeitorEvento
{
    public const int Critico = 1;
    public const int Erro = 2;
    public const int Aviso = 3;
    public const int Informacao = 4;

    private static readonly XNamespace _ns = "http://schemas.microsoft.com/win/2004/08/events/event";

    public static EventoSistema? Interpretar(string? xml, string? mensagem = null)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        XElement raiz;
        try
        {
            raiz = XElement.Parse(xml);
        }
        catch (XmlException)
        {
            return null;
        }

        var sistema = raiz.Element(_ns + "System");
        var provedor = sistema?.Element(_ns + "Provider")?.Attribute("Name")?.Value;
        if (sistema is null || provedor is null
            || !int.TryParse(sistema.Element(_ns + "EventID")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || !DateTimeOffset.TryParse(sistema.Element(_ns + "TimeCreated")?.Attribute("SystemTime")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var momento))
        {
            return null;
        }

        _ = int.TryParse(sistema.Element(_ns + "Level")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var nivel);

        var dados = new Dictionary<string, string>(StringComparer.Ordinal);
        var posicao = 0;
        foreach (var d in raiz.Element(_ns + "EventData")?.Elements(_ns + "Data") ?? [])
        {
            posicao++;
            dados.TryAdd(d.Attribute("Name")?.Value ?? posicao.ToString(CultureInfo.InvariantCulture), d.Value);
        }

        var texto = mensagem?.Trim();
        return new EventoSistema(provedor, id, nivel, momento, dados, string.IsNullOrEmpty(texto) ? null : texto);
    }

    /// <summary>
    /// Consulta XPath do log Sistema, como em "Consuming events" (learn.microsoft.com): cada provedor com os
    /// seus ids (sem ids, todos), ligados por "or", e o período em milissegundos pelo timediff. Filtrar o id
    /// evita que os eventos de rotina de um provedor (o Kernel-Power registra cada suspensão) gastem o limite.
    /// A página pede consulta estruturada acima de 20 expressões; aqui cada consulta leva no máximo
    /// <see cref="FiltrosPorConsulta"/> filtros.
    /// </summary>
    public static IReadOnlyList<string> Consultas(IReadOnlyList<FiltroEvento> filtros, int dias)
    {
        var milissegundos = (long)dias * 24 * 60 * 60 * 1000;
        return filtros
            .Chunk(FiltrosPorConsulta)
            .Select(grupo => $"*[System[({string.Join(" or ", grupo.Select(Expressao))}) and TimeCreated[timediff(@SystemTime) <= {milissegundos.ToString(CultureInfo.InvariantCulture)}]]]")
            .ToList();
    }

    public const int FiltrosPorConsulta = 6;

    private static string Expressao(FiltroEvento f)
    {
        var provedor = $"Provider[@Name='{f.Provedor.Replace("'", string.Empty, StringComparison.Ordinal)}']";
        return f.Ids is { Count: > 0 } ids
            ? $"({provedor} and ({string.Join(" or ", ids.Select(i => $"EventID={i.ToString(CultureInfo.InvariantCulture)}"))}))"
            : provedor;
    }
}

/// <summary>Um provedor do log Sistema e os ids que interessam. Sem ids, todos os eventos do provedor.</summary>
public sealed record FiltroEvento(string Provedor, IReadOnlyList<int>? Ids = null)
{
}
