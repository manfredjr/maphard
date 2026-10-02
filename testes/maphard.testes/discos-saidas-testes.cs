using System.Text.Json;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.LinhaDeComando;
using MapHard.Nucleo.Painel;
using MapHard.Nucleo.Relatorios;
using MapHard.Nucleo.Saude;
using MapHard.Nucleo.Smart;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class DiscosSaidasTestes
{
    private static Task<ColetaMaquina> Coletar(FontesColeta? fontes = null) =>
        new Coletor(fontes ?? FontesSimuladas.Completas(), TimeSpan.FromSeconds(5), agora: () => new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.FromHours(-3))).ColetarAsync();

    [Theory]
    [InlineData(12_345L, "12.345 horas, 514 dias")]
    [InlineData(23L, "23 horas")]
    [InlineData(24L, "24 horas, 1 dia")]
    public void Horas_no_formato_brasileiro(long horas, string esperado)
    {
        Assert.Equal(esperado, Formatador.Horas(horas));
    }

    [Fact]
    public void Gravados_e_capacidade_em_potencias_de_1000()
    {
        Assert.Equal("38,2 TB gravados", Formatador.Gravados(38_200_000_000_000m));
        Assert.Equal("1 TB", Formatador.BytesDecimais(1_000_204_886_016m));
        Assert.Equal("512,11 GB", Formatador.BytesDecimais(512_110_190_592m));
        Assert.Equal("42 °C", Formatador.Temperatura(42));
    }

    [Fact]
    public async Task Coleta_traz_o_nvme_lido_e_o_sata_pedindo_administrador()
    {
        var c = await Coletar();
        var discos = c.Discos.Discos.Valor!;

        Assert.Equal(2, discos.Count);
        var nvme = discos[0];
        Assert.Equal("NVMe Exemplo 1TB", nvme.Modelo.Valor);
        Assert.Equal("NVMe, PCIe 4.0 x4", nvme.Interface.Valor);
        Assert.Equal(EstadoSaude.Bom, nvme.Saude.Estado);
        Assert.Equal(42, nvme.Temperatura.Valor);
        Assert.Equal(12_345, nvme.HorasLigado.Valor);
        Assert.Equal(3, nvme.VidaUsada.Valor);
        Assert.Equal(["C:"], nvme.Volumes.Select(v => v.Letra.Valor));
        Assert.Equal(EstadoCampo.RequerAdministrador, nvme.Volumes[0].BitLocker.Estado);

        var hdd = discos[1];
        Assert.Equal("HDD", hdd.Tipo.Valor);
        Assert.Equal(EstadoSaude.Desconhecido, hdd.Saude.Estado);
        Assert.Equal([Volumes.RequerAdministrador], hdd.Saude.Motivos);
        Assert.Equal(EstadoCampo.RequerAdministrador, hdd.Temperatura.Estado);
        Assert.Equal(EstadoCampo.RequerAdministrador, hdd.Smart.Estado);
        Assert.Equal(EstadoCampo.RequerAdministrador, hdd.Rotacao.Estado);
        Assert.Equal("SATA", hdd.Interface.Valor);

        Assert.Equal(["G:"], c.Discos.VolumesSemDisco.Select(v => v.Letra.Valor));
        Assert.Equal("SSD NVMe 1 TB, HDD 2 TB", c.Identificacao.Discos.Valor);
    }

    [Fact]
    public async Task Com_administrador_o_sata_traz_a_tabela_e_a_saude()
    {
        var smart = new SmartAtaBruto(
            SmartAtaTestes.Identificacao(0x0006, 7200),
            SmartAtaTestes.Valores((0x05, 100, 100, 3), (0x09, 95, 95, 16_802), (0x0C, 99, 99, 1_204), (0xC2, 39, 52, 39)),
            SmartAtaTestes.Limites((0x05, 36)),
            0x4F,
            0xC2,
            null);
        var fontes = FontesSimuladas.Completas(discos: new FontesSimuladas.DiscosSimulados(_ => smart));

        var hdd = (await Coletar(fontes)).Discos.Discos.Valor![1];

        Assert.Equal(EstadoSaude.Atencao, hdd.Saude.Estado);
        Assert.Equal(["3 setores realocados"], hdd.Saude.Motivos);
        Assert.Equal(7200, hdd.Rotacao.Valor);
        Assert.Equal("SATA 6 Gb/s", hdd.Interface.Valor);
        Assert.Equal(39, hdd.Temperatura.Valor);
        Assert.Equal(16_802, hdd.HorasLigado.Valor);
        Assert.Equal(1_204, hdd.CiclosEnergia.Valor);
        Assert.Equal(EstadoCampo.NaoInformado, hdd.DadosGravadosBytes.Estado);
        Assert.Equal(4, hdd.Smart.Valor!.Count);
        Assert.Equal("05h", hdd.Smart.Valor[0].Id);
        Assert.Equal("Setores realocados", hdd.Smart.Valor[0].Nome);
    }

    [Fact]
    public async Task Disco_que_passa_do_tempo_fica_com_o_smart_em_tempo_esgotado_e_os_outros_seguem()
    {
        // O disco travado prende uma linha do pool de threads, e o .NET leva perto de meio segundo para abrir
        // outra. Com limite menor que isso, a leitura do disco 0 fica na fila e estoura também (aconteceu no CI,
        // que roda com poucos núcleos). O limite fica bem acima desse atraso e bem abaixo da espera do disco travado.
        var fontes = FontesSimuladas.Completas(discos: new FontesSimuladas.DiscosSimulados(_ =>
        {
            Thread.Sleep(6000);
            return new SmartAtaBruto(null, null, null, null, null, null);
        }));

        var c = await new Coletor(fontes, TimeSpan.FromSeconds(2)).ColetarAsync();

        var hdd = c.Discos.Discos.Valor![1];
        Assert.Equal(EstadoSaude.Desconhecido, hdd.Saude.Estado);
        Assert.Equal(["tempo esgotado"], hdd.Saude.Motivos);
        Assert.Equal(EstadoSaude.Bom, c.Discos.Discos.Valor[0].Saude.Estado);
    }

    [Fact]
    public async Task Json_traz_os_discos_e_volta_igual()
    {
        var original = await Coletar();
        var texto = ExportadorJson.Serializar(original);
        var json = JsonDocument.Parse(texto).RootElement;

        var discos = json.GetProperty("discos").GetProperty("discos").GetProperty("valor");
        Assert.Equal(2, discos.GetArrayLength());
        Assert.Equal("bom", discos[0].GetProperty("saude").GetProperty("estado").GetString());
        Assert.Equal("requerAdministrador", discos[1].GetProperty("smart").GetProperty("estado").GetString());

        var lida = ExportadorJson.Desserializar(texto)!;
        Assert.Equal(texto, ExportadorJson.Serializar(lida));
    }

    [Fact]
    public async Task Painel_tem_um_cartao_por_disco_com_o_selo_e_a_tabela_smart_logo_abaixo()
    {
        var c = await Coletar();
        var secao = MontadorSecoes.Montar(c, new DateOnly(2026, 10, 1)).Single(s => s.Id == MontadorSecoes.Discos);

        Assert.Equal(["Disco 0: NVMe Exemplo 1TB", "SMART do disco 0", "Disco 1: FABRICANTE HDD Exemplo 2TB", "SMART do disco 1", "Volumes sem disco físico ligado"], secao.Cartoes.Select(x => x.Titulo));
        Assert.Equal(("Bom", "bom"), (secao.Cartoes[0].Selo, secao.Cartoes[0].TomSelo));
        Assert.Equal(("Desconhecido", "desconhecido"), (secao.Cartoes[2].Selo, secao.Cartoes[2].TomSelo));
        Assert.Null(secao.Cartoes[1].Selo);

        string Texto(int cartao, string rotulo) => secao.Cartoes[cartao].Linhas.First(l => l.Rotulo == rotulo).Texto;
        Assert.Equal("nenhum problema encontrado", Texto(0, "Saúde"));
        Assert.Equal("42 °C", Texto(0, "Temperatura"));
        Assert.Equal("12.345 horas, 514 dias", Texto(0, "Horas ligado"));
        Assert.Equal("1 TB", Texto(0, "Capacidade"));
        Assert.StartsWith("400 GB livres de 930 GB, NTFS, BitLocker: requer administrador", Texto(0, "Volume C: Sistema"));
        Assert.Equal("requer administrador", Texto(2, "Temperatura"));
        Assert.Equal("requer administrador", Texto(3, "SMART"));
        Assert.All(secao.Cartoes.SelectMany(x => x.Linhas), l => Assert.False(string.IsNullOrWhiteSpace(l.Texto), l.Rotulo));
    }

    [Fact]
    public async Task Linha_de_comando_mostra_uma_linha_por_disco()
    {
        var linhas = ExecutorCli.Resumo(await Coletar()).ToList();

        Assert.Contains("Disco 0:     SSD NVMe 1 TB, Bom, 42 °C, 3% da vida usada", linhas);
        Assert.Contains("Disco 1:     HDD 2 TB, saúde Desconhecido (requer administrador)", linhas);
    }

    [Fact]
    public void Demonstracao_tem_um_disco_bom_e_um_em_atencao()
    {
        var c = DadosDemonstracao.Coleta();
        var linhas = ExecutorCli.Resumo(c).ToList();

        Assert.Equal([EstadoSaude.Bom, EstadoSaude.Atencao], c.Discos.Discos.Valor!.Select(d => d.Saude.Estado));
        Assert.Contains("  Discos:       Atenção (Disco 1: 3 setores realocados)", linhas);
        Assert.All(c.Discos.Discos.Valor!, d => Assert.StartsWith("SERIE-DISCO", d.NumeroSerie.Valor));
    }
}
