using System.Text.RegularExpressions;

namespace MapHard.Testes;

/// <summary>
/// Confere o que a página e a verificação jurídica (docs/legal/verificacao-pagina-maphard.md) prometem: o programa
/// não tem código de rede, a página não cita os programas de referência, só aponta para os endereços esperados e
/// usa imagens que existem.
/// </summary>
public partial class PaginaTestes
{
    private static string Raiz => CaracteresProibidosTestes.RaizDoRepositorio();

    // Os nomes vão em pedaços, para o próprio teste não citar os programas de referência.
    private static readonly string[] _nomesDeReferencia = ["CPU" + "-Z", "Crystal" + "DiskInfo", "Crystal" + " Dew"];

    private static readonly string[] _redes = ["HttpClient", "WebClient", "WebRequest", "Socket", "TcpClient", "UdpClient", "System.Net.Http", "Dns."];

    private static readonly string[] _enderecosPermitidos =
    [
        "https://maphard.manfred.com.br/",
        "https://github.com/manfredjr/maphard",
        "https://www.manfred.com.br",
        "https://www.cloudflare.com/pt-br/privacypolicy/",
        "https://docs.github.com/pt/site-policy/",
    ];

    private static IEnumerable<string> Paginas() => Directory.EnumerateFiles(Path.Combine(Raiz, "public"), "*.html");

    [Fact]
    public void Programa_nao_tem_codigo_de_rede()
    {
        var achados = Directory.EnumerateFiles(Path.Combine(Raiz, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(a => !a.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"))
            .SelectMany(a => _redes.Where(r => File.ReadAllText(a).Contains(r, StringComparison.Ordinal)).Select(r => $"{Path.GetFileName(a)}: {r}"))
            .ToList();

        Assert.True(achados.Count == 0, string.Join(Environment.NewLine, achados));
    }

    [Fact]
    public void Pagina_e_readme_nao_citam_os_programas_de_referencia()
    {
        foreach (var arquivo in Paginas().Append(Path.Combine(Raiz, "README.md")))
        {
            var texto = File.ReadAllText(arquivo);
            Assert.All(_nomesDeReferencia, nome => Assert.DoesNotContain(nome, texto, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Pagina_so_aponta_para_os_enderecos_esperados()
    {
        var fora = Paginas()
            .SelectMany(p => Endereco().Matches(File.ReadAllText(p)).Select(m => m.Groups[1].Value))
            .Where(e => e.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Where(e => !_enderecosPermitidos.Any(p => e.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        Assert.True(fora.Count == 0, string.Join(Environment.NewLine, fora));
    }

    [Fact]
    public void Imagens_e_estilo_da_pagina_existem()
    {
        foreach (var pagina in Paginas())
        {
            var locais = Endereco().Matches(File.ReadAllText(pagina)).Select(m => m.Groups[1].Value)
                .Where(e => !e.Contains(':', StringComparison.Ordinal) && !e.StartsWith('#') && e != "./");
            Assert.All(locais, e => Assert.True(File.Exists(Path.Combine(Raiz, "public", e)), $"{Path.GetFileName(pagina)}: {e}"));
        }
    }

    [Fact]
    public void Pagina_tem_os_textos_da_verificacao_juridica()
    {
        var pagina = File.ReadAllText(Path.Combine(Raiz, "public", "index.html"));

        Assert.Contains("<h2>Uso autorizado</h2>", pagina, StringComparison.Ordinal);
        Assert.Contains("<strong>Licença e garantias.</strong>", pagina, StringComparison.Ordinal);
        Assert.Contains("não é previsão de falha", pagina, StringComparison.Ordinal);
        Assert.Contains("PC Integridade do Computador", pagina, StringComparison.Ordinal);
        Assert.Contains("Windows 11 25H2", pagina, StringComparison.Ordinal);
    }

    [GeneratedRegex("(?:href|src)=\"([^\"]+)\"")]
    private static partial Regex Endereco();
}
