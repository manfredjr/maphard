using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Windows11;

namespace MapHard.Testes;

public class Windows11Testes
{
    private static readonly TabelaWindows11 Tabela = TabelaWindows11.Embutida;

    [Theory]
    [InlineData("12th Gen Intel(R) Core(TM) i5-12500H", "12th Generation Core i5 Processors")]
    [InlineData("Intel(R) Core(TM) i7-8550U CPU @ 1.80GHz", "8th Generation Core i7 Processors")]
    [InlineData("11th Gen Intel(R) Core(TM) i5-1135G7 @ 2.40GHz", "11th Generation Core i5 Processors")]
    [InlineData("Intel(R) Core(TM) i7-10510U CPU @ 1.80GHz", "10th Generation Core i7 Processors")]
    [InlineData("Intel(R) Core(TM) i9-14900K", "Core i9 processors (14th Generation)")]
    [InlineData("Intel(R) Core(TM) m3-8100Y CPU @ 1.10GHz", "8th Generation Core m Processors")]
    [InlineData("Intel(R) Core(TM) Ultra 7 155H", "Core Ultra Processors (Series 1)")]
    [InlineData("Intel(R) Core(TM) Ultra 5 225H", "Core Ultra Processors (Series 2)")]
    [InlineData("Intel(R) Core(TM) 7 150U", "Core Processors (Series 1)")]
    [InlineData("Intel(R) Core(TM) i3-N305", "Core Processor N300 Series")]
    [InlineData("Intel(R) N100", "N100 Series")]
    [InlineData("Intel(R) Celeron(R) N4020 CPU @ 1.10GHz", "Celeron N4000 Series")]
    [InlineData("Intel(R) Pentium(R) Silver N6000 @ 1.10GHz", "Pentium Silver N6000 Series")]
    [InlineData("Intel(R) Pentium(R) Gold 4417U CPU @ 2.30GHz", "Pentium Gold 4000U Series")]
    [InlineData("Intel(R) Xeon(R) W-2445", "Xeon W-2400 Series")]
    [InlineData("AMD Ryzen 7 7840HS w/ Radeon 780M Graphics", "7000 Series")]
    [InlineData("AMD Ryzen 5 3600 6-Core Processor", "3000 Series")]
    [InlineData("AMD Ryzen 7 PRO 5850U with Radeon Graphics", "5000 Series")]
    [InlineData("AMD Ryzen AI 9 HX 370 w/ Radeon 890M", "AI 300 Series")]
    [InlineData("AMD Ryzen Z1 Extreme", "Z1 Series")]
    [InlineData("AMD Ryzen Threadripper PRO 5995WX 64-Cores", "5000 WX-Series")]
    [InlineData("AMD Athlon Silver 7120U with Radeon Graphics", "7000 U Series")]
    [InlineData("AMD EPYC 7763 64-Core Processor", "7003 Series")]
    [InlineData("Snapdragon(R) X Elite - X1E78100 - Qualcomm(R) Oryon(TM) CPU", "X1E")]
    [InlineData("Snapdragon(R) X Plus - X1P64100 - Qualcomm(R) Oryon(TM) CPU", "X1P")]
    public void Processador_na_lista(string nome, string serie)
    {
        var p = Tabela.Verificar(nome);

        Assert.Equal(SituacaoProcessador.NaLista, p.Situacao);
        Assert.Equal(serie, p.Serie);
    }

    [Theory]
    [InlineData("Intel(R) Core(TM) i7-7700HQ CPU @ 2.80GHz")]
    [InlineData("Intel(R) Core(TM) i5-6500 CPU @ 3.20GHz")]
    [InlineData("Intel(R) Core(TM) i7 CPU 920 @ 2.67GHz")]
    [InlineData("Intel(R) Core(TM) i7-920")]
    [InlineData("AMD Ryzen 7 2700X Eight-Core Processor")]
    [InlineData("AMD Ryzen 5 1600 Six-Core Processor")]
    public void Geracao_anterior_a_lista(string nome)
    {
        Assert.Equal(SituacaoProcessador.GeracaoAnterior, Tabela.Verificar(nome).Situacao);
    }

    [Theory]
    [InlineData("Intel(R) Xeon(R) W-2245 CPU @ 3.90GHz")]
    [InlineData("Intel(R) Core(TM)2 Duo CPU E8400 @ 3.00GHz")]
    [InlineData("Processador Exemplo de Outro Fabricante")]
    [InlineData("AMD FX(tm)-8350 Eight-Core Processor")]
    [InlineData(null)]
    public void Fora_da_lista_sem_certeza(string? nome)
    {
        Assert.Equal(SituacaoProcessador.ForaDaLista, Tabela.Verificar(nome).Situacao);
    }

    [Fact]
    public void Tabela_embutida_e_integra()
    {
        Assert.True(Tabela.Quantidade > 100);
        Assert.Equal("25H2", Tabela.Versao);
    }

