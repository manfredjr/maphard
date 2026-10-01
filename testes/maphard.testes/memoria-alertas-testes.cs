using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Smbios;
using MapHard.Nucleo.Tabelas;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class MemoriaAlertasTestes
{
    private const long Gb = 1024L * 1024 * 1024;

    private sealed record Pente(ushort Mb, string Slot, ushort Nominal = 3200, ushort Configurada = 3200, string PartNumber = "PN-TESTE-3200", string Banco = "BANK 0");

    /// <summary>Placa com <paramref name="slots"/> slots; pente com 0 MB é slot vazio.</summary>
    private static SecaoMemoria Secao(int slots, uint capacidadeKb, long instaladaGb, long utilizavelBytes, params Pente[] pentes)
    {
        var c = new ConstrutorSmbios().Estrutura(16, ConstrutorMemoria.Conjunto(capacidadeKb, (ushort)slots));
        foreach (var p in pentes)
        {
            c.Estrutura(17, ConstrutorMemoria.Modulo(p.Mb, tipo: 0x1A, formato: 0x0D, velocidade: p.Nominal, configurada: p.Configurada),
                p.Slot, p.Banco, "Fabricante Memoria", "SERIE-MEM-0001", "PATRIMONIO-0001", p.PartNumber);
        }

        var tabela = LeitorTabelaSmbios.Interpretar(c.Fim().Montar())!;
        var estado = new EstadoMemoriaWindows(utilizavelBytes, 4 * Gb, 50, null, null, null);
        return LeitorMemoria.Montar(new LeiturasMemoria(tabela, null, instaladaGb * 1024 * 1024, null, estado, null), FabricantesMemoria.Ler("banco;codigo;nome;fonte\n"));
    }

    private static SecaoMemoria DoisIguais(params Pente[] outros) =>
        Secao(2, 64 * 1024 * 1024, 16, 16 * Gb - (200 * 1024 * 1024), [new Pente(8192, "ChannelA-DIMM0"), new Pente(8192, "ChannelB-DIMM0"), .. outros]);

    private static IEnumerable<string> Codigos(SecaoMemoria s) => s.Alertas.Select(a => a.Codigo);

    [Fact]
    public void Dois_modulos_iguais_em_canais_diferentes_nao_geram_alerta()
    {
        Assert.Empty(DoisIguais().Alertas);
    }

    [Fact]
    public void Capacidades_diferentes_geram_alerta_com_os_tamanhos()
    {
        var s = Secao(2, 64 * 1024 * 1024, 12, 12 * Gb - (200 * 1024 * 1024), new Pente(8192, "ChannelA-DIMM0"), new Pente(4096, "ChannelB-DIMM0"));

        var alerta = Assert.Single(s.Alertas, a => a.Codigo == AlertasMemoria.ModulosDiferentes);
        Assert.Equal("módulos diferentes: 8 GB e 4 GB", alerta.Texto);
    }

    [Fact]
    public void Velocidades_nominais_diferentes_geram_alerta()
    {
        var s = Secao(2, 64 * 1024 * 1024, 16, 16 * Gb - (200 * 1024 * 1024), new Pente(8192, "ChannelA-DIMM0", 3200, 2666), new Pente(8192, "ChannelB-DIMM0", 2666, 2666));

        Assert.Contains("3200 e 2666 MT/s", Assert.Single(s.Alertas, a => a.Codigo == AlertasMemoria.ModulosDiferentes).Texto);
    }

    [Fact]
    public void Part_numbers_diferentes_com_capacidade_e_velocidade_iguais_nao_geram_alerta()
    {
        var s = Secao(2, 64 * 1024 * 1024, 16, 16 * Gb - (200 * 1024 * 1024), new Pente(8192, "ChannelA-DIMM0"), new Pente(8192, "ChannelB-DIMM0", PartNumber: "PN-TESTE-OUTRO"));

        Assert.Empty(s.Alertas);
    }

    [Fact]
    public void Configurada_abaixo_da_nominal_gera_alerta_com_as_duas_velocidades()
    {
        var s = Secao(2, 64 * 1024 * 1024, 16, 16 * Gb - (200 * 1024 * 1024), new Pente(8192, "ChannelA-DIMM0", 3200, 2666), new Pente(8192, "ChannelB-DIMM0", 3200, 2666));

        var alerta = Assert.Single(s.Alertas);
        Assert.Equal(AlertasMemoria.AbaixoDaVelocidade, alerta.Codigo);
        Assert.Equal("rodando a 2666 MT/s; os módulos aceitam 3200. Pode ser limite do processador ou da placa", alerta.Texto);
    }

    [Fact]
    public void Um_modulo_so_em_placa_com_dois_slots_e_canal_unico_provavel()
    {
        var s = Secao(2, 64 * 1024 * 1024, 8, 8 * Gb - (200 * 1024 * 1024), new Pente(8192, "ChannelA-DIMM0"), new Pente(0, "ChannelB-DIMM0"));

        Assert.Equal("provável canal único: o desempenho da memória cai", Assert.Single(s.Alertas).Texto);
    }

    [Fact]
    public void Um_modulo_so_em_placa_com_um_slot_nao_gera_alerta()
    {
        var s = Secao(1, 16 * 1024 * 1024, 8, 8 * Gb - (200 * 1024 * 1024), new Pente(8192, "ChannelA-DIMM0"));

        Assert.Empty(s.Alertas);
    }

    [Theory]
    [InlineData("ChannelA-DIMM0", "ChannelA-DIMM1")]
    [InlineData("A1", "A2")]
    [InlineData("DIMM_A1", "DIMM_A2")]
    public void Dois_modulos_no_mesmo_canal_sao_canal_unico_provavel(string slot1, string slot2)
    {
        var s = Secao(4, 64 * 1024 * 1024, 16, 16 * Gb - (200 * 1024 * 1024), new Pente(8192, slot1), new Pente(8192, slot2), new Pente(0, "B1"), new Pente(0, "B2"));

        Assert.Equal([AlertasMemoria.CanalUnico], Codigos(s));
    }

    [Fact]
    public void Canal_no_texto_do_banco_tambem_vale()
    {
        var s = Secao(2, 64 * 1024 * 1024, 16, 16 * Gb - (200 * 1024 * 1024), new Pente(8192, "DIMM 0", Banco: "P0 CHANNEL A"), new Pente(8192, "DIMM 1", Banco: "P0 CHANNEL A"));

        Assert.Equal([AlertasMemoria.CanalUnico], Codigos(s));
    }

    [Theory]
    [InlineData("DIMM 0", "DIMM 1")]
    [InlineData("Bottom-Slot 1", "Bottom-Slot 2")]
    [InlineData("A1", "B1")]
    public void Slot_sem_padrao_conhecido_ou_em_canais_diferentes_nao_gera_alerta_de_canal(string slot1, string slot2)
    {
        var s = Secao(2, 64 * 1024 * 1024, 16, 16 * Gb - (200 * 1024 * 1024), new Pente(8192, slot1), new Pente(8192, slot2));

        Assert.DoesNotContain(AlertasMemoria.CanalUnico, Codigos(s));
    }

    [Fact]
    public void Reserva_acima_de_25_por_cento_gera_alerta()
    {
        var s = Secao(2, 64 * 1024 * 1024, 16, 10 * Gb, new Pente(8192, "ChannelA-DIMM0"), new Pente(8192, "ChannelB-DIMM0"));

        Assert.Equal("o Windows usa 10 GB dos 16 GB instalados", Assert.Single(s.Alertas).Texto);
    }

    [Fact]
    public void Reserva_de_25_por_cento_ou_menos_nao_gera_alerta()
    {
        var s = Secao(2, 64 * 1024 * 1024, 16, 12 * Gb, new Pente(8192, "ChannelA-DIMM0"), new Pente(8192, "ChannelB-DIMM0"));

        Assert.Empty(s.Alertas);
    }

    [Fact]
    public void Ampliacao_com_capacidade_e_slots_livres()
    {
        var s = Secao(4, 64 * 1024 * 1024, 16, 16 * Gb - (200 * 1024 * 1024), new Pente(8192, "ChannelA-DIMM0"), new Pente(0, "ChannelA-DIMM1"), new Pente(8192, "ChannelB-DIMM0"), new Pente(0, "ChannelB-DIMM1"));

        Assert.Equal("cabem até 64 GB (informado pelo firmware); 2 slots livres; tipo DDR4, formato SODIMM", s.Ampliacao.Valor);
    }

    [Fact]
    public void Ampliacao_sem_capacidade_no_firmware_diz_so_os_slots_e_o_tipo()
    {
        var s = Secao(4, 0, 8, 8 * Gb - (200 * 1024 * 1024), new Pente(8192, "ChannelA-DIMM0"), new Pente(0, "ChannelA-DIMM1"), new Pente(0, "ChannelB-DIMM0"), new Pente(0, "ChannelB-DIMM1"));

        Assert.Equal("3 slots livres; tipo DDR4, formato SODIMM", s.Ampliacao.Valor);
    }

    [Fact]
    public void Ampliacao_com_todos_os_slots_ocupados()
    {
        Assert.Equal("cabem até 64 GB (informado pelo firmware); sem slot livre: ampliar exige trocar módulos; tipo DDR4, formato SODIMM", DoisIguais().Ampliacao.Valor);
    }

    [Fact]
    public void Ampliacao_com_um_slot_livre_fica_no_singular()
    {
        var s = Secao(2, 64 * 1024 * 1024, 8, 8 * Gb - (200 * 1024 * 1024), new Pente(8192, "ChannelA-DIMM0"), new Pente(0, "ChannelB-DIMM0"));

        Assert.StartsWith("cabem até 64 GB (informado pelo firmware); 1 slot livre;", s.Ampliacao.Valor);
    }

    [Fact]
    public void Ampliacao_sem_slots_no_firmware_fica_nao_informada()
    {
        var s = LeitorMemoria.Montar(new LeiturasMemoria(null, "falha simulada", 8 * 1024 * 1024, null, new EstadoMemoriaWindows(8 * Gb, 4 * Gb, 50, null, null, null), null), FabricantesMemoria.Ler("banco;codigo;nome;fonte\n"));

        Assert.Equal(EstadoCampo.NaoInformado, s.Ampliacao.Estado);
        Assert.Empty(s.Alertas);
    }
}
