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
}
