using System.Buffers.Binary;
using MapHard.Nucleo.Smbios;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class SmbiosTestes
{
    private static TabelaSmbios Interpretar(ConstrutorSmbios construtor) => LeitorTabelaSmbios.Interpretar(construtor.Montar())!;

    [Fact]
    public void Divide_tres_estruturas_e_o_fim()
    {
        var tabela = Interpretar(new ConstrutorSmbios()
            .Estrutura(0, ConstrutorSmbios.Corpo(0x12, (0x04, 1), (0x05, 2)), "Fabricante BIOS", "1.0")
            .Estrutura(1, ConstrutorSmbios.Corpo(0x08, (0x04, 1)), "Fabricante Exemplo")
            .Estrutura(2, ConstrutorSmbios.Corpo(0x08))
            .Fim());

        Assert.Equal(new Version(3, 4), tabela.Versao);
        Assert.Equal([0, 1, 2, 127], tabela.Estruturas.Select(e => (int)e.Tipo));
        Assert.Equal(["Fabricante BIOS", "1.0"], tabela.Estruturas[0].Textos);
        Assert.Empty(tabela.Estruturas[2].Textos);
    }

    [Fact]
    public void Indice_de_texto_zero_ou_alem_da_lista_devolve_nulo()
    {
        var tabela = Interpretar(new ConstrutorSmbios()
            .Estrutura(2, ConstrutorSmbios.Corpo(0x08, (0x04, 0), (0x05, 1), (0x06, 5)), "Placa Exemplo")
            .Fim());

        var e = tabela.Estruturas[0];
        Assert.Null(e.Texto(0x04));
        Assert.Equal("Placa Exemplo", e.Texto(0x05));
        Assert.Null(e.Texto(0x06));
        Assert.Null(e.Texto(0x40));
        Assert.Null(e.Byte(-1));
        Assert.Null(e.Palavra(0x07));
    }

    [Fact]
    public void Tabela_truncada_devolve_as_estruturas_completas_sem_excecao()
    {
        var bruta = new ConstrutorSmbios()
            .Estrutura(1, ConstrutorSmbios.Corpo(0x08, (0x04, 1)), "Fabricante Exemplo")
            .Estrutura(2, ConstrutorSmbios.Corpo(0x08, (0x04, 1)), "Placa Exemplo")
            .Fim()
            .Montar();

        // Corta no meio dos textos da segunda estrutura.
        var cortada = bruta[..(8 + 8 + 20 + 8 + 4)];
        BinaryPrimitives.WriteUInt32LittleEndian(cortada.AsSpan(4), (uint)(cortada.Length - 8));

        var tabela = LeitorTabelaSmbios.Interpretar(cortada)!;

        Assert.Single(tabela.Estruturas);
        Assert.Equal(1, tabela.Estruturas[0].Tipo);
    }

    [Fact]
    public void Tamanho_declarado_maior_que_o_buffer_nao_passa_do_buffer()
    {
        var bruta = new ConstrutorSmbios().Estrutura(1, ConstrutorSmbios.Corpo(0x08)).Fim().Montar();
        BinaryPrimitives.WriteUInt32LittleEndian(bruta.AsSpan(4), 100000);

        var tabela = LeitorTabelaSmbios.Interpretar(bruta)!;

        Assert.Equal(2, tabela.Estruturas.Count);
    }

    [Fact]
    public void Estrutura_com_tamanho_menor_que_o_cabecalho_encerra_a_leitura()
    {
        byte[] bruta = [0, 3, 4, 0, 8, 0, 0, 0, 1, 2, 0, 0, 0, 0, 0, 0];

        Assert.Empty(LeitorTabelaSmbios.Interpretar(bruta)!.Estruturas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Buffer_vazio_ou_curto_devolve_nulo(int tamanho)
    {
        Assert.Null(LeitorTabelaSmbios.Interpretar(new byte[tamanho]));
    }

    [Fact]
    public void Buffer_nulo_devolve_nulo()
    {
        Assert.Null(LeitorTabelaSmbios.Interpretar(null));
    }

    [Fact]
    public void Byte_fora_do_ascii_imprimivel_vira_ponto()
    {
        var bruta = new ConstrutorSmbios().Estrutura(2, ConstrutorSmbios.Corpo(0x08, (0x04, 1)), "X").Fim().Montar();
        bruta[8 + 8] = 0xE9;

        Assert.Equal(".", LeitorTabelaSmbios.Interpretar(bruta)!.Estruturas[0].Texto(0x04));
    }

    [Fact]
    public void Bios_com_data_uefi_e_maquina_virtual()
    {
        var tabela = Interpretar(new ConstrutorSmbios()
            .Estrutura(0, ConstrutorSmbios.Corpo(0x18, (0x04, 1), (0x05, 2), (0x08, 3), (0x13, 0x08 | 0x10)), "Fabricante BIOS", "F.10", "03/15/2021")
            .Fim());

        var bios = BiosSmbios.De(tabela)!;

        Assert.Equal("Fabricante BIOS", bios.Fabricante);
        Assert.Equal("F.10", bios.Versao);
        Assert.Equal(new DateOnly(2021, 3, 15), bios.Data);
        Assert.True(bios.SuportaUefi);
        Assert.True(bios.MaquinaVirtual);
    }

    [Fact]
    public void Bios_curta_nao_informa_uefi()
    {
        var tabela = Interpretar(new ConstrutorSmbios().Estrutura(0, ConstrutorSmbios.Corpo(0x12, (0x04, 1)), "Fabricante BIOS").Fim());

        var bios = BiosSmbios.De(tabela)!;

        Assert.Null(bios.SuportaUefi);
        Assert.Null(bios.Data);
    }

    [Theory]
    [InlineData("12/31/99", 1999)]
    [InlineData("01/02/2024", 2024)]
    public void Data_da_bios_com_dois_ou_quatro_digitos(string texto, int ano)
    {
        Assert.Equal(ano, BiosSmbios.LerData(texto)!.Value.Year);
    }

    [Fact]
    public void Data_da_bios_invalida_devolve_nulo()
    {
        Assert.Null(BiosSmbios.LerData("sem data"));
    }

    [Fact]
    public void Sistema_com_uuid_em_little_endian_a_partir_da_versao_2_6()
    {
        byte[] uuid = [0x33, 0x22, 0x11, 0x00, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];
        var corpo = ConstrutorSmbios.Corpo(0x1B, (0x04, 1), (0x05, 2), (0x07, 3), (0x19, 4), (0x1A, 5));
        uuid.CopyTo(corpo, 0x08 - 4);

        var tabela = Interpretar(new ConstrutorSmbios()
            .Estrutura(1, corpo, "Fabricante Exemplo", "Modelo Exemplo", "SERIE-TESTE-0001", "SKU-TESTE", "Linha Exemplo")
            .Fim());

        var sistema = SistemaSmbios.De(tabela)!;

        Assert.Equal("Fabricante Exemplo", sistema.Fabricante);
        Assert.Equal("Modelo Exemplo", sistema.Produto);
        Assert.Null(sistema.Versao);
        Assert.Equal("SERIE-TESTE-0001", sistema.NumeroSerie);
        Assert.Equal("00112233-4455-6677-8899-AABBCCDDEEFF", sistema.Uuid);
        Assert.Equal("SKU-TESTE", sistema.Sku);
        Assert.Equal("Linha Exemplo", sistema.Familia);
    }

    [Fact]
    public void Uuid_antes_da_versao_2_6_fica_na_ordem_da_tabela()
    {
        byte[] uuid = [0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];

        Assert.Equal("00112233-4455-6677-8899-AABBCCDDEEFF", SistemaSmbios.LerUuid(uuid, 0x0205));
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0xFF)]
    public void Uuid_todo_zero_ou_todo_ff_devolve_nulo(byte valor)
    {
        Assert.Null(SistemaSmbios.LerUuid(Enumerable.Repeat(valor, 16).ToArray(), 0x0304));
    }

    [Fact]
    public void Placa_mae()
    {
        var tabela = Interpretar(new ConstrutorSmbios()
            .Estrutura(2, ConstrutorSmbios.Corpo(0x08, (0x04, 1), (0x05, 2), (0x06, 3), (0x07, 4)), "Fabricante Placa", "PLACA-X1", "1.0", "SERIE-TESTE-0002")
            .Fim());

        Assert.Equal(new PlacaSmbios("Fabricante Placa", "PLACA-X1", "1.0", "SERIE-TESTE-0002"), PlacaSmbios.De(tabela));
    }

    [Fact]
    public void Gabinete_notebook_com_bit_de_trava()
    {
        var tabela = Interpretar(new ConstrutorSmbios()
            .Estrutura(3, ConstrutorSmbios.Corpo(0x09, (0x04, 1), (0x05, 0x80 | 0x0A)), "Fabricante Exemplo")
            .Fim());

        var gabinete = GabineteSmbios.De(tabela)!;

        Assert.Equal((byte)0x0A, gabinete.CodigoTipo);
        Assert.Equal("notebook", gabinete.Tipo);
        Assert.True(gabinete.EhPortatil);
    }

    [Theory]
    [InlineData(0x03, "desktop")]
    [InlineData(0x0D, "all-in-one")]
    [InlineData(0x23, "mini PC")]
    [InlineData(0x24, "stick PC")]
    [InlineData(0x00, null)]
    [InlineData(0x25, null)]
    public void Nome_do_tipo_de_gabinete(byte codigo, string? esperado)
    {
        Assert.Equal(esperado, GabineteSmbios.NomeTipo(codigo));
    }

    [Fact]
    public void Processador_com_contagens_de_um_byte()
    {
        var tabela = Interpretar(new ConstrutorSmbios()
            .Estrutura(4, ProcessadorCorpo(0x30, nucleos: 8, ativos: 8, threads: 16), "SOQUETE 1", "Fabricante CPU", "Processador Exemplo")
            .Fim());

        var cpu = Assert.Single(ProcessadorSmbios.Todos(tabela));

        Assert.Equal("SOQUETE 1", cpu.Soquete);
        Assert.Equal("Processador Exemplo", cpu.Versao);
        Assert.Equal(100, cpu.ClockExternoMhz);
        Assert.Equal(4700, cpu.ClockMaximoMhz);
        Assert.Equal(8, cpu.Nucleos);
        Assert.Equal(16, cpu.Threads);
    }

    [Fact]
    public void Processador_com_0xff_usa_o_campo_de_dois_bytes()
    {
        var corpo = ProcessadorCorpo(0x30, nucleos: 0xFF, ativos: 0xFF, threads: 0xFF);
        BinaryPrimitives.WriteUInt16LittleEndian(corpo.AsSpan(0x2A - 4), 128);
        BinaryPrimitives.WriteUInt16LittleEndian(corpo.AsSpan(0x2C - 4), 128);
        BinaryPrimitives.WriteUInt16LittleEndian(corpo.AsSpan(0x2E - 4), 256);

        var cpu = Assert.Single(ProcessadorSmbios.Todos(Interpretar(new ConstrutorSmbios().Estrutura(4, corpo, "S", "F", "V").Fim())));

        Assert.Equal(128, cpu.Nucleos);
        Assert.Equal(128, cpu.NucleosAtivos);
        Assert.Equal(256, cpu.Threads);
    }

    [Fact]
    public void Soquete_vazio_fica_fora_da_lista()
    {
        var corpo = ProcessadorCorpo(0x30, nucleos: 8, ativos: 8, threads: 16);
        corpo[0x18 - 4] = 0x00;

        Assert.Empty(ProcessadorSmbios.Todos(Interpretar(new ConstrutorSmbios().Estrutura(4, corpo, "S", "F", "V").Fim())));
    }

    [Fact]
    public void Processador_antigo_sem_contagens()
    {
        var corpo = ProcessadorCorpo(0x1A, nucleos: 0, ativos: 0, threads: 0);

        var cpu = Assert.Single(ProcessadorSmbios.Todos(Interpretar(new ConstrutorSmbios().Estrutura(4, corpo, "S", "F", "V").Fim())));

        Assert.Null(cpu.Nucleos);
        Assert.Null(cpu.Threads);
    }

    [FatoWindows]
    public void Tabela_real_tem_o_tipo_1()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tabela = LeitorTabelaSmbios.Interpretar(new FonteSmbiosWindows().LerTabelaBruta());

        Assert.NotNull(tabela);
        Assert.NotNull(tabela.PrimeiraDoTipo(1));
    }

    private static byte[] ProcessadorCorpo(int tamanho, byte nucleos, byte ativos, byte threads)
    {
        var corpo = new byte[tamanho - 4];
        corpo[0x04 - 4] = 1;
        corpo[0x07 - 4] = 2;
        corpo[0x10 - 4] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(corpo.AsSpan(0x12 - 4), 100);
        BinaryPrimitives.WriteUInt16LittleEndian(corpo.AsSpan(0x14 - 4), 4700);
        corpo[0x18 - 4] = 0x41;
        if (tamanho >= 0x28)
        {
            corpo[0x23 - 4] = nucleos;
            corpo[0x24 - 4] = ativos;
            corpo[0x25 - 4] = threads;
        }

        return corpo;
    }
}
