using System.Buffers.Binary;
using System.Text;
using MapHard.Nucleo.Discos;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class DiscosDescritorTestes
{
    /// <summary>STORAGE_DEVICE_DESCRIPTOR montado à mão, com os textos depois dos 36 bytes fixos. Texto nulo fica com deslocamento 0.</summary>
    internal static byte[] Descritor(int barramento, string? fabricante, string? produto, string? revisao, string? serie, bool removivel = false)
    {
        var bytes = new List<byte>(new byte[36]);
        var deslocamentos = new int[4];
        string?[] textos = [fabricante, produto, revisao, serie];
        for (var i = 0; i < 4; i++)
        {
            if (textos[i] is { } t)
            {
                deslocamentos[i] = bytes.Count;
                bytes.AddRange(Encoding.ASCII.GetBytes(t));
                bytes.Add(0);
            }
        }

        var b = bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(0), 1);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4), b.Length);
        b[10] = removivel ? (byte)1 : (byte)0;
        for (var i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(12 + (i * 4)), deslocamentos[i]);
        }

        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(28), barramento);
        return b;
    }

    [Fact]
    public void Modelo_firmware_e_serie_pelos_deslocamentos()
    {
        var d = DescritorArmazenamento.Interpretar(Descritor(11, "FABRICANTE", "  DISCO EXEMPLO 1TB  ", "FW01", "SERIE-DISCO-0001"))!;

        Assert.Equal("FABRICANTE DISCO EXEMPLO 1TB", d.Modelo);
        Assert.Equal("FW01", d.Firmware);
        Assert.Equal("SERIE-DISCO-0001", d.NumeroSerie);
        Assert.Equal(DescritorArmazenamento.BarramentoSata, d.Barramento);
        Assert.False(d.Removivel);
    }

    [Fact]
    public void Deslocamento_zero_vira_nulo_e_nvme_sem_fabricante_usa_so_o_produto()
    {
        var d = DescritorArmazenamento.Interpretar(Descritor(17, null, "NVMe Exemplo 512GB", "1.0", null))!;

        Assert.Equal("NVMe Exemplo 512GB", d.Modelo);
        Assert.Null(d.NumeroSerie);
    }

    [Fact]
    public void Deslocamento_fora_do_buffer_vira_nulo_sem_excecao()
    {
        var b = Descritor(11, null, "DISCO", "FW01", "SERIE");
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(24), 5000);

        Assert.Null(DescritorArmazenamento.Interpretar(b)!.NumeroSerie);
    }

    [Fact]
    public void Buffer_curto_ou_nulo_devolve_nulo()
    {
        Assert.Null(DescritorArmazenamento.Interpretar(new byte[20]));
        Assert.Null(DescritorArmazenamento.Interpretar(null));
    }

    [Theory]
    [InlineData(17, null, TipoDisco.SsdNvme)]
    [InlineData(11, false, TipoDisco.SsdSata)]
    [InlineData(11, true, TipoDisco.Hdd)]
    [InlineData(3, true, TipoDisco.Hdd)]
    [InlineData(11, null, TipoDisco.Outro)]
    [InlineData(7, true, TipoDisco.Usb)]
    [InlineData(7, false, TipoDisco.Usb)]
    [InlineData(8, false, TipoDisco.Raid)]
    [InlineData(14, null, TipoDisco.Outro)]
    public void Tipo_pelo_barramento_e_pela_penalidade_de_busca(int barramento, bool? penalidade, TipoDisco esperado)
    {
        Assert.Equal(esperado, DescritorArmazenamento.Classificar(barramento, penalidade));
    }

    [Theory]
    [InlineData(4u, 4u, "PCIe 4.0 x4")]
    [InlineData(3u, 2u, "PCIe 3.0 x2")]
    [InlineData(1u, null, "PCIe 1.0")]
    [InlineData(0u, 4u, null)]
    [InlineData(7u, 4u, null)]
    [InlineData(null, 4u, null)]
    public void Link_pcie_pela_velocidade_e_largura(uint? velocidade, uint? largura, string? esperado)
    {
        Assert.Equal(esperado, DescritorArmazenamento.LinkPcie(velocidade, largura));
    }

    [Theory]
    [InlineData(0, "MBR")]
    [InlineData(1, "GPT")]
    [InlineData(2, "sem partição")]
    [InlineData(9, null)]
    public void Estilo_de_particao(int estilo, string? esperado)
    {
        Assert.Equal(esperado, DescritorArmazenamento.NomeEstiloParticao(estilo));
    }

    [FatoWindows]
    public void Maquina_real_tem_ao_menos_um_disco_com_tamanho_e_modelo()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var discos = new FonteDiscosWindows().Discos();

        Assert.Contains(discos, d => d.TamanhoBytes > 0 && DescritorArmazenamento.Interpretar(d.Descritor)?.Modelo is not null);
    }
}
