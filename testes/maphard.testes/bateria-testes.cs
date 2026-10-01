using System.Buffers.Binary;
using System.Text;
using MapHard.Nucleo.Baterias;
using MapHard.Nucleo.Campos;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class BateriaTestes
{
    private const uint Sistema = 0x80000000;

    /// <summary>BATTERY_INFORMATION montado à mão.</summary>
    internal static byte[] Informacao(uint projeto, uint atual, uint ciclos = 120, string quimica = "LION", uint capacidades = Sistema)
    {
        var b = new byte[LeitorBateria.TamanhoInformacao];
        BinaryPrimitives.WriteUInt32LittleEndian(b, capacidades);
        b[4] = 1;
        Encoding.ASCII.GetBytes(quimica).AsSpan(0, Math.Min(4, quimica.Length)).CopyTo(b.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(12), projeto);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(16), atual);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(32), ciclos);
        return b;
    }

    private static Bateria? Ler(byte[] informacao) => LeitorBateria.Interpretar(new BateriaBruta(informacao, "Bateria Exemplo", "Fabricante Exemplo"));

    [Fact]
    public void Desgaste_pela_capacidade_de_carga_total()
    {
        var b = Ler(Informacao(56000, 42000))!;

        Assert.Equal(56000, b.CapacidadeProjetoMwh.Valor);
        Assert.Equal(42000, b.CapacidadeAtualMwh.Valor);
        Assert.Equal(25.0, b.Desgaste.Valor);
        Assert.Equal(120, b.Ciclos.Valor);
        Assert.Equal("íon de lítio", b.Quimica.Valor);
        Assert.Equal("Bateria Exemplo", b.Nome.Valor);
    }

    [Fact]
    public void Carga_acima_do_projeto_nao_da_desgaste_negativo()
    {
        Assert.Equal(0.0, Ler(Informacao(50000, 52000))!.Desgaste.Valor);
    }

    [Fact]
    public void Capacidade_relativa_nao_vira_mwh()
    {
        var b = Ler(Informacao(100, 88, capacidades: Sistema | 0x40000000))!;

        Assert.Equal(EstadoCampo.NaoInformado, b.CapacidadeProjetoMwh.Estado);
        Assert.Equal(EstadoCampo.NaoInformado, b.CapacidadeAtualMwh.Estado);
        Assert.Equal(12.0, b.Desgaste.Valor);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0xFFFFFFFFu)]
    public void Capacidade_zero_ou_desconhecida_fica_sem_desgaste(uint atual)
    {
        var b = Ler(Informacao(56000, atual))!;

        Assert.Equal(EstadoCampo.NaoInformado, b.CapacidadeAtualMwh.Estado);
        Assert.Equal(EstadoCampo.NaoInformado, b.Desgaste.Estado);
    }

    [Fact]
    public void Ciclos_zero_e_nao_informado()
    {
        var c = Ler(Informacao(56000, 42000, ciclos: 0))!.Ciclos;

        Assert.Equal(EstadoCampo.NaoInformado, c.Estado);
        Assert.Equal("a bateria não informa", c.Motivo);
    }

    [Fact]
    public void Quimica_fora_da_tabela_fica_como_veio()
    {
        var q = Ler(Informacao(56000, 42000, quimica: "LiP"))!.Quimica;

        Assert.Equal("LiP", q.Valor);
        Assert.Equal("abreviação fora da tabela da Microsoft", q.Motivo);
    }

    [Fact]
    public void Nobreak_e_bateria_fora_do_sistema_ficam_de_fora()
    {
        Assert.Null(Ler(Informacao(56000, 42000, capacidades: Sistema | 0x20000000)));
        Assert.Null(Ler(Informacao(56000, 42000, capacidades: 0)));
        Assert.Null(Ler(new byte[10]));
        Assert.Empty(LeitorBateria.Montar([]));
    }

    [FatoWindows]
    public void Leitura_real_nao_falha()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _ = LeitorBateria.Montar(new FonteBateriasWindows().Ler());
    }
}
