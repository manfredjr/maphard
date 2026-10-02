using System.Reflection;
using System.Text;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Painel;

namespace MapHard.Nucleo.Relatorios;

/// <summary>
/// Relatório HTML de arquivo único para o cliente (R38): estilo e logo da MT dentro do próprio arquivo, sem nada
/// buscado na internet. Os cartões de saúde do Resumo ficam na primeira página e as outras seções vêm depois, na
/// ordem da navegação, com as mesmas linhas da tela. Todo texto passa por codificação HTML, porque nome de
/// computador e modelo são escolhidos por quem configurou a máquina. Imprime em PDF pelo navegador.
/// </summary>
public static class RelatorioHtml
{
    private static readonly Lazy<string> _logo = new(() =>
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream("mt-logo.png")
            ?? throw new InvalidOperationException("Logo da MT não encontrado no programa.");
        using var memoria = new MemoryStream();
        fluxo.CopyTo(memoria);
        return Convert.ToBase64String(memoria.ToArray());
    });

    public static string NomePadrao(ColetaMaquina c) =>
        Path.ChangeExtension(ExportadorJson.NomePadrao(c.Identificacao.Computador.Valor ?? "computador", c.ColetadoEm), ".html");

    public static void Gravar(ColetaMaquina c, string caminho, DateOnly hoje) =>
        File.WriteAllText(caminho, Gerar(c, hoje), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    public static string Gerar(ColetaMaquina c, DateOnly hoje)
    {
        var secoes = MontadorSecoes.Montar(c, hoje);
        var computador = c.Identificacao.Computador.Valor ?? "computador";
        var html = new StringBuilder(64 * 1024);
        html.Append($$"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="generator" content="MapHard - MT {{C(c.VersaoPrograma)}}">
            <title>MapHard - {{C(computador)}} - {{C(Formatador.DataHora(c.ColetadoEm))}}</title>
            <style>{{Estilo}}</style>
            </head>
            <body>
            <header>
              <img class="logo" alt="MT - Manfred Tecnologia" src="data:image/png;base64,{{_logo.Value}}">
              <div>
                <div class="marca">MapHard - MT</div>
                <h1>{{C(computador)}}</h1>
                <p class="sub">Coletado em {{C(Formatador.DataHora(c.ColetadoEm))}}{{(c.Administrador ? ", como administrador" : ", como usuário comum")}}</p>
              </div>
            </header>
            <main>
            """);

        var primeira = true;
        foreach (var secao in secoes)
        {
            html.Append(primeira ? "<section class=\"secao\">" : "<section class=\"secao nova-pagina\">");
            html.Append($"<h2>{C(secao.Titulo)}</h2>");
            foreach (var cartao in secao.Cartoes)
            {
                html.Append($"<div class=\"cartao{(cartao.Abre is null ? string.Empty : " saude")}\"><h3>{C(cartao.Titulo)}");
                if (cartao.Selo is { } selo)
                {
                    html.Append($" <span class=\"selo {C(cartao.TomSelo ?? "desconhecido")}\">{C(selo)}</span>");
                }

                html.Append("</h3><table>");
                foreach (var linha in cartao.Linhas)
                {
                    html.Append($"<tr><th>{C(linha.Rotulo)}</th><td{(linha.Lido ? string.Empty : " class=\"sem-leitura\"")}>{C(linha.Texto)}</td></tr>");
                }

                html.Append("</table></div>");
            }

            html.Append("</section>");
            primeira = false;
        }

        html.Append($$"""
            </main>
            <footer>Gerado pelo MapHard - MT {{C(c.VersaoPrograma)}}. O MapHard só lê: não altera nada no computador.</footer>
            </body>
            </html>
            """);
        return html.ToString();
    }

    /// <summary>
    /// Só os cinco caracteres que mudam o sentido do HTML. O WebUtility.HtmlEncode troca também os acentos por
    /// números, o que deixa o arquivo ilegível para quem abre no Bloco de Notas, sem ganho nenhum em UTF-8.
    /// </summary>
    internal static string C(string texto) => texto
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&#39;", StringComparison.Ordinal);

    private const string Estilo = """
        :root { --verde-escuro: #006B2D; --verde: #0F8F2F; --verde-claro: #43A92C; --verde-limao: #9AD52B; --grafite: #202020; --atencao: #B7791F; --ruim: #B42318; --suave: #6B6B6B; --borda: #DADADA; }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: Montserrat, "Segoe UI", Arial, sans-serif; color: var(--grafite); background: #F4F6F4; font-size: 14px; }
        header { display: flex; align-items: center; gap: 18px; padding: 18px 28px; color: white; background: linear-gradient(90deg, var(--verde-escuro), var(--verde) 60%, var(--verde-claro)); }
        header .logo { height: 54px; background: white; border-radius: 8px; padding: 6px; }
        header .marca { font-weight: 800; letter-spacing: 0.5px; }
        header h1 { margin: 2px 0; font-size: 24px; }
        header .sub { margin: 0; opacity: 0.9; }
        main { padding: 18px 28px; max-width: 1100px; }
        h2 { color: var(--verde-escuro); border-bottom: 3px solid var(--verde-limao); padding-bottom: 4px; margin: 24px 0 12px; }
        .cartao { background: white; border: 1px solid var(--borda); border-radius: 10px; padding: 12px 16px; margin-bottom: 12px; break-inside: avoid; }
        .cartao.saude { border-left: 6px solid var(--verde); }
        h3 { margin: 0 0 8px; font-size: 16px; }
        .selo { display: inline-block; color: white; background: var(--suave); border-radius: 12px; padding: 2px 12px; font-size: 13px; margin-left: 8px; }
        .selo.bom { background: var(--verde); }
        .selo.atencao { background: var(--atencao); }
        .selo.ruim { background: var(--ruim); }
        table { border-collapse: collapse; width: 100%; }
        th { text-align: left; font-weight: normal; color: var(--suave); width: 34%; padding: 3px 12px 3px 0; vertical-align: top; }
        td { font-weight: 600; padding: 3px 0; }
        td.sem-leitura { font-weight: normal; font-style: italic; color: var(--suave); }
        footer { padding: 14px 28px 24px; color: var(--suave); font-size: 12px; }
        @media print {
          body { background: white; }
          header { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
          .selo { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
          .nova-pagina { break-before: page; }
          main { padding: 0 12px; }
        }
        """;
}
