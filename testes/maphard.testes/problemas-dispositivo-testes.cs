using MapHard.Nucleo.Tabelas;

namespace MapHard.Testes;

public class ProblemasDispositivoTestes
{
    private const string Cabecalho = "codigo;constante;texto;fonte\n";

    [Fact]
    public void Codigo_conhecido_da_o_texto_e_a_constante()
    {
        var t = ProblemasDispositivo.Ler("# comentario\n" + Cabecalho + "28;CM_PROB_FAILED_INSTALL;O driver do dispositivo não está instalado.;f\n");

        Assert.Equal("O driver do dispositivo não está instalado.", t.Texto(28));
        Assert.Equal("CM_PROB_FAILED_INSTALL", t.Constante(28));
    }

    [Fact]
    public void Codigo_fora_da_tabela()
    {
        Assert.Equal("problema código 99", ProblemasDispositivo.Ler(Cabecalho).Texto(99));
        Assert.Null(ProblemasDispositivo.Ler(Cabecalho).Constante(99));
    }

    [Theory]
    [InlineData("28;A;texto;f\n28;B;outro;f\n")]
    [InlineData("28;A;texto; \n")]
    [InlineData("x;A;texto;f\n")]
    public void Linha_repetida_sem_fonte_ou_com_codigo_invalido_e_erro(string corpo)
    {
        Assert.Throws<FormatException>(() => ProblemasDispositivo.Ler(Cabecalho + corpo));
    }

    [Fact]
    public void Tabela_embutida_tem_os_41_codigos_da_pagina_oficial()
    {
        var t = ProblemasDispositivo.Embutida;

        Assert.Equal(41, t.Quantidade);
        Assert.Equal("O dispositivo está desativado.", t.Texto(22));
        Assert.Equal("O Windows parou o dispositivo porque ele informou problemas.", t.Texto(43));
        Assert.Equal("CM_PROB_GUEST_ASSIGNMENT_FAILED", t.Constante(57));
    }
}
