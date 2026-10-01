using MapHard.Nucleo.Processador;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class TopologiaTestes
{
    [Fact]
    public void Quatro_nucleos_com_duas_threads_sem_classes_de_eficiencia()
    {
        var c = new ConstrutorTopologia().Pacote();
        for (var i = 0; i < 4; i++)
        {
            c.Nucleo(2);
        }

        var t = LeitorTopologia.Interpretar(c.Montar())!;

        Assert.Equal(1, t.Pacotes);
        Assert.Equal(4, t.Nucleos);
        Assert.Equal(8, t.Threads);
        Assert.Equal(4, t.NucleosDesempenho);
        Assert.Equal(0, t.NucleosEficiencia);
        Assert.False(t.Hibrido);
    }

    [Fact]
    public void Processador_hibrido_separa_desempenho_e_eficiencia()
    {
        var c = new ConstrutorTopologia().Pacote();
        for (var i = 0; i < 6; i++)
        {
            c.Nucleo(2, eficiencia: 1);
        }

        for (var i = 0; i < 8; i++)
        {
            c.Nucleo(1, eficiencia: 0);
        }

        var t = LeitorTopologia.Interpretar(c.Montar())!;

        Assert.Equal(14, t.Nucleos);
        Assert.Equal(20, t.Threads);
        Assert.Equal(6, t.NucleosDesempenho);
        Assert.Equal(8, t.NucleosEficiencia);
        Assert.True(t.Hibrido);
    }

    [Fact]
    public void Caches_agrupados_por_nivel_e_tipo()
    {
        var c = new ConstrutorTopologia().Pacote().Nucleo(2).Nucleo(2);
        c.Cache(1, 2, 48 * 1024).Cache(1, 1, 32 * 1024).Cache(2, 0, 1280 * 1024)
         .Cache(1, 2, 48 * 1024).Cache(1, 1, 32 * 1024).Cache(2, 0, 1280 * 1024)
         .Cache(3, 0, 24 * 1024 * 1024, associatividade: 12);

        var caches = LeitorTopologia.Interpretar(c.Montar())!.Caches;

        Assert.Equal(["L1 de dados", "L1 de instruções", "L2", "L3"], caches.Select(x => x.Rotulo));
        Assert.Equal(2, caches[0].Unidades);
        Assert.Equal(48 * 1024, caches[0].TamanhoPorUnidade);
        Assert.Equal(96 * 1024, caches[0].TamanhoTotal);
        Assert.Equal(1, caches[3].Unidades);
        Assert.Equal(12, caches[3].Associatividade);
        Assert.Equal(64, caches[3].TamanhoLinha);
    }

    [Fact]
    public void Processador_hibrido_separa_os_caches_de_tamanhos_diferentes()
    {
        // 4 núcleos de desempenho e 8 de eficiência, com o L2 dos de eficiência dividido entre 4 núcleos.
        var c = new ConstrutorTopologia().Pacote();
        for (var i = 0; i < 4; i++)
        {
            c.Nucleo(2, eficiencia: 1).Cache(1, 2, 48 * 1024).Cache(1, 1, 32 * 1024).Cache(2, 0, 1280 * 1024, associatividade: 10);
        }

        for (var i = 0; i < 8; i++)
        {
            c.Nucleo(1, eficiencia: 0).Cache(1, 2, 32 * 1024).Cache(1, 1, 64 * 1024);
        }

        c.Cache(2, 0, 2048 * 1024, associatividade: 16).Cache(2, 0, 2048 * 1024, associatividade: 16).Cache(3, 0, 18 * 1024 * 1024);

        var caches = LeitorTopologia.Interpretar(c.Montar())!.Caches;

        Assert.Equal(["L1 de dados", "L1 de dados", "L1 de instruções", "L1 de instruções", "L2", "L2", "L3"], caches.Select(x => x.Rotulo));
        Assert.Equal(448 * 1024, caches.Where(x => x.Rotulo == "L1 de dados").Sum(x => x.TamanhoTotal));
        Assert.Equal(640 * 1024, caches.Where(x => x.Rotulo == "L1 de instruções").Sum(x => x.TamanhoTotal));
        Assert.Equal(9 * 1024 * 1024, caches.Where(x => x.Rotulo == "L2").Sum(x => x.TamanhoTotal));
        Assert.Equal((4, 48 * 1024), (caches[0].Unidades, caches[0].TamanhoPorUnidade));
        Assert.Equal((8, 32 * 1024), (caches[1].Unidades, caches[1].TamanhoPorUnidade));
        Assert.Equal(16, caches[5].Associatividade);
    }

    [Fact]
    public void Cache_totalmente_associativo_fica_sem_numero()
    {
        var c = new ConstrutorTopologia().Nucleo(1).Cache(1, 2, 32 * 1024, associatividade: 0xFF);

        Assert.Null(LeitorTopologia.Interpretar(c.Montar())!.Caches[0].Associatividade);
    }

    [Fact]
    public void Registro_desconhecido_e_pulado_pelo_tamanho()
    {
        var c = new ConstrutorTopologia().Desconhecido(4, 40).Nucleo(2).Desconhecido(1, 24).Nucleo(2);

        var t = LeitorTopologia.Interpretar(c.Montar())!;

        Assert.Equal(2, t.Nucleos);
        Assert.Equal(4, t.Threads);
    }

    [Fact]
    public void Buffer_truncado_usa_o_que_ficou_inteiro()
    {
        var bruta = new ConstrutorTopologia().Nucleo(2).Nucleo(2).Montar();

        var t = LeitorTopologia.Interpretar(bruta[..(bruta.Length - 10)])!;

        Assert.Equal(1, t.Nucleos);
    }

    [Fact]
    public void Sem_pacote_conta_um()
    {
        Assert.Equal(1, LeitorTopologia.Interpretar(new ConstrutorTopologia().Nucleo(1).Montar())!.Pacotes);
    }

    [Fact]
    public void Sem_nucleos_devolve_nulo()
    {
        Assert.Null(LeitorTopologia.Interpretar(new ConstrutorTopologia().Pacote().Montar()));
        Assert.Null(LeitorTopologia.Interpretar(null));
        Assert.Null(LeitorTopologia.Interpretar([1, 2, 3]));
    }

    [Fact]
    public void Tamanho_de_registro_invalido_encerra_a_leitura()
    {
        var bruta = new ConstrutorTopologia().Nucleo(2).Montar();
        bruta[4] = 4;

        Assert.Null(LeitorTopologia.Interpretar(bruta));
    }

    [FatoWindows]
    public void Topologia_real_tem_nucleos_e_threads()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var t = LeitorTopologia.Interpretar(new FonteTopologiaWindows().LerBruta());

        Assert.NotNull(t);
        Assert.True(t.Nucleos >= 1);
        Assert.True(t.Threads >= t.Nucleos);
    }
}
