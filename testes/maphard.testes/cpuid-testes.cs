using MapHard.Nucleo.Cpuid;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class CpuidTestes
{
    [Theory]
    [InlineData(0x000906EAu, 0x6, 0x9E, 0xA)]
    [InlineData(0x00A20F10u, 0x19, 0x21, 0x0)]
    [InlineData(0x00800F82u, 0x17, 0x08, 0x2)]
    [InlineData(0x000006F6u, 0x6, 0x0F, 0x6)]
    public void Assinatura(uint eax, int familia, int modelo, int revisao)
    {
        Assert.Equal((familia, modelo, revisao), DecodificadorCpuid.Assinatura(eax));
    }

    [Fact]
    public void Modelo_estendido_nao_entra_fora_das_familias_6_e_f()
    {
        // Família 5 com modelo estendido 1: o modelo exibido fica só com os 4 bits baixos.
        Assert.Equal((5, 0x4, 0x3), DecodificadorCpuid.Assinatura(0x00010543));
    }

    [Theory]
    [InlineData("GenuineIntel", "Intel")]
    [InlineData("AuthenticAMD", "AMD")]
    [InlineData("OutroFabric", "OutroFabric")]
    public void Fabricante(string texto, string nome)
    {
        var id = DecodificadorCpuid.Decodificar(CpuidSimulado.Base(texto))!;

        Assert.Equal(texto, id.Fabricante);
        Assert.Equal(nome, id.NomeFabricante);
    }

    [Fact]
    public void Nome_comercial_sem_espacos_nas_pontas_nem_repetidos()
    {
        var id = DecodificadorCpuid.Decodificar(CpuidSimulado.Base().NomeComercial("      Processador   de Teste 3.60GHz  "))!;

        Assert.Equal("Processador de Teste 3.60GHz", id.NomeComercial);
    }

    [Fact]
    public void Sem_as_funcoes_estendidas_o_nome_comercial_fica_nulo()
    {
        var id = DecodificadorCpuid.Decodificar(CpuidSimulado.Base(maiorEstendida: 0x80000001).NomeComercial("Nao deve aparecer"))!;

        Assert.Null(id.NomeComercial);
    }

    [Theory]
    [InlineData(1u, 'd', 23, "MMX")]
    [InlineData(1u, 'd', 25, "SSE")]
    [InlineData(1u, 'd', 26, "SSE2")]
    [InlineData(1u, 'c', 0, "SSE3")]
    [InlineData(1u, 'c', 9, "SSSE3")]
    [InlineData(1u, 'c', 19, "SSE4.1")]
    [InlineData(1u, 'c', 20, "SSE4.2")]
    [InlineData(0x80000001u, 'd', 29, "x86-64")]
    [InlineData(1u, 'c', 25, "AES")]
    [InlineData(1u, 'c', 28, "AVX")]
    [InlineData(7u, 'b', 5, "AVX2")]
    [InlineData(1u, 'c', 12, "FMA3")]
    [InlineData(7u, 'b', 29, "SHA")]
    [InlineData(7u, 'b', 16, "AVX-512F")]
    [InlineData(1u, 'c', 5, "VT-x")]
    [InlineData(0x80000001u, 'c', 2, "AMD-V")]
    public void Cada_bit_liga_so_a_sua_instrucao(uint funcao, char registrador, int posicao, string instrucao)
    {
        var id = DecodificadorCpuid.Decodificar(CpuidSimulado.Base().Bit(funcao, registrador, posicao))!;

        Assert.Equal([instrucao], id.Instrucoes);
    }

    [Fact]
    public void Nivel_x86_64_de_1_a_4()
    {
        var nivel1 = CpuidSimulado.Base().Bit(0x80000001, 'd', 29);
        Assert.Equal(1, DecodificadorCpuid.Decodificar(nivel1)!.NivelX8664);

        var nivel2 = Nivel2(CpuidSimulado.Base());
        Assert.Equal(2, DecodificadorCpuid.Decodificar(nivel2)!.NivelX8664);

        var nivel3 = Nivel3(Nivel2(CpuidSimulado.Base()));
        Assert.Equal(3, DecodificadorCpuid.Decodificar(nivel3)!.NivelX8664);

        var nivel4 = Nivel3(Nivel2(CpuidSimulado.Base()))
            .Bit(7, 'b', 16).Bit(7, 'b', 17).Bit(7, 'b', 28).Bit(7, 'b', 30).Bit(7, 'b', 31);
        Assert.Equal(4, DecodificadorCpuid.Decodificar(nivel4)!.NivelX8664);
    }

    [Fact]
    public void Falta_do_movbe_deixa_no_nivel_2()
    {
        var semMovbe = Nivel2(CpuidSimulado.Base())
            .Bit(1, 'c', 28).Bit(7, 'b', 5).Bit(7, 'b', 3).Bit(7, 'b', 8).Bit(1, 'c', 29).Bit(1, 'c', 12)
            .Bit(0x80000001, 'c', 5).Bit(1, 'c', 27);

        Assert.Equal(2, DecodificadorCpuid.Decodificar(semMovbe)!.NivelX8664);
    }

    [Fact]
    public void Sem_x86_64_o_nivel_fica_nulo()
    {
        Assert.Null(DecodificadorCpuid.Decodificar(CpuidSimulado.Base())!.NivelX8664);
    }

    [Fact]
    public void Virtualizacao_hipervisor_e_hibrido()
    {
        var id = DecodificadorCpuid.Decodificar(CpuidSimulado.Base().Bit(1, 'c', 5).Bit(1, 'c', 31).Bit(7, 'd', 15))!;

        Assert.True(id.VirtualizacaoNoProcessador);
        Assert.True(id.HipervisorPresente);
        Assert.True(id.Hibrido);
    }

    [Fact]
    public void Amd_v_conta_como_virtualizacao()
    {
        var id = DecodificadorCpuid.Decodificar(CpuidSimulado.Base("AuthenticAMD", 0x00A20F10).Bit(0x80000001, 'c', 2))!;

        Assert.True(id.VirtualizacaoNoProcessador);
        Assert.False(id.HipervisorPresente);
    }

    [Fact]
    public void Processador_sem_a_funcao_7_nao_informa_hibrido_nem_instrucoes_dela()
    {
        var id = DecodificadorCpuid.Decodificar(CpuidSimulado.Base(maiorBasica: 1).Bit(7, 'b', 5).Bit(7, 'd', 15))!;

        Assert.Null(id.Hibrido);
        Assert.DoesNotContain("AVX2", id.Instrucoes);
    }

    [Fact]
    public void Clock_base_e_maximo_pela_funcao_16()
    {
        var id = DecodificadorCpuid.Decodificar(CpuidSimulado.Base(maiorBasica: 0x16).Com(0x16, 3600, 4700, 100))!;

        Assert.Equal(3600, id.ClockBaseMhz);
        Assert.Equal(4700, id.ClockMaximoMhz);
    }

    [Fact]
    public void Sem_a_funcao_16_o_clock_fica_nulo()
    {
        var id = DecodificadorCpuid.Decodificar(CpuidSimulado.Base().Com(0x16, 3600, 4700))!;

        Assert.Null(id.ClockBaseMhz);
    }

    [Fact]
    public void Fonte_indisponivel_devolve_nulo()
    {
        Assert.Null(DecodificadorCpuid.Decodificar(new CpuidSimulado { Disponivel = false }));
    }

    [FatoWindows]
    public void Fabricante_real_nao_e_vazio()
    {
        var id = DecodificadorCpuid.Decodificar(new FonteCpuidReal());

        Assert.NotNull(id);
        Assert.False(string.IsNullOrWhiteSpace(id.Fabricante));
    }

    private static CpuidSimulado Nivel2(CpuidSimulado c) => c
        .Bit(0x80000001, 'd', 29)
        .Bit(1, 'c', 13).Bit(0x80000001, 'c', 0).Bit(1, 'c', 23)
        .Bit(1, 'c', 0).Bit(1, 'c', 9).Bit(1, 'c', 19).Bit(1, 'c', 20);

    private static CpuidSimulado Nivel3(CpuidSimulado c) => c
        .Bit(1, 'c', 28).Bit(7, 'b', 5).Bit(7, 'b', 3).Bit(7, 'b', 8).Bit(1, 'c', 29).Bit(1, 'c', 12)
        .Bit(0x80000001, 'c', 5).Bit(1, 'c', 22).Bit(1, 'c', 27);
}
