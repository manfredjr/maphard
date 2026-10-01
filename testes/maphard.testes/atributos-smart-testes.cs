using MapHard.Nucleo.Tabelas;

namespace MapHard.Testes;

public class AtributosSmartTestes
{
    private const string Cabecalho = "id;nome_original;nome;tipo_disco;fonte\n";

    [Fact]
    public void Id_conhecido_da_o_nome_em_portugues_e_o_original()
    {
        var t = AtributosSmart.Ler("# comentario\n" + Cabecalho + "5;Reallocated_Sector_Ct;Setores realocados;;f\n193;Load_Cycle_Count;Ciclos de carga da cabeça;HDD;f\n");

        Assert.Equal("Setores realocados", t.Nome(5));
        Assert.Equal("Reallocated_Sector_Ct", t.Buscar(5)!.NomeOriginal);
        Assert.Null(t.Buscar(5)!.TipoDisco);
        Assert.Equal("HDD", t.Buscar(193)!.TipoDisco);
    }

    [Fact]
    public void Id_fora_da_tabela_e_atributo_do_fabricante()
    {
        Assert.Equal("atributo do fabricante (0xAA)", AtributosSmart.Ler(Cabecalho).Nome(0xAA));
    }

    [Theory]
    [InlineData("5;Reallocated_Sector_Ct;Setores realocados;;f\n5;Outro;Outro;;f\n")]
    [InlineData("5;Reallocated_Sector_Ct;;;f\n")]
    [InlineData("5;Reallocated_Sector_Ct;Setores realocados;; \n")]
    [InlineData("300;Grande;Grande;;f\n")]
    public void Linha_repetida_sem_nome_sem_fonte_ou_com_id_invalido_e_erro(string corpo)
    {
        Assert.Throws<FormatException>(() => AtributosSmart.Ler(Cabecalho + corpo));
    }

    [Fact]
    public void Tabela_embutida_e_integra_e_tem_os_atributos_das_regras_de_saude()
    {
        var t = AtributosSmart.Embutida;

        Assert.True(t.Quantidade >= 60);
        Assert.Equal("Setores realocados", t.Nome(0x05));
        Assert.Equal("Horas ligado", t.Nome(0x09));
        Assert.Equal("Temperatura", t.Nome(0xC2));
        Assert.Equal("Eventos de realocação", t.Nome(0xC4));
        Assert.Equal("Setores pendentes", t.Nome(0xC5));
        Assert.Equal("Setores incorrigíveis", t.Nome(0xC6));
    }
}
