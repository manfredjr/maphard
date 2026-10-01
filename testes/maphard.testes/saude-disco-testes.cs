using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Saude;
using MapHard.Nucleo.Smart;
using MapHard.Nucleo.Tabelas;

namespace MapHard.Testes;

public class SaudeDiscoTestes
{
    private static readonly AtributosSmart Nomes = AtributosSmart.Ler("id;nome_original;nome;tipo_disco;fonte\n5;Reallocated_Sector_Ct;Setores realocados;;f\n");

    private static LeituraSmartAta Ata(bool? falhaPrevista = false, bool? ssd = null, params AtributoSmart[] atributos) =>
        new(atributos, falhaPrevista, null, ssd, null, null, null);

    private static AtributoSmart A(byte id, byte atual = 100, byte? limite = 0, ulong bruto = 0) => new(id, 0x33, atual, atual, limite, bruto);

    private static SaudeNvme Nvme(byte alerta = 0, int? temperatura = 42, int reserva = 100, int limite = 10, int usado = 3, decimal erros = 0, int? aviso = 75) =>
        new(alerta, temperatura, reserva, limite, usado, 0, 0, 0, 0, 0, erros, 0, aviso);

    [Fact]
    public void Ata_sem_problema_e_bom()
    {
        var s = RegrasDisco.Ata(Ata(atributos: [A(0x05, limite: 36), A(0xC2, bruto: 35)]), TipoDisco.Hdd, Nomes);

        Assert.Equal(EstadoSaude.Bom, s.Estado);
        Assert.Empty(s.Motivos);
    }

    [Fact]
    public void Ata_atributo_no_limite_do_fabricante_e_ruim()
    {
        var s = RegrasDisco.Ata(Ata(atributos: [A(0x05, atual: 36, limite: 36)]), TipoDisco.Hdd, Nomes);

        Assert.Equal(EstadoSaude.Ruim, s.Estado);
        Assert.Equal(["Setores realocados no limite do fabricante (valor 36, limite 36)"], s.Motivos);
    }

    [Fact]
    public void Ata_limite_zero_nao_dispara()
    {
        Assert.Equal(EstadoSaude.Bom, RegrasDisco.Ata(Ata(atributos: [A(0x01, atual: 0, limite: 0)]), TipoDisco.Hdd, Nomes).Estado);
    }

    [Fact]
    public void Ata_falha_prevista_pelo_disco_e_ruim()
    {
        var s = RegrasDisco.Ata(Ata(falhaPrevista: true), TipoDisco.Hdd, Nomes);

        Assert.Equal(EstadoSaude.Ruim, s.Estado);
        Assert.Equal(["o próprio disco prevê falha"], s.Motivos);
    }

    [Theory]
    [InlineData((byte)0x05, 3ul, "3 setores realocados")]
    [InlineData((byte)0xC4, 1ul, "1 evento de realocação")]
    [InlineData((byte)0xC5, 2ul, "2 setores pendentes")]
    [InlineData((byte)0xC6, 1_234ul, "1.234 setores incorrigíveis")]
    public void Ata_setores_acima_de_zero_e_atencao(byte id, ulong bruto, string motivo)
    {
        var s = RegrasDisco.Ata(Ata(atributos: [A(id, bruto: bruto)]), TipoDisco.Hdd, Nomes);

        Assert.Equal(EstadoSaude.Atencao, s.Estado);
        Assert.Equal([motivo], s.Motivos);
    }

    [Fact]
    public void Ata_realocados_usa_so_os_16_bits_baixos()
    {
        Assert.Equal(EstadoSaude.Bom, RegrasDisco.Ata(Ata(atributos: [A(0x05, bruto: 0x0001_0000)]), TipoDisco.Hdd, Nomes).Estado);
    }

    [Fact]
    public void Ata_ruim_ganha_de_atencao_e_os_motivos_dos_dois_aparecem()
    {
        var s = RegrasDisco.Ata(Ata(falhaPrevista: true, atributos: [A(0xC5, bruto: 2)]), TipoDisco.Hdd, Nomes);

        Assert.Equal(EstadoSaude.Ruim, s.Estado);
        Assert.Equal(["o próprio disco prevê falha", "2 setores pendentes"], s.Motivos);
    }

    [Theory]
    [InlineData((byte)0xE7)]
    [InlineData((byte)0xE9)]
    public void Ssd_com_vida_restante_abaixo_de_10_e_atencao(byte id)
    {
        var s = RegrasDisco.Ata(Ata(atributos: [A(id, atual: 8)]), TipoDisco.SsdSata, Nomes);

        Assert.Equal(["vida restante de 8% informada pelo SSD"], s.Motivos);
    }

    [Fact]
    public void Vida_restante_so_vale_para_ssd()
    {
        Assert.Equal(EstadoSaude.Bom, RegrasDisco.Ata(Ata(atributos: [A(0xE7, atual: 8)]), TipoDisco.Hdd, Nomes).Estado);
        Assert.Equal(EstadoSaude.Bom, RegrasDisco.Ata(Ata(atributos: [A(0xE7, atual: 10)]), TipoDisco.SsdSata, Nomes).Estado);
    }

