using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Processador;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class ClocksTestes
{
    private sealed class ClocksSimulados : IFonteClocks
    {
        public int? Registro { get; init; }

        public int? MaximoWindows { get; init; }

        public AmostraDesempenho? Amostra { get; init; } = new(100, [10, 30]);

        public int? ClockRegistroMhz() => Registro;

        public int? ClockMaximoWindowsMhz() => MaximoWindows;

        public AmostraDesempenho? Amostrar() => Amostra;
    }

    [Fact]
    public void Clock_atual_sai_do_base_e_do_desempenho()
    {
        var c = CalculoClocks.Montar(3600, 4700, null, new ClocksSimulados { Amostra = new(125, [50, 70]) });

        Assert.Equal(3600, c.Base.Valor);
        Assert.Equal(FonteDado.Cpuid, c.Base.Fonte);
        Assert.Equal(4700, c.Maximo.Valor);
        Assert.Equal(4500, c.Atual.Valor);
        Assert.Equal(60, c.UsoTotal.Valor);
        Assert.Equal([50.0, 70.0], c.UsoPorProcessador.Valor!);
    }

    [Fact]
    public void Sem_cpuid_o_base_vem_do_registro_e_o_maximo_do_windows()
    {
        var c = CalculoClocks.Montar(null, null, 3900, new ClocksSimulados { Registro = 2904, MaximoWindows = 2904 });

        Assert.Equal(FonteDado.Registro, c.Base.Fonte);
        Assert.Equal(2904, c.Base.Valor);
        Assert.Equal(FonteDado.Windows, c.Maximo.Fonte);
    }

    [Fact]
    public void Ultimo_recurso_do_maximo_e_o_smbios()
    {
        var c = CalculoClocks.Montar(null, null, 3900, new ClocksSimulados());

        Assert.Equal(3900, c.Maximo.Valor);
        Assert.Equal(FonteDado.Smbios, c.Maximo.Fonte);
    }

    [Fact]
    public void Sem_base_o_atual_fica_nao_informado()
    {
        var c = CalculoClocks.Montar(null, 4700, null, new ClocksSimulados());

        Assert.Equal(EstadoCampo.NaoInformado, c.Base.Estado);
        Assert.Equal(EstadoCampo.NaoInformado, c.Atual.Estado);
        Assert.Equal(4700, c.Maximo.Valor);
    }

    [Fact]
    public void Contador_indisponivel_vira_erro_de_leitura_com_motivo()
    {
        var c = CalculoClocks.Montar(3600, null, null, new ClocksSimulados { Amostra = null });

        Assert.Equal(EstadoCampo.ErroLeitura, c.Atual.Estado);
        Assert.Equal(EstadoCampo.ErroLeitura, c.UsoTotal.Estado);
        Assert.Contains("contador", c.Atual.Motivo);
        Assert.Equal(3600, c.Base.Valor);
    }

    [Fact]
    public void Amostra_sem_processadores_deixa_o_uso_nao_informado()
    {
        var c = CalculoClocks.Montar(3600, null, null, new ClocksSimulados { Amostra = new(100, []) });

        Assert.Equal(EstadoCampo.NaoInformado, c.UsoTotal.Estado);
        Assert.Equal(3600, c.Atual.Valor);
    }

    [FatoWindows]
    public void Clock_real_tem_base_ou_maximo()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var fonte = new FonteClocksWindows();

        Assert.True(fonte.ClockRegistroMhz() > 0 || fonte.ClockMaximoWindowsMhz() > 0);
    }
}
