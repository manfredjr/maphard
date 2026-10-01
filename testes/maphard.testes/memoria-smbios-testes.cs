using MapHard.Nucleo.Smbios;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class MemoriaSmbiosTestes
{
    private const long Gb = 1024L * 1024 * 1024;

    private static TabelaSmbios Tabela(params (byte Tipo, byte[] Corpo)[] estruturas)
    {
        var c = new ConstrutorSmbios();
        foreach (var (tipo, corpo) in estruturas)
        {
            if (tipo == 17)
            {
                c.Estrutura(tipo, corpo, ConstrutorMemoria.TextosModulo);
            }
            else
            {
                c.Estrutura(tipo, corpo);
            }
        }

        return LeitorTabelaSmbios.Interpretar(c.Fim().Montar())!;
    }

    private static ModuloMemoriaSmbios Modulo(byte[] corpo) => ModuloMemoriaSmbios.Todos(Tabela((17, corpo))).Single();

    [Fact]
    public void Modulo_de_16_gb_em_mb_com_todos_os_campos()
    {
        var m = Modulo(ConstrutorMemoria.Modulo(16384, codigoFabricante: 0xCE80, ranks: 2));

        Assert.Equal(16 * Gb, m.TamanhoBytes);
        Assert.False(m.Vazio);
        Assert.Equal("ChannelA-DIMM0", m.Slot);
        Assert.Equal("BANK 0", m.Banco);
        Assert.Equal("DDR4", m.NomeDoTipo);
        Assert.Equal("SODIMM", m.NomeDoFormato);
        Assert.Equal(3200, m.VelocidadeNominal);
        Assert.Equal(3200, m.VelocidadeConfigurada);
        Assert.Equal("Fabricante Memoria", m.Fabricante);
        Assert.Equal((ushort)0xCE80, m.CodigoFabricante);
        Assert.Equal("SERIE-MEM-0001", m.NumeroSerie);
        Assert.Equal("PN-TESTE-3200", m.PartNumber);
        Assert.Equal(2, m.Ranks);
        Assert.Equal(1200, m.VoltagemConfiguradaMv);
    }

    [Fact]
    public void Bit_15_ligado_quer_dizer_kb()
    {
        Assert.Equal(512 * 1024L, Modulo(ConstrutorMemoria.Modulo(0x8000 | 512)).TamanhoBytes);
    }

    [Fact]
    public void Tamanho_0x7fff_le_o_estendido()
    {
        Assert.Equal(64 * Gb, Modulo(ConstrutorMemoria.Modulo(0x7FFF, tamanhoEstendidoMb: 65536)).TamanhoBytes);
    }

    [Fact]
    public void Tamanho_0x7fff_em_estrutura_sem_o_estendido_fica_desconhecido()
    {
        var m = Modulo(ConstrutorMemoria.Modulo(0x7FFF, tamanhoEstendidoMb: 65536, tamanho: 0x1C));

        Assert.Null(m.TamanhoBytes);
        Assert.False(m.Vazio);
    }

    [Fact]
    public void Slot_vazio_ignora_os_outros_campos()
    {
        var m = Modulo(ConstrutorMemoria.Modulo(0, codigoFabricante: 0xCE80));

        Assert.True(m.Vazio);
        Assert.Equal("ChannelA-DIMM0", m.Slot);
        Assert.Null(m.TamanhoBytes);
        Assert.Null(m.VelocidadeNominal);
        Assert.Null(m.Fabricante);
        Assert.Null(m.PartNumber);
        Assert.Null(m.CodigoFabricante);
    }

    [Fact]
    public void Tamanho_0xffff_e_desconhecido()
    {
        var m = Modulo(ConstrutorMemoria.Modulo(0xFFFF));

        Assert.Null(m.TamanhoBytes);
        Assert.False(m.Vazio);
    }

    [Fact]
    public void Velocidade_0xffff_le_o_estendido_na_versao_3_3()
    {
        var m = Modulo(ConstrutorMemoria.Modulo(32768, tipo: 0x22, velocidade: 0xFFFF, configurada: 0xFFFF, velocidadeEstendida: 70000, configuradaEstendida: 68000, tamanho: ConstrutorMemoria.Tipo17Versao33));

        Assert.Equal(70000, m.VelocidadeNominal);
        Assert.Equal(68000, m.VelocidadeConfigurada);
        Assert.Equal("DDR5", m.NomeDoTipo);
    }

    [Fact]
    public void Velocidade_0xffff_sem_o_estendido_fica_desconhecida()
    {
        Assert.Null(Modulo(ConstrutorMemoria.Modulo(8192, velocidade: 0xFFFF)).VelocidadeNominal);
    }

    [Fact]
    public void Estrutura_curta_da_versao_2_1_nao_tem_velocidade_nem_textos()
    {
        var m = Modulo(ConstrutorMemoria.Modulo(8192, tamanho: 0x15));

        Assert.Equal(8 * Gb, m.TamanhoBytes);
        Assert.Null(m.VelocidadeNominal);
        Assert.Null(m.Fabricante);
        Assert.Null(m.Ranks);
    }

    [Fact]
    public void Estrutura_da_versao_2_x_nao_tem_voltagem_nem_codigo_de_fabricante()
    {
        var m = Modulo(ConstrutorMemoria.Modulo(8192, codigoFabricante: 0xCE80, tamanho: 0x22));

        Assert.Equal(3200, m.VelocidadeConfigurada);
        Assert.Null(m.VoltagemConfiguradaMv);
        Assert.Null(m.CodigoFabricante);
        Assert.Equal("PN-TESTE-3200", m.PartNumber);
    }

    [Theory]
    [InlineData(0x1A, "DDR4")]
    [InlineData(0x22, "DDR5")]
    [InlineData(0x23, "LPDDR5")]
    [InlineData(0x18, "DDR3")]
    [InlineData(0x1E, "LPDDR4")]
    public void Nomes_de_tipo(byte codigo, string nome)
    {
        Assert.Equal(nome, ModuloMemoriaSmbios.NomeTipo(codigo));
    }

    [Theory]
    [InlineData(0x09, "DIMM")]
    [InlineData(0x0D, "SODIMM")]
    [InlineData(0x0B, "fileira de chips")]
    public void Nomes_de_formato(byte codigo, string nome)
    {
        Assert.Equal(nome, ModuloMemoriaSmbios.NomeFormato(codigo));
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x01)]
    [InlineData(0x02)]
    [InlineData(0x15)]
    [InlineData(0x25)]
    [InlineData(0xFF)]
    public void Tipo_fora_da_tabela_outro_desconhecido_ou_reservado_vira_nulo(byte codigo)
    {
        Assert.Null(ModuloMemoriaSmbios.NomeTipo(codigo));
    }

    [Fact]
    public void Conjunto_com_capacidade_em_kb()
    {
        var c = ConjuntoMemoriaSmbios.DoSistema(Tabela((16, ConstrutorMemoria.Conjunto(32 * 1024 * 1024, 2)))).Single();

        Assert.Equal(32 * Gb, c.CapacidadeMaximaBytes);
        Assert.Equal(2, c.Slots);
        Assert.Equal("nenhuma", ConjuntoMemoriaSmbios.NomeCorrecaoErro(c.CorrecaoErro));
    }

    [Fact]
    public void Conjunto_com_0x80000000_le_o_estendido()
    {
        var c = ConjuntoMemoriaSmbios.DoSistema(Tabela((16, ConstrutorMemoria.Conjunto(0x80000000, 4, capacidadeEstendida: 8UL * 1024 * Gb)))).Single();

        Assert.Equal(8 * 1024 * Gb, c.CapacidadeMaximaBytes);
    }

    [Fact]
    public void Conjunto_curto_com_0x80000000_fica_sem_capacidade()
    {
        var c = ConjuntoMemoriaSmbios.DoSistema(Tabela((16, ConstrutorMemoria.Conjunto(0x80000000, 4, tamanho: 0x0F)))).Single();

        Assert.Null(c.CapacidadeMaximaBytes);
        Assert.Equal(4, c.Slots);
    }

    [Fact]
    public void Conjunto_com_ecc_de_um_bit()
    {
        var c = ConjuntoMemoriaSmbios.DoSistema(Tabela((16, ConstrutorMemoria.Conjunto(16 * 1024 * 1024, 4, ecc: 0x05)))).Single();

        Assert.Equal("ECC de um bit", ConjuntoMemoriaSmbios.NomeCorrecaoErro(c.CorrecaoErro));
    }

    [Fact]
    public void So_os_conjuntos_de_memoria_do_sistema_contam()
    {
        var tabela = Tabela(
            (16, ConstrutorMemoria.Conjunto(16 * 1024 * 1024, 4)),
            (16, ConstrutorMemoria.Conjunto(1024, 1, uso: 0x05)),
            (16, ConstrutorMemoria.Conjunto(64 * 1024 * 1024, 2)));

        var conjuntos = ConjuntoMemoriaSmbios.DoSistema(tabela);

        Assert.Equal(2, conjuntos.Count);
        Assert.Equal(6, conjuntos.Sum(c => c.Slots));
    }

    [FatoWindows]
    public void Maquina_real_tem_ao_menos_um_modulo_com_tamanho()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tabela = LeitorTabelaSmbios.Interpretar(new FonteSmbiosWindows().LerTabelaBruta());

        Assert.NotNull(tabela);
        Assert.Contains(ModuloMemoriaSmbios.Todos(tabela), m => m.TamanhoBytes > 0);
    }
}
