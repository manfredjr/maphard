using System.Buffers.Binary;
using System.Text;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Rede;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class RedeTestes
{
    /// <summary>MIB_IF_ROW2 de 64 bits montada à mão.</summary>
    internal static byte[] Linha(string nome, int tipo = 6, bool conectada = true, ulong velocidade = 1_000_000_000, byte sinais = 0x05, int meio = 14)
    {
        var l = new byte[LeitorRede.TamanhoLinha];
        Encoding.Unicode.GetBytes(nome).CopyTo(l, 28);
        Encoding.Unicode.GetBytes("Placa de Rede Exemplo").CopyTo(l, 542);
        BinaryPrimitives.WriteUInt32LittleEndian(l.AsSpan(1056), 6);
        new byte[] { 0x02, 0x00, 0x5E, 0x10, 0x20, 0xAB }.CopyTo(l, 1060);
        BinaryPrimitives.WriteInt32LittleEndian(l.AsSpan(1128), tipo);
        BinaryPrimitives.WriteInt32LittleEndian(l.AsSpan(1140), meio);
        l[1152] = sinais;
        BinaryPrimitives.WriteInt32LittleEndian(l.AsSpan(1164), conectada ? 1 : 2);
        BinaryPrimitives.WriteUInt64LittleEndian(l.AsSpan(1200), velocidade);
        return l;
    }

    [Fact]
    public void Placa_ethernet_conectada()
    {
        var p = LeitorRede.Interpretar(Linha("Ethernet"))!;

        Assert.Equal("Ethernet", p.Nome.Valor);
        Assert.Equal("Placa de Rede Exemplo", p.Descricao.Valor);
        Assert.Equal("02-00-5E-10-20-AB", p.Mac.Valor);
        Assert.Equal("Ethernet", p.Tipo.Valor);
        Assert.Equal(1_000_000_000, p.VelocidadeBps.Valor);
        Assert.True(p.Conectada.Valor);
    }

    [Fact]
    public void Wifi_e_bluetooth_pelo_tipo_e_pelo_meio()
    {
        Assert.Equal("Wi-Fi", LeitorRede.Interpretar(Linha("Wi-Fi", tipo: 71, meio: 9))!.Tipo.Valor);
        Assert.Equal("Bluetooth", LeitorRede.Interpretar(Linha("Bluetooth", meio: 10))!.Tipo.Valor);
    }

    [Fact]
    public void Desconectada_fica_sem_velocidade()
    {
        var p = LeitorRede.Interpretar(Linha("Ethernet", conectada: false))!;

        Assert.False(p.Conectada.Valor);
        Assert.Equal(EstadoCampo.NaoInformado, p.VelocidadeBps.Estado);
        Assert.Equal("desconectada", p.VelocidadeBps.Motivo);
    }

    [Fact]
    public void Adaptador_virtual_e_filtro_ficam_de_fora()
    {
        Assert.Empty(LeitorRede.Montar([Linha("Virtual", sinais: 0x00), Linha("Filtro", sinais: 0x03), new byte[100]]));
    }

    [Theory]
    [InlineData(1_000_000_000L, "1 Gb/s")]
    [InlineData(721_000_000L, "721 Mb/s")]
    [InlineData(2_500_000_000L, "2,5 Gb/s")]
    [InlineData(100_000_000L, "100 Mb/s")]
    public void Velocidade_em_texto(long bps, string esperado)
    {
        Assert.Equal(esperado, LeitorRede.TextoVelocidade(bps));
    }

    [FatoWindows]
    public void Leitura_real_tem_linhas_e_nao_falha()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var linhas = new FonteRedeWindows().Interfaces();

        Assert.NotEmpty(linhas);
        _ = LeitorRede.Montar(linhas);
    }
}
