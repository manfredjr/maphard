using System.Text.Json;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Relatorios;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class ExportadorJsonTestes
{
    private static Task<ColetaMaquina> Coletar(FontesColeta? fontes = null) =>
        new Coletor(fontes ?? FontesSimuladas.Completas(), TimeSpan.FromSeconds(5), agora: () => new DateTimeOffset(2026, 9, 30, 10, 5, 0, TimeSpan.FromHours(-3))).ColetarAsync();

    [Fact]
    public async Task Campo_lido_sai_com_valor_estado_e_fonte()
    {
        var json = JsonDocument.Parse(ExportadorJson.Serializar(await Coletar())).RootElement;

        var nucleos = json.GetProperty("processador").GetProperty("nucleos");
        Assert.Equal(8, nucleos.GetProperty("valor").GetInt32());
        Assert.Equal("lido", nucleos.GetProperty("estado").GetString());
        Assert.Equal("topologia", nucleos.GetProperty("fonte").GetString());
        Assert.Equal(JsonValueKind.Null, nucleos.GetProperty("motivo").ValueKind);
        Assert.Equal("maphard-coleta", json.GetProperty("formato").GetString());
    }

    [Fact]
    public async Task Estados_sem_leitura_saem_com_valor_nulo_e_nome_certo()
    {
        var json = JsonDocument.Parse(ExportadorJson.Serializar(await Coletar())).RootElement;
        var processador = json.GetProperty("processador");
        var placa = json.GetProperty("placa");

        Assert.Equal("naoInformado", processador.GetProperty("litografia").GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, processador.GetProperty("litografia").GetProperty("valor").ValueKind);
        Assert.Equal("naoSuportado", processador.GetProperty("nucleosEficiencia").GetProperty("estado").GetString());
        Assert.Equal("requerAdministrador", placa.GetProperty("firmware").GetProperty("tpmFabricante").GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Erro_de_leitura_sai_com_o_motivo()
    {
        var smbios = new FontesSimuladas.Smbios(() => throw new InvalidOperationException("falha simulada"));

        var json = JsonDocument.Parse(ExportadorJson.Serializar(await Coletar(FontesSimuladas.Completas(smbios: smbios)))).RootElement;
        var modelo = json.GetProperty("placa").GetProperty("placaModelo");

        Assert.Equal("erroLeitura", modelo.GetProperty("estado").GetString());
        Assert.Equal("falha simulada", modelo.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task Ida_e_volta_mantem_a_coleta()
    {
        var original = await Coletar();

        var lida = ExportadorJson.Desserializar(ExportadorJson.Serializar(original))!;

        Assert.Equal(original.Identificacao.Computador.Valor, lida.Identificacao.Computador.Valor);
        Assert.Equal(original.Processador.Nucleos.Valor, lida.Processador.Nucleos.Valor);
        Assert.Equal(original.Processador.Nucleos.Fonte, lida.Processador.Nucleos.Fonte);
        Assert.Equal(original.Processador.Litografia.Estado, lida.Processador.Litografia.Estado);
        Assert.Equal(original.Processador.Caches.Valor!.Count, lida.Processador.Caches.Valor!.Count);
        Assert.Equal(original.Placa.BiosData.Valor, lida.Placa.BiosData.Valor);
        Assert.Equal(original.ColetadoEm, lida.ColetadoEm);
        Assert.Equal(ExportadorJson.Serializar(original), ExportadorJson.Serializar(lida));
    }

    [Fact]
    public async Task Formato_traz_a_secao_memoria()
    {
        var json = JsonDocument.Parse(ExportadorJson.Serializar(await Coletar())).RootElement;
        var memoria = json.GetProperty("memoria");
        var modulos = memoria.GetProperty("modulos").GetProperty("valor");

        Assert.Equal(3, json.GetProperty("versaoFormato").GetInt32());
        Assert.Equal(17179869184, memoria.GetProperty("instalada").GetProperty("valor").GetInt64());
        Assert.Equal(4, modulos.GetArrayLength());
        Assert.True(modulos[1].GetProperty("vazio").GetBoolean());
        Assert.Equal("naoSuportado", modulos[1].GetProperty("tamanho").GetProperty("estado").GetString());
        Assert.Equal("PN-TESTE-3200", modulos[0].GetProperty("partNumber").GetProperty("valor").GetString());
        Assert.Equal(JsonValueKind.Array, memoria.GetProperty("alertas").ValueKind);
        Assert.Equal("DDR4", json.GetProperty("identificacao").GetProperty("memoriaTipo").GetProperty("valor").GetString());
    }

    [Fact]
    public async Task Ida_e_volta_mantem_a_secao_memoria()
    {
        var original = await Coletar();

        var lida = ExportadorJson.Desserializar(ExportadorJson.Serializar(original))!;

        Assert.Equal(original.Memoria.Modulos.Valor!.Count, lida.Memoria.Modulos.Valor!.Count);
        Assert.Equal(original.Memoria.Modulos.Valor[0].Fabricante, lida.Memoria.Modulos.Valor[0].Fabricante);
        Assert.Equal(original.Memoria.Ampliacao, lida.Memoria.Ampliacao);
        Assert.Equal(original.Memoria.Instalada, lida.Memoria.Instalada);
    }

    [Fact]
    public async Task Acentos_saem_sem_escape()
    {
        var json = ExportadorJson.Serializar(await Coletar());

        Assert.Contains("sem núcleos de eficiência", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u00FA", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ESTACAO-01", "maphard-ESTACAO-01-2026-09-30-1005.json")]
    [InlineData("A:B/C*D?", "maphard-A-B-C-D--2026-09-30-1005.json")]
    [InlineData("   ", "maphard-computador-2026-09-30-1005.json")]
    public void Nome_padrao_do_arquivo(string computador, string esperado)
    {
        Assert.Equal(esperado, ExportadorJson.NomePadrao(computador, new DateTimeOffset(2026, 9, 30, 10, 5, 0, TimeSpan.FromHours(-3))));
    }

    [Fact]
    public async Task Grava_em_utf8_sem_bom()
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "teste-json-sem-bom.json");
        try
        {
            ExportadorJson.Gravar(await Coletar(), caminho);

            var bytes = File.ReadAllBytes(caminho);
            Assert.Equal((byte)'{', bytes[0]);
        }
        finally
        {
            File.Delete(caminho);
        }
    }
}
