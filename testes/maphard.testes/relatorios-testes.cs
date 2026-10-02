using MapHard.Nucleo.Painel;
using MapHard.Nucleo.Relatorios;

namespace MapHard.Testes;

public class RelatoriosTestes
{
    private static readonly DateOnly Hoje = new(2026, 10, 2);

    [Fact]
    public void Texto_da_secao_tem_titulo_cartoes_e_linhas()
    {
        var c = DadosDemonstracao.Coleta();
        var secao = MontadorSecoes.Montar(c, Hoje).Single(s => s.Id == MontadorSecoes.Bateria);

        var texto = TextoSecao.Gerar(secao, c);

        Assert.StartsWith("MapHard - MT: Bateria\r\nESTACAO-EXEMPLO, coletado em 30/09/2026 10:00\r\n", texto, StringComparison.Ordinal);
        Assert.Contains("Bateria [Bom]\r\n", texto, StringComparison.Ordinal);
        Assert.Contains("  Desgaste: 12%\r\n", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Campo_nao_lido_sai_com_o_texto_do_estado()
    {
        var c = DadosDemonstracao.Coleta();
        var secao = MontadorSecoes.Montar(c, Hoje).Single(s => s.Id == MontadorSecoes.Processador);

        Assert.Contains("  Litografia: não informado pelo fabricante", TextoSecao.Gerar(secao, c), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Copia_avisa_na_barra_de_status()
    {
        var painel = PainelPrincipal.ComDemonstracao();
        await painel.AtualizarAsync();

        painel.AvisarCopia("Bateria");

        Assert.EndsWith("seção Bateria copiada", painel.TextoStatus, StringComparison.Ordinal);
    }

    private static string Html(MapHard.Nucleo.Coleta.ColetaMaquina? c = null) => RelatorioHtml.Gerar(c ?? DadosDemonstracao.Coleta(), Hoje);

    [Fact]
    public void Html_e_um_documento_em_portugues()
    {
        var html = Html();

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"pt-BR\">", html, StringComparison.Ordinal);
        Assert.Contains("<meta charset=\"utf-8\">", html, StringComparison.Ordinal);
        Assert.Contains("Gerado pelo MapHard - MT", html, StringComparison.Ordinal);
        Assert.Contains("O MapHard só lê: não altera nada no computador.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_e_arquivo_unico_sem_nada_da_internet()
    {
        var html = Html();

        Assert.DoesNotContain("src=\"http", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("src=\"data:image/png;base64,iVBOR", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_comeca_pelo_resumo_e_traz_todas_as_secoes_com_os_textos_da_tela()
    {
        var c = DadosDemonstracao.Coleta();
        var html = Html(c);
        var secoes = MontadorSecoes.Montar(c, Hoje);

        var posicoes = secoes.Select(s => html.IndexOf($"<h2>{RelatorioHtml.C(s.Titulo)}</h2>", StringComparison.Ordinal)).ToList();
        Assert.All(posicoes, p => Assert.True(p > 0));
        Assert.Equal(posicoes.Order(), posicoes);
        Assert.Equal(secoes.Count - 1, System.Text.RegularExpressions.Regex.Matches(html, "nova-pagina\"").Count);
        Assert.Contains("<span class=\"selo atencao\">Atenção</span>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"sem-leitura\">não informado pelo fabricante</td>", html, StringComparison.Ordinal);
        foreach (var linha in secoes.SelectMany(s => s.Cartoes).SelectMany(k => k.Linhas))
        {
            Assert.Contains(RelatorioHtml.C(linha.Texto), html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Texto_da_maquina_e_codificado()
    {
        var c = DadosDemonstracao.Coleta();
        c = c with { Identificacao = c.Identificacao with { Computador = MapHard.Nucleo.Campos.Campo<string>.Lido("<script>alert(1)</script>", MapHard.Nucleo.Campos.FonteDado.Windows) } };

        var html = Html(c);

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Nome_padrao_do_html()
    {
        Assert.Equal("maphard-ESTACAO-EXEMPLO-2026-09-30-1000.html", RelatorioHtml.NomePadrao(DadosDemonstracao.Coleta()));
    }
}