    private static readonly Campo<string> NaLista = Campo<string>.Lido("12th Gen Intel(R) Core(TM) i5-12500H", FonteDado.Cpuid);

    private static DadosFirmware Firmware(string modo = "UEFI", bool? secureBoot = true, string? tpm = "2.0")
    {
        var texto = Campo<string>.NaoInformado(FonteDado.Firmware);
        return new DadosFirmware(
            Campo<string>.Lido(modo, FonteDado.Firmware),
            secureBoot is { } s ? Campo<bool>.Lido(s, FonteDado.Registro) : Campo<bool>.NaoSuportado(FonteDado.Registro, "firmware em modo legado"),
            tpm is null ? Campo<string>.NaoSuportado(FonteDado.Tpm, "TPM não encontrado") : Campo<string>.Lido(tpm, FonteDado.Tpm),
            texto,
            texto,
            Campo<bool>.NaoInformado(FonteDado.Firmware));
    }

    private static VerificacaoWindows11 Verificar(Campo<string>? processador = null, DadosFirmware? firmware = null, long memoria = 16L << 30, long disco = 512_000_000_000) =>
        VerificadorWindows11.Verificar(processador ?? NaLista, firmware ?? Firmware(), Campo<long>.Lido(memoria, FonteDado.Windows), Campo<long>.Lido(disco, FonteDado.Armazenamento), Tabela);

    private static ItemWindows11 Item(VerificacaoWindows11 v, string item) => v.Itens.Single(i => i.Item == item);

    [Fact]
    public void Maquina_que_atende_tudo()
    {
        var v = Verificar();

        Assert.Equal(6, v.Itens.Count);
        Assert.All(v.Itens, i => Assert.Equal(EstadoRequisito.Atende, i.Estado));
        Assert.Equal("25H2", v.VersaoLista);
    }

    [Fact]
    public void Processador_de_geracao_anterior_nao_atende()
    {
        Assert.Equal(EstadoRequisito.NaoAtende, Item(Verificar(Campo<string>.Lido("Intel(R) Core(TM) i7-7700 CPU", FonteDado.Cpuid)), "Processador").Estado);
    }

    [Fact]
    public void Processador_fora_da_lista_fica_desconhecido()
    {
        var i = Item(Verificar(Campo<string>.Lido("Processador Exemplo", FonteDado.Cpuid)), "Processador");

        Assert.Equal(EstadoRequisito.Desconhecido, i.Estado);
        Assert.StartsWith("não consta na lista do MapHard", i.Detalhe, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1.2", EstadoRequisito.NaoAtende)]
    [InlineData(null, EstadoRequisito.Configuracao)]
    public void Tpm_que_nao_atende(string? versao, EstadoRequisito esperado)
    {
        Assert.Equal(esperado, Item(Verificar(firmware: Firmware(tpm: versao)), "TPM").Estado);
    }

    [Fact]
    public void Bios_legado_e_secure_boot_sao_configuracao()
    {
        var v = Verificar(firmware: Firmware(modo: "BIOS legado", secureBoot: null));

        Assert.Equal(EstadoRequisito.Configuracao, Item(v, "Firmware").Estado);
        Assert.Equal(EstadoRequisito.Configuracao, Item(v, "Secure Boot").Estado);
    }

    [Fact]
    public void Secure_boot_desligado_com_uefi()
    {
        var i = Item(Verificar(firmware: Firmware(secureBoot: false)), "Secure Boot");

        Assert.Equal(EstadoRequisito.Configuracao, i.Estado);
        Assert.Equal("Secure Boot desligado: ligar no firmware", i.Detalhe);
    }

    [Fact]
    public void Memoria_abaixo_de_4_gb_nao_atende()
    {
        Assert.Equal(EstadoRequisito.NaoAtende, Item(Verificar(memoria: 2L << 30), "Memória").Estado);
        Assert.Equal(EstadoRequisito.Atende, Item(Verificar(memoria: 4L << 30), "Memória").Estado);
    }

    [Fact]
    public void Disco_abaixo_de_64_gb_nao_atende()
    {
        Assert.Equal(EstadoRequisito.NaoAtende, Item(Verificar(disco: 32_000_000_000), "Armazenamento").Estado);
        Assert.Equal(EstadoRequisito.Atende, Item(Verificar(disco: 64_000_000_000), "Armazenamento").Estado);
    }

    [Fact]
    public void Campo_nao_lido_fica_desconhecido()
    {
        var v = VerificadorWindows11.Verificar(
            Campo<string>.Erro(FonteDado.Cpuid, "falhou"),
            Firmware(),
            Campo<long>.Erro(FonteDado.Windows, "falhou"),
            Campo<long>.NaoInformado(FonteDado.Armazenamento),
            Tabela);

        Assert.Equal(EstadoRequisito.Desconhecido, Item(v, "Processador").Estado);
        Assert.Equal(EstadoRequisito.Desconhecido, Item(v, "Memória").Estado);
        Assert.Equal(EstadoRequisito.Desconhecido, Item(v, "Armazenamento").Estado);
    }
}
