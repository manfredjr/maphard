using MapHard.Nucleo.Tabelas;

namespace MapHard.Testes;

public class TabelaProcessadoresTestes
{
    private const string Cabecalho = "fabricante;familia;modelo_inicial;modelo_final;revisoes;nome;litografia;fonte\n";

    [Fact]
    public void Le_linha_valida_e_ignora_comentario()
    {
        var tabela = TabelaProcessadores.Ler("# comentario\n" + Cabecalho + "GenuineIntel;6;9E;9E;10-13;Coffee Lake;14 nm;fonte X\n");

        var linha = Assert.Single(tabela.Linhas);
        Assert.Equal(6, linha.Familia);
        Assert.Equal(0x9E, linha.ModeloInicial);
        Assert.Equal([10, 11, 12, 13], linha.Revisoes!.Order());
        Assert.Equal("14 nm", linha.Litografia);
    }

    [Fact]
    public void Linha_com_campo_faltando_e_erro()
    {
        Assert.Throws<FormatException>(() => TabelaProcessadores.Ler(Cabecalho + "GenuineIntel;6;9E;9E;;Coffee Lake;fonte\n"));
    }

    [Fact]
    public void Linha_sem_fonte_e_erro()
    {
        Assert.Throws<FormatException>(() => TabelaProcessadores.Ler(Cabecalho + "GenuineIntel;6;9E;9E;;Coffee Lake;; \n"));
    }

    [Fact]
    public void Busca_prefere_a_linha_com_revisao()
    {
        var tabela = TabelaProcessadores.Ler(Cabecalho
            + "GenuineIntel;6;9E;9E;;Geral;;f\n"
            + "GenuineIntel;6;9E;9E;9;Especifica;;f\n");

        Assert.Equal("Especifica", tabela.Buscar("GenuineIntel", 6, 0x9E, 9)!.Nome);
        Assert.Equal("Geral", tabela.Buscar("GenuineIntel", 6, 0x9E, 3)!.Nome);
    }

    [Fact]
    public void Busca_por_faixa_de_modelo_e_sem_resultado()
    {
        var tabela = TabelaProcessadores.Ler(Cabecalho + "AuthenticAMD;19;20;5F;;Zen 3;;f\n");

        Assert.Equal("Zen 3", tabela.Buscar("AuthenticAMD", 0x19, 0x21, 0)!.Nome);
        Assert.Null(tabela.Buscar("AuthenticAMD", 0x19, 0x60, 0));
        Assert.Null(tabela.Buscar("GenuineIntel", 0x19, 0x21, 0));
    }

    [Fact]
    public void Tabela_embutida_tem_fonte_em_toda_linha_e_sem_repeticao()
    {
        var linhas = TabelaProcessadores.Embutida.Linhas;

        Assert.NotEmpty(linhas);
        Assert.All(linhas, l => Assert.False(string.IsNullOrWhiteSpace(l.Fonte)));
        Assert.All(linhas, l => Assert.True(l.ModeloInicial <= l.ModeloFinal, l.Nome));

        // Nenhum processador pode cair em duas linhas do mesmo nível de detalhe.
        var conflitos = linhas
            .SelectMany(l => Enumerable.Range(l.ModeloInicial, l.ModeloFinal - l.ModeloInicial + 1)
                .SelectMany(m => (l.Revisoes ?? new HashSet<int> { -1 }).Select(r => (l.Fabricante, l.Familia, m, r))))
            .GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.ToString())
            .ToList();
        Assert.True(conflitos.Count == 0, string.Join(", ", conflitos));
    }

    [Theory]
    [InlineData("GenuineIntel", 0x6, 0x9E, 0xA, "Coffee Lake")]
    [InlineData("GenuineIntel", 0x6, 0x9E, 0x9, "Kaby Lake")]
    [InlineData("GenuineIntel", 0x6, 0x97, 0x2, "Alder Lake")]
    [InlineData("AuthenticAMD", 0x19, 0x21, 0x0, "Zen 3")]
    [InlineData("AuthenticAMD", 0x17, 0x71, 0x0, "Zen 2")]
    public void Tabela_embutida_acha_processadores_conhecidos(string fabricante, int familia, int modelo, int revisao, string nome)
    {
        Assert.Equal(nome, TabelaProcessadores.Embutida.Buscar(fabricante, familia, modelo, revisao)?.Nome);
    }
}
