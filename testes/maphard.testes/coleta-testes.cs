using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class ColetaTestes
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(5);

    private static Task<ColetaMaquina> Coletar(FontesColeta fontes, TimeSpan? limite = null) =>
        new Coletor(fontes, limite ?? Limite, agora: () => new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.FromHours(-3))).ColetarAsync();

    [Fact]
    public async Task Coleta_completa_da_maquina_ficticia()
    {
        var c = await Coletar(FontesSimuladas.Completas());

        Assert.Equal("maphard-coleta", c.Formato);
        Assert.Equal("ESTACAO-TESTE", c.Identificacao.Computador.Valor);
        Assert.Equal("Fabricante Exemplo", c.Identificacao.Fabricante.Valor);
        Assert.Equal("SERIE-TESTE-0001", c.Identificacao.NumeroSerie.Valor);
        Assert.Equal("Windows 11 Pro", c.Identificacao.Windows.Nome.Valor);

        var p = c.Processador;
        Assert.Equal("Processador de Teste 3.60GHz", p.Nome.Valor);
        Assert.Equal("Intel", p.Fabricante.Valor);
        Assert.Equal("Coffee Lake", p.Codinome.Valor);
        Assert.Equal(EstadoCampo.NaoInformado, p.Litografia.Estado);
        Assert.Equal("SOQUETE 1", p.Soquete.Valor);
        Assert.Equal(8, p.Nucleos.Valor);
        Assert.Equal(16, p.Threads.Valor);
        Assert.Equal(EstadoCampo.NaoSuportado, p.NucleosEficiencia.Estado);
        Assert.Equal(4, p.Caches.Valor!.Count);
        Assert.Equal(4500, p.Clocks.Atual.Valor);
        Assert.True(p.VirtualizacaoNoProcessador.Valor);

        var placa = c.Placa;
        Assert.Equal("PLACA-X1", placa.PlacaModelo.Valor);
        Assert.Equal("desktop", placa.Gabinete.Valor);
        Assert.Equal(new DateOnly(2021, 3, 15), placa.BiosData.Valor);
        Assert.Equal("UEFI", placa.Firmware.Modo.Valor);
        Assert.False(placa.MaquinaVirtual.Valor);
    }

    [Fact]
    public async Task Fonte_que_lanca_excecao_deixa_so_os_campos_dela_em_erro()
    {
        var smbios = new FontesSimuladas.Smbios(() => throw new InvalidOperationException("falha simulada"));

        var c = await Coletar(FontesSimuladas.Completas(smbios: smbios));

        Assert.Equal(EstadoCampo.ErroLeitura, c.Placa.PlacaModelo.Estado);
        Assert.Equal("falha simulada", c.Placa.PlacaModelo.Motivo);
        Assert.Equal(EstadoCampo.ErroLeitura, c.Placa.BiosData.Estado);
        Assert.Equal(EstadoCampo.ErroLeitura, c.Processador.Soquete.Estado);
        Assert.Equal("Processador de Teste 3.60GHz", c.Processador.Nome.Valor);
        Assert.Equal(8, c.Processador.Nucleos.Valor);
    }

    [Fact]
    public async Task Fonte_que_passa_do_tempo_vira_tempo_esgotado()
    {
        var topologia = new FontesSimuladas.Topologia(() =>
        {
            Thread.Sleep(2000);
            return FontesSimuladas.TabelaTopologia();
        });

        var c = await Coletar(FontesSimuladas.Completas(topologia: topologia), TimeSpan.FromMilliseconds(200));

        Assert.Equal(EstadoCampo.ErroLeitura, c.Processador.Caches.Estado);
        Assert.Equal("tempo esgotado", c.Processador.Caches.Motivo);
    }

    [Fact]
    public async Task Sem_topologia_os_nucleos_vem_do_smbios()
    {
        var c = await Coletar(FontesSimuladas.Completas(topologia: new FontesSimuladas.Topologia(() => null)));

        Assert.Equal(8, c.Processador.Nucleos.Valor);
        Assert.Equal(FonteDado.Smbios, c.Processador.Nucleos.Fonte);
        Assert.Equal(16, c.Processador.Threads.Valor);
    }

    [Fact]
    public async Task Divergencia_de_nucleos_vale_a_topologia_com_o_motivo()
    {
        var smbios = new FontesSimuladas.Smbios(() => FontesSimuladas.TabelaSmbios(nucleosSmbios: 6));

        var c = await Coletar(FontesSimuladas.Completas(smbios: smbios));

        Assert.Equal(8, c.Processador.Nucleos.Valor);
        Assert.Equal(FonteDado.Topologia, c.Processador.Nucleos.Fonte);
        Assert.Equal("o SMBIOS informa 6", c.Processador.Nucleos.Motivo);
    }

    [Fact]
    public async Task Fabricante_de_fabrica_cai_para_a_placa_mae()
    {
        var smbios = new FontesSimuladas.Smbios(() => FontesSimuladas.TabelaSmbios(fabricanteSistema: "To Be Filled By O.E.M."));

        var c = await Coletar(FontesSimuladas.Completas(smbios: smbios));

        Assert.Equal(EstadoCampo.NaoInformado, c.Placa.EquipamentoFabricante.Estado);
        Assert.Equal("Fabricante Placa", c.Identificacao.Fabricante.Valor);
        Assert.Equal("da placa-mãe", c.Identificacao.Fabricante.Motivo);
    }

    [Fact]
    public async Task Processador_fora_da_tabela()
    {
        var cpuid = CpuidSimulado.Base(assinatura: 0x000A0F00).NomeComercial("Processador Novo");

        var c = await Coletar(FontesSimuladas.Completas(cpuid: cpuid));

        Assert.Equal(EstadoCampo.NaoInformado, c.Processador.Codinome.Estado);
        Assert.Equal("não consta na tabela do MapHard", c.Processador.Codinome.Motivo);
    }

    [Fact]
    public async Task Cpuid_indisponivel_usa_o_nome_do_smbios()
    {
        var c = await Coletar(FontesSimuladas.Completas(cpuid: new CpuidSimulado { Disponivel = false }));

        Assert.Equal("Processador Exemplo", c.Processador.Nome.Valor);
        Assert.Equal(FonteDado.Smbios, c.Processador.Nome.Fonte);
        Assert.Equal(EstadoCampo.NaoSuportado, c.Processador.Instrucoes.Estado);
    }

    [Fact]
    public async Task Nenhum_campo_fica_sem_estado_valido()
    {
        var c = await Coletar(FontesSimuladas.Completas(smbios: new FontesSimuladas.Smbios(() => null)));

        foreach (var campo in Campos(c))
        {
            Assert.True(Enum.IsDefined(campo.Estado));
            Assert.True(campo.Estado == EstadoCampo.Lido || campo.Valor is null || campo.Valor.Equals(false) || campo.Valor.Equals(0) || campo.Valor.Equals(0L) || campo.Valor.Equals(0.0) || campo.Valor.Equals(default(DateOnly)));
        }
    }

    /// <summary>Todos os campos da coleta, por reflexão, com o estado e o valor.</summary>
    internal static IEnumerable<(EstadoCampo Estado, object? Valor)> Campos(object raiz)
    {
        foreach (var propriedade in raiz.GetType().GetProperties())
        {
            var valor = propriedade.GetValue(raiz);
            if (valor is null)
            {
                continue;
            }

            var tipo = valor.GetType();
            if (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(Campo<>))
            {
                yield return ((EstadoCampo)tipo.GetProperty("Estado")!.GetValue(valor)!, tipo.GetProperty("Valor")!.GetValue(valor));
            }
            else if (tipo.Namespace?.StartsWith("MapHard", StringComparison.Ordinal) == true && !tipo.IsEnum)
            {
                foreach (var interno in Campos(valor))
                {
                    yield return interno;
                }
            }
        }
    }
}
