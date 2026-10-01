using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Processador;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class ClocksTestes
{
    private sealed class ClocksSimulados : IFonteClocks
    {
        public int? NominalWindows { get; init; }

        public AmostraDesempenho? Amostra { get; init; } = new(100, [10, 30]);

        public int? ClockNominalWindowsMhz() => NominalWindows;

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
    public void Com_o_cpuid_zerado_o_base_vem_do_nominal_do_windows_e_o_maximo_do_smbios()
    {
        // Processador híbrido com o hipervisor do Windows ativo: a folha 0x16 volta zerada.
        var c = CalculoClocks.Montar(null, null, 4500, new ClocksSimulados { NominalWindows = 2500, Amostra = new(150, [50]) });

        Assert.Equal(2500, c.Base.Valor);
        Assert.Equal(FonteDado.Windows, c.Base.Fonte);
        Assert.Equal(4500, c.Maximo.Valor);
        Assert.Equal(FonteDado.Smbios, c.Maximo.Fonte);
        Assert.Equal(3750, c.Atual.Valor);
    }

    [Fact]
    public void Maximo_do_smbios_abaixo_do_base_fica_nao_informado()
    {
        var c = CalculoClocks.Montar(null, null, 2000, new ClocksSimulados { NominalWindows = 2500 });

        Assert.Equal(EstadoCampo.NaoInformado, c.Maximo.Estado);
        Assert.Contains("2000 MHz", c.Maximo.Motivo);
    }

    [Fact]
    public void Sem_cpuid_e_sem_smbios_o_maximo_fica_nao_informado()
    {
        var c = CalculoClocks.Montar(null, null, null, new ClocksSimulados { NominalWindows = 2500 });

        Assert.Equal(EstadoCampo.NaoInformado, c.Maximo.Estado);
        Assert.Equal(2500, c.Base.Valor);
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
    public void Clock_nominal_real_e_lido()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var fonte = new FonteClocksWindows();

        Assert.True(fonte.ClockNominalWindowsMhz() > 0);
    }
}
