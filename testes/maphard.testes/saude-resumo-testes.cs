using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Painel;
using MapHard.Nucleo.Saude;

namespace MapHard.Testes;

public class SaudeResumoTestes
{
    private static readonly DiscoTela Modelo = DadosDemonstracao.Coleta().Discos.Discos.Valor![0];

    private static SecaoDiscos Discos(params (int Numero, EstadoSaude Estado, string[] Motivos)[] discos) =>
        new(Campo<IReadOnlyList<DiscoTela>>.Lido(discos.Select(d => Modelo with { Numero = d.Numero, Saude = new SaudeDisco(d.Estado, d.Motivos) }).ToList(), FonteDado.Demonstracao), []);

    [Fact]
    public void Conjunto_dos_discos_fica_com_o_pior()
    {
        var s = RegrasDisco.Conjunto(Discos((0, EstadoSaude.Bom, []), (1, EstadoSaude.Atencao, ["3 setores realocados"])));

        Assert.Equal(EstadoSaude.Atencao, s.Estado);
        Assert.Equal(["Disco 1: 3 setores realocados"], s.Motivos);
    }

    [Fact]
    public void Disco_sem_smart_deixa_o_conjunto_desconhecido_so_quando_nenhum_esta_pior()
    {
        Assert.Equal(EstadoSaude.Desconhecido, RegrasDisco.Conjunto(Discos((0, EstadoSaude.Bom, []), (1, EstadoSaude.Desconhecido, ["requer administrador"]))).Estado);

        var s = RegrasDisco.Conjunto(Discos((0, EstadoSaude.Desconhecido, ["requer administrador"]), (1, EstadoSaude.Ruim, ["falha prevista pelo disco"])));
        Assert.Equal(EstadoSaude.Ruim, s.Estado);
        Assert.Equal(["Disco 1: falha prevista pelo disco", "Disco 0: requer administrador"], s.Motivos);
    }

    [Fact]
    public void Todos_bons_e_bom_sem_motivo()
    {
        var s = RegrasDisco.Conjunto(Discos((0, EstadoSaude.Bom, []), (1, EstadoSaude.Bom, [])));

        Assert.Equal(EstadoSaude.Bom, s.Estado);
        Assert.Empty(s.Motivos);
    }

    [Fact]
    public void Lista_de_discos_nao_lida_ou_vazia_fica_desconhecida()
    {
        var s = RegrasDisco.Conjunto(new SecaoDiscos(Campo<IReadOnlyList<DiscoTela>>.Erro(FonteDado.Armazenamento, "lista de discos indisponível"), []));

        Assert.Equal(EstadoSaude.Desconhecido, s.Estado);
        Assert.Equal(["lista de discos indisponível"], s.Motivos);
        Assert.Equal(EstadoSaude.Desconhecido, RegrasDisco.Conjunto(Discos()).Estado);
    }
}
