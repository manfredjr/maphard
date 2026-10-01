using System.Buffers.Binary;
using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Smart;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class SmartAtaTestes
{
    /// <summary>Tabela de valores de 512 bytes: id, atual, pior e bruto. A soma de verificação fecha em zero.</summary>
    internal static byte[] Valores(params (byte Id, byte Atual, byte Pior, ulong Bruto)[] atributos)
    {
        var bloco = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(bloco, 0x0010);
        for (var i = 0; i < atributos.Length; i++)
        {
            var p = 2 + (i * 12);
            bloco[p] = atributos[i].Id;
            BinaryPrimitives.WriteUInt16LittleEndian(bloco.AsSpan(p + 1), 0x0033);
            bloco[p + 3] = atributos[i].Atual;
            bloco[p + 4] = atributos[i].Pior;
            for (var b = 0; b < 6; b++)
            {
                bloco[p + 5 + b] = (byte)(atributos[i].Bruto >> (8 * b));
            }
        }

        return Fechar(bloco);
    }

    internal static byte[] Limites(params (byte Id, byte Limite)[] limites)
    {
        var bloco = new byte[512];
        for (var i = 0; i < limites.Length; i++)
        {
            bloco[2 + (i * 12)] = limites[i].Id;
            bloco[2 + (i * 12) + 1] = limites[i].Limite;
        }

        return Fechar(bloco);
    }

    internal static byte[] Identificacao(ushort palavra77, ushort palavra217)
    {
        var id = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(id.AsSpan(77 * 2), palavra77);
        BinaryPrimitives.WriteUInt16LittleEndian(id.AsSpan(217 * 2), palavra217);
        return id;
    }

    private static byte[] Fechar(byte[] bloco)
    {
        byte soma = 0;
        for (var i = 0; i < 511; i++)
        {
            soma += bloco[i];
        }

        bloco[511] = (byte)(256 - soma);
        return bloco;
    }

    private static LeituraSmartAta Ler(byte[]? valores, byte[]? limites = null, byte? baixo = 0x4F, byte? alto = 0xC2, byte[]? id = null) =>
        LeitorSmartAta.Interpretar(new SmartAtaBruto(id ?? Identificacao(0x0006, 7200), valores, limites, baixo, alto, null));

    [Fact]
    public void Atributos_com_bruto_de_6_bytes_e_limite_pelo_id()
    {
        var l = Ler(Valores((0x05, 100, 100, 3), (0x09, 95, 95, 12_345), (0xC2, 64, 40, 0x0000_2D00_1E00_0024)), Limites((0x05, 36), (0x09, 0)));

        Assert.Equal(3, l.Atributos.Count);
        Assert.Equal((byte)36, l.Atributos[0].Limite);
        Assert.Equal(3ul, l.Atributos[0].Bruto);
        Assert.Equal((byte)0, l.Atributos[1].Limite);
        Assert.Equal(12_345ul, l.Atributos[1].Bruto);
        Assert.Null(l.Atributos[2].Limite);
        Assert.Equal(0x0000_2D00_1E00_0024ul, l.Atributos[2].Bruto);
        Assert.Equal((ushort)0x0033, l.Atributos[0].Flags);
        Assert.Null(l.Observacao);
        Assert.Null(l.Falha);
    }

    [Fact]
    public void Id_zero_e_ignorado_e_sem_limites_o_limite_fica_nulo()
    {
        var l = Ler(Valores((0, 1, 1, 1), (0x05, 100, 100, 0)));

        var a = Assert.Single(l.Atributos);
        Assert.Null(a.Limite);
    }

    [Fact]
    public void Soma_de_verificacao_errada_vai_na_observacao_sem_descartar()
    {
        var valores = Valores((0x05, 100, 100, 0));
        valores[511]++;

        var l = Ler(valores);

        Assert.Single(l.Atributos);
        Assert.Equal("soma de verificação da tabela SMART não confere", l.Observacao);
    }

    [Theory]
    [InlineData((byte)0xF4, (byte)0x2C, true)]
    [InlineData((byte)0x4F, (byte)0xC2, false)]
    [InlineData((byte)0x00, (byte)0x00, null)]
    public void Status_pelos_registradores(byte baixo, byte alto, bool? falha)
    {
        Assert.Equal(falha, Ler(Valores((0x05, 100, 100, 0)), baixo: baixo, alto: alto).FalhaPrevista);
    }

    [Fact]
    public void Status_sem_resposta_fica_nulo()
    {
        Assert.Null(Ler(Valores((0x05, 100, 100, 0)), baixo: null, alto: null).FalhaPrevista);
    }

    [Theory]
    [InlineData((ushort)0x0006, (ushort)7200, 7200, false, "SATA 6 Gb/s")]
    [InlineData((ushort)0x0004, (ushort)1, null, true, "SATA 3 Gb/s")]
    [InlineData((ushort)0x0002, (ushort)0, null, null, "SATA 1,5 Gb/s")]
    [InlineData((ushort)0x0007, (ushort)0xFFFF, null, null, null)]
    [InlineData((ushort)0x0000, (ushort)0x0300, null, null, null)]
    public void Rotacao_e_velocidade_sata_pelo_identify(ushort palavra77, ushort palavra217, int? rpm, bool? ssd, string? sata)
    {
        var l = Ler(Valores((0x05, 100, 100, 0)), id: Identificacao(palavra77, palavra217));

        Assert.Equal(rpm, l.RotacaoRpm);
        Assert.Equal(ssd, l.Ssd);
        Assert.Equal(sata, l.VelocidadeSata);
    }

    [Fact]
    public void Sem_tabela_volta_o_motivo_e_mantem_o_identify()
    {
        var l = Ler(null);

        Assert.Empty(l.Atributos);
        Assert.Equal("o disco não devolveu a tabela SMART", l.Falha);
        Assert.Equal(7200, l.RotacaoRpm);
    }

    [Fact]
    public void Falha_da_leitura_passa_adiante()
    {
        var l = LeitorSmartAta.Interpretar(new SmartAtaBruto(null, null, null, null, null, Volumes.RequerAdministrador));

        Assert.Equal(Volumes.RequerAdministrador, l.Falha);
        Assert.Empty(l.Atributos);
    }

    [Fact]
    public void ComandosAtaPermitidosTestes_so_leitura()
    {
        Assert.Equal([ComandoAta.Identificar, ComandoAta.LerValores, ComandoAta.LerLimites, ComandoAta.LerStatus], ComandosAta.Permitidos);
        Assert.Equal(Enum.GetValues<ComandoAta>(), ComandosAta.Permitidos);

        var registradores = ComandosAta.Permitidos.Select(ComandosAta.Registradores).ToList();
        Assert.Equal((byte)0xEC, registradores[0][6]);
        Assert.All(registradores.Skip(1), r => Assert.Equal((byte)0xB0, r[6]));
        Assert.Equal([(byte)0xD0, (byte)0xD1, (byte)0xDA], registradores.Skip(1).Select(r => r[0]));
        Assert.All(registradores.Skip(1), r => Assert.Equal(((byte)0x4F, (byte)0xC2), (r[3], r[4])));
    }

    [FatoWindows]
    public void Sem_administrador_o_smart_ata_pede_elevacao_sem_excecao()
    {
        if (!OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
        {
            return;
        }

        var bruto = new FonteDiscosWindows().SmartAta(0);

        Assert.Equal(Volumes.RequerAdministrador, bruto.Falha);
    }
}
