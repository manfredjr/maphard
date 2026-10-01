using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Smbios;
using MapHard.Nucleo.Tabelas;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class MemoriaLeitorTestes
{
    private const long Gb = 1024L * 1024 * 1024;

    private static readonly FabricantesMemoria Fabricantes = FabricantesMemoria.Ler("banco;codigo;nome;fonte\n1;CE;Samsung;f\n");

    /// <summary>Quatro slots, dois com módulos iguais de 8 GB DDR4-3200, em placa que aceita 64 GB.</summary>
    internal static TabelaSmbios DoisDeQuatro() => LeitorTabelaSmbios.Interpretar(new ConstrutorSmbios()
        .Estrutura(16, ConstrutorMemoria.Conjunto(64 * 1024 * 1024, 4))
        .Estrutura(17, ConstrutorMemoria.Modulo(8192, tipo: 0x1A, formato: 0x09, codigoFabricante: 0xCE80), "ChannelA-DIMM0", "BANK 0", "Fabricante Memoria", "SERIE-MEM-0001", "PATRIMONIO-0001", "PN-TESTE-3200")
        .Estrutura(17, ConstrutorMemoria.Modulo(0, formato: 0x09), "ChannelA-DIMM1", "BANK 1")
        .Estrutura(17, ConstrutorMemoria.Modulo(8192, tipo: 0x1A, formato: 0x09, codigoFabricante: 0xCE80), "ChannelB-DIMM0", "BANK 2", "Fabricante Memoria", "SERIE-MEM-0002", "PATRIMONIO-0002", "PN-TESTE-3200")
        .Estrutura(17, ConstrutorMemoria.Modulo(0, formato: 0x09), "ChannelB-DIMM1", "BANK 3")
        .Fim().Montar())!;

    internal static EstadoMemoriaWindows EstadoWindows(long total = 16 * Gb - 300 * 1024 * 1024) =>
        new(total, 6 * Gb, 62, 12 * Gb, 20 * Gb, 3 * Gb);

    private static SecaoMemoria Montar(TabelaSmbios? smbios, long? instaladaKb = 16 * 1024 * 1024, EstadoMemoriaWindows? estado = null, string? falhaSmbios = null) =>
        LeitorMemoria.Montar(new LeiturasMemoria(smbios, falhaSmbios, instaladaKb, null, estado ?? EstadoWindows(), null), Fabricantes);

    [Fact]
    public void Dois_modulos_iguais_em_quatro_slots()
    {
        var s = Montar(DoisDeQuatro());

        Assert.Equal(16 * Gb, s.Instalada.Valor);
        Assert.Null(s.Instalada.Motivo);
        Assert.Equal("DDR4", s.Tipo.Valor);
        Assert.Equal(4, s.SlotsTotal.Valor);
        Assert.Equal(2, s.SlotsOcupados.Valor);
        Assert.Equal(64 * Gb, s.CapacidadeMaxima.Valor);
        Assert.Equal("nenhuma", s.Ecc.Valor);

        var m = s.Modulos.Valor![0];
        Assert.Equal("ChannelA-DIMM0", m.Slot.Valor);
        Assert.Equal(8 * Gb, m.Tamanho.Valor);
        Assert.Equal("DIMM", m.Formato.Valor);
        Assert.Equal(3200, m.VelocidadeConfigurada.Valor);
        Assert.Equal("Samsung", m.Fabricante.Valor);
        Assert.Equal("PN-TESTE-3200", m.PartNumber.Valor);
        Assert.Equal("SERIE-MEM-0001", m.NumeroSerie.Valor);
        Assert.Equal(1200, m.VoltagemMv.Valor);
    }

    [Fact]
    public void Slot_sem_modulo_aparece_como_vazio_e_nao_some()
    {
        var modulos = Montar(DoisDeQuatro()).Modulos.Valor!;

        Assert.Equal(4, modulos.Count);
        var vazio = modulos[1];
        Assert.True(vazio.Vazio);
        Assert.Equal("ChannelA-DIMM1", vazio.Slot.Valor);
        Assert.Equal(EstadoCampo.NaoSuportado, vazio.Tamanho.Estado);
        Assert.Equal("slot vazio", vazio.Tamanho.Motivo);
    }

    [Fact]
    public void Reservada_e_a_instalada_menos_a_utilizavel()
    {
        var s = Montar(DoisDeQuatro());

        Assert.Equal(300 * 1024 * 1024L, s.Reservada.Valor);
        Assert.Equal(16 * Gb - (300 * 1024 * 1024L), s.Utilizavel.Valor);
    }

    [Fact]
    public void Soma_dos_modulos_diferente_da_instalada_vai_no_motivo()
    {
        var s = Montar(DoisDeQuatro(), instaladaKb: 24 * 1024 * 1024);

        Assert.Equal(24 * Gb, s.Instalada.Valor);
        Assert.Contains("16 GB", s.Instalada.Motivo);
    }

    [Fact]
    public void Sem_o_windows_a_instalada_e_a_soma_dos_modulos()
    {
        var s = Montar(DoisDeQuatro(), instaladaKb: null);

        Assert.Equal(16 * Gb, s.Instalada.Valor);
        Assert.Equal(FonteDado.Smbios, s.Instalada.Fonte);
    }

    [Fact]
    public void Smbios_sem_tipo_17_deixa_os_modulos_nao_informados_e_a_instalada_sai_do_windows()
    {
        var tabela = LeitorTabelaSmbios.Interpretar(new ConstrutorSmbios().Estrutura(16, ConstrutorMemoria.Conjunto(32 * 1024 * 1024, 2)).Fim().Montar())!;

        var s = Montar(tabela);

        Assert.Equal(EstadoCampo.NaoInformado, s.Modulos.Estado);
        Assert.Equal(EstadoCampo.NaoInformado, s.SlotsOcupados.Estado);
        Assert.Equal(EstadoCampo.NaoInformado, s.Tipo.Estado);
        Assert.Equal(2, s.SlotsTotal.Valor);
        Assert.Equal(16 * Gb, s.Instalada.Valor);
        Assert.Equal(FonteDado.Windows, s.Instalada.Fonte);
    }

    [Fact]
    public void Smbios_que_falhou_deixa_so_os_campos_dele_em_erro()
    {
        var s = Montar(null, falhaSmbios: "falha simulada");

        Assert.Equal(EstadoCampo.ErroLeitura, s.Modulos.Estado);
        Assert.Equal("falha simulada", s.Modulos.Motivo);
        Assert.Equal(EstadoCampo.ErroLeitura, s.SlotsTotal.Estado);
        Assert.Equal(16 * Gb, s.Instalada.Valor);
        Assert.Equal(62, s.Carga.Valor);
    }

    [Fact]
    public void Uso_agora_sai_do_windows()
    {
        var s = Montar(DoisDeQuatro());

        Assert.Equal(EstadoWindows().TotalBytes - (6 * Gb), s.EmUso.Valor);
        Assert.Equal(6 * Gb, s.Disponivel.Valor);
        Assert.Equal(12 * Gb, s.Confirmada.Valor);
        Assert.Equal(20 * Gb, s.LimiteConfirmada.Valor);
        Assert.Equal(3 * Gb, s.Cache.Valor);
    }

    [Fact]
    public void Sem_o_desempenho_do_windows_a_confirmada_fica_nao_informada()
    {
        var s = Montar(DoisDeQuatro(), estado: new EstadoMemoriaWindows(16 * Gb, 6 * Gb, 62, null, null, null));

        Assert.Equal(EstadoCampo.NaoInformado, s.Confirmada.Estado);
        Assert.Equal(6 * Gb, s.Disponivel.Valor);
    }

    [Fact]
    public void Tipos_misturados_aparecem_juntos_com_observacao()
    {
        var tabela = LeitorTabelaSmbios.Interpretar(new ConstrutorSmbios()
            .Estrutura(17, ConstrutorMemoria.Modulo(8192, tipo: 0x1A), ConstrutorMemoria.TextosModulo)
            .Estrutura(17, ConstrutorMemoria.Modulo(8192, tipo: 0x22), ConstrutorMemoria.TextosModulo)
            .Fim().Montar())!;

        var s = Montar(tabela);

        Assert.Equal("DDR4 e DDR5", s.Tipo.Valor);
        Assert.Equal("módulos de tipos diferentes", s.Tipo.Motivo);
        Assert.Equal(2, s.SlotsTotal.Valor);
        Assert.Equal("contagem dos registros de módulo", s.SlotsTotal.Motivo);
    }
}
