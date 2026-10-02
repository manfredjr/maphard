using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
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

    private static SecaoProcessador Processador(bool suporte, bool ligada, bool hyperV) =>
        DadosDemonstracao.Coleta().Processador with
        {
            VirtualizacaoNoProcessador = Campo<bool>.Lido(suporte, FonteDado.Demonstracao),
            VirtualizacaoLigada = Campo<bool>.Lido(ligada, FonteDado.Demonstracao),
            HyperVPedido = Campo<bool>.Lido(hyperV, FonteDado.Demonstracao),
        };

    [Fact]
    public void Virtualizacao_desligada_com_hyperv_pedido_e_atencao()
    {
        var s = RegrasProcessador.Processador(Processador(suporte: true, ligada: false, hyperV: true));

        Assert.Equal(EstadoSaude.Atencao, s.Estado);
        Assert.Equal(["virtualização desligada no firmware, e o Hyper-V ou o WSL está instalado: ligar no firmware"], s.Motivos);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    public void Sem_os_tres_juntos_o_processador_e_bom(bool suporte, bool ligada, bool hyperV)
    {
        Assert.Equal(EstadoSaude.Bom, RegrasProcessador.Processador(Processador(suporte, ligada, hyperV)).Estado);
    }

    [Fact]
    public void Processador_nao_lido_fica_desconhecido()
    {
        var p = Processador(suporte: true, ligada: true, hyperV: false) with { Nome = Campo<string>.Erro(FonteDado.Cpuid, "falhou") };

        Assert.Equal(EstadoSaude.Desconhecido, RegrasProcessador.Processador(p).Estado);
    }

    private sealed class RegistroCom(params string[] chaves) : MapHard.Nucleo.Firmware.IFonteRegistro
    {
        public object? Ler(string chave, string valor) => chaves.Contains(chave) && valor == "Start" ? 2 : null;
    }

    [Fact]
    public void Hyperv_pedido_pelo_servico_do_hyperv_ou_do_wsl()
    {
        Assert.True(MapHard.Nucleo.Processador.LeitorHyperV.Pedido(new RegistroCom(@"SYSTEM\CurrentControlSet\Services\vmms")).Valor);
        Assert.Equal("serviço do WSL instalado", MapHard.Nucleo.Processador.LeitorHyperV.Pedido(new RegistroCom(@"SYSTEM\CurrentControlSet\Services\WslService")).Motivo);
        Assert.False(MapHard.Nucleo.Processador.LeitorHyperV.Pedido(new RegistroCom()).Valor);
    }

    [Fact]
    public void Resumo_na_ordem_do_r1_com_a_secao_de_cada_cartao()
    {
        var cartoes = ResumoSaude.Montar(DadosDemonstracao.Coleta());

        Assert.Equal(["Discos", "Memória", "Processador", "Estabilidade", "Dispositivos", "Windows 11", "Bateria"], cartoes.Select(c => c.Area));
        Assert.Equal(
            [MontadorSecoes.Discos, MontadorSecoes.Memoria, MontadorSecoes.Processador, MontadorSecoes.Estabilidade, MontadorSecoes.Dispositivos, MontadorSecoes.Windows, MontadorSecoes.Bateria],
            cartoes.Select(c => c.Secao));
    }

    [Fact]
    public void Demonstracao_tem_os_estados_do_plano()
    {
        var estados = ResumoSaude.Montar(DadosDemonstracao.Coleta()).ToDictionary(c => c.Area, c => c.Estado);

        Assert.Equal(EstadoSaude.Atencao, estados["Discos"]);
        Assert.Equal(EstadoSaude.Atencao, estados["Memória"]);
        Assert.Equal(EstadoSaude.Bom, estados["Processador"]);
        Assert.Equal(EstadoSaude.Atencao, estados["Estabilidade"]);
        Assert.Equal(EstadoSaude.Atencao, estados["Dispositivos"]);
        Assert.Equal(EstadoSaude.Atencao, estados["Windows 11"]);
        Assert.Equal(EstadoSaude.Bom, estados["Bateria"]);
    }

    [Fact]
    public void Frase_curta_do_cartao()
    {
        Assert.Equal("nenhum problema encontrado", ResumoSaude.Frase(new SaudeArea(EstadoSaude.Bom, [])));
        Assert.Equal("3 setores realocados", ResumoSaude.Frase(new SaudeArea(EstadoSaude.Atencao, ["3 setores realocados"])));
        Assert.Equal("1 tela azul, e mais 2", ResumoSaude.Frase(new SaudeArea(EstadoSaude.Ruim, ["1 tela azul", "b", "c"])));
    }

    [Fact]
    public void Desktop_sem_bateria_fica_sem_o_cartao()
    {
        var c = DadosDemonstracao.Coleta() with { Bateria = Campo<IReadOnlyList<MapHard.Nucleo.Baterias.Bateria>>.Lido([], FonteDado.Demonstracao) };

        Assert.DoesNotContain(ResumoSaude.Montar(c), k => k.Area == "Bateria");
    }

    [Fact]
    public void Bateria_que_falhou_aparece_desconhecida()
    {
        var c = DadosDemonstracao.Coleta() with { Bateria = Campo<IReadOnlyList<MapHard.Nucleo.Baterias.Bateria>>.Erro(FonteDado.Windows, "falhou") };

        Assert.Equal(EstadoSaude.Desconhecido, ResumoSaude.Montar(c).Single(k => k.Area == "Bateria").Estado);
    }

    [Fact]
    public void Cartao_e_secao_tem_o_mesmo_motivo()
    {
        var c = DadosDemonstracao.Coleta();
        var discos = ResumoSaude.Montar(c).Single(k => k.Area == "Discos");

        Assert.Equal(c.Discos.Discos.Valor!.Where(d => d.Saude.Estado != EstadoSaude.Bom).SelectMany(d => d.Saude.Motivos.Select(m => $"Disco {d.Numero}: {m}")), discos.Motivos);
    }
}