    [Theory]
    [InlineData(TipoDisco.Hdd, 51ul, true)]
    [InlineData(TipoDisco.Hdd, 50ul, false)]
    [InlineData(TipoDisco.SsdSata, 65ul, false)]
    [InlineData(TipoDisco.SsdSata, 71ul, true)]
    public void Temperatura_acima_do_limite_por_tipo(TipoDisco tipo, ulong graus, bool atencao)
    {
        var s = RegrasDisco.Ata(Ata(atributos: [A(0xC2, bruto: 0x0000_2D00_1E00_0000 | graus)]), tipo, Nomes);

        Assert.Equal(atencao ? EstadoSaude.Atencao : EstadoSaude.Bom, s.Estado);
        if (atencao)
        {
            Assert.Equal([$"temperatura de {graus} °C"], s.Motivos);
        }
    }

    [Fact]
    public void Temperatura_do_be_quando_nao_ha_c2_e_ssd_pelo_identify_em_disco_usb()
    {
        var leitura = Ata(ssd: true, atributos: [A(0xBE, bruto: 72)]);

        Assert.Equal(72, RegrasDisco.TemperaturaAta(leitura));
        Assert.Equal(EstadoSaude.Atencao, RegrasDisco.Ata(leitura, TipoDisco.Usb, Nomes).Estado);
    }

    [Fact]
    public void Ata_sem_smart_lido_e_desconhecido_com_o_motivo_nunca_bom()
    {
        var s = RegrasDisco.Ata(new LeituraSmartAta([], null, null, null, null, null, Volumes.RequerAdministrador), TipoDisco.SsdSata, Nomes);

        Assert.Equal(EstadoSaude.Desconhecido, s.Estado);
        Assert.Equal([Volumes.RequerAdministrador], s.Motivos);
    }

    [Fact]
    public void Disco_usb_sem_smart_e_desconhecido_com_o_aviso_da_controladora()
    {
        var s = RegrasDisco.Ata(new LeituraSmartAta([], null, null, null, null, null, FonteDiscosWindows.ControladoraSemSmart), TipoDisco.Usb, Nomes);

        Assert.Equal(EstadoSaude.Desconhecido, s.Estado);
        Assert.Equal(["SMART indisponível por esta controladora"], s.Motivos);
    }

    [Fact]
    public void Nvme_sem_problema_e_bom()
    {
        var s = RegrasDisco.Nvme(Nvme(), null);

        Assert.Equal(EstadoSaude.Bom, s.Estado);
        Assert.Empty(s.Motivos);
    }

    [Fact]
    public void Nvme_qualquer_bit_do_alerta_critico_e_ruim()
    {
        var s = RegrasDisco.Nvme(Nvme(alerta: 0x04), null);

        Assert.Equal(EstadoSaude.Ruim, s.Estado);
        Assert.Equal(["alerta crítico do disco: confiabilidade degradada"], s.Motivos);
    }

    [Theory]
    [InlineData(92, 0, 100, 42, "92% da vida útil usada")]
    [InlineData(3, 2, 100, 42, "2 erros de mídia")]
    [InlineData(3, 0, 19, 42, "reserva de 19%, perto do limite de 10%")]
    [InlineData(3, 0, 100, 80, "temperatura de 80 °C, acima do aviso de 75 °C do próprio disco")]
    public void Nvme_atencao_por_regra(int usado, int erros, int reserva, int temperatura, string motivo)
    {
        var s = RegrasDisco.Nvme(Nvme(usado: usado, erros: erros, reserva: reserva, temperatura: temperatura), null);

        Assert.Equal(EstadoSaude.Atencao, s.Estado);
        Assert.Equal([motivo], s.Motivos);
    }

    [Theory]
    [InlineData(89, 0, 20, 75)]
    [InlineData(3, 0, 20, 75)]
    public void Nvme_no_limite_das_regras_continua_bom(int usado, int erros, int reserva, int temperatura)
    {
        Assert.Equal(EstadoSaude.Bom, RegrasDisco.Nvme(Nvme(usado: usado, erros: erros, reserva: reserva, temperatura: temperatura), null).Estado);
    }

    [Fact]
    public void Nvme_sem_limite_de_aviso_nao_avalia_temperatura()
    {
        Assert.Equal(EstadoSaude.Bom, RegrasDisco.Nvme(Nvme(temperatura: 90, aviso: null), null).Estado);
    }

    [Fact]
    public void Nvme_sem_log_e_desconhecido()
    {
        var s = RegrasDisco.Nvme(null, null);

        Assert.Equal(EstadoSaude.Desconhecido, s.Estado);
        Assert.Equal(["SMART indisponível por esta controladora"], s.Motivos);
    }
}
