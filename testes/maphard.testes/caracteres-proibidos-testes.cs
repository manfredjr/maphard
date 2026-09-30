namespace MapHard.Testes;

/// <summary>
/// Confere as regras de texto do AGENTS.md em todo arquivo de código e de documentação:
/// sem travessão, aspas curvas, reticências de um caractere, espaço especial, seta, marcador
/// solto, sinal de multiplicação ou de menos unicode.
/// </summary>
public class CaracteresProibidosTestes
{
    private static readonly Dictionary<char, string> _proibidos = new()
    {
        ['\u2014'] = "travessão longo",
        ['\u2013'] = "travessão médio",
        ['\u201C'] = "aspas curvas",
        ['\u201D'] = "aspas curvas",
        ['\u2018'] = "aspas curvas simples",
        ['\u2019'] = "aspas curvas simples",
        ['\u2026'] = "reticências de um caractere",
        ['\u00A0'] = "espaço sem quebra",
        ['\u202F'] = "espaço estreito",
        ['\u200B'] = "espaço de largura zero",
        ['\u2022'] = "marcador solto",
        ['\u2192'] = "seta",
        ['\u2190'] = "seta",
        ['\u21D2'] = "seta",
        ['\u00D7'] = "sinal de multiplicação",
        ['\u2212'] = "sinal de menos unicode",
    };

    // Exceções do AGENTS.md: o MSBuild só lê o Directory.Build.props com essa grafia no
    // Linux, AGENTS.md, README.md, CONTRIBUTING.md e LICENSE são convenção de repositório, e as consultas ao
    // advogado seguem o nome do método.
    private static readonly string[] _nomesFixos = ["AGENTS.md", "README.md", "CONTRIBUTING.md", "LICENSE", "Directory.Build.props"];

    // A busca pula o que não consegue ler, para uma pasta sem permissão não derrubar o teste.
    private static readonly EnumerationOptions _todasAsPastas = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

    private static readonly string[] _extensoes = [".cs", ".md", ".csproj", ".props", ".json", ".manifest", ".ps1", ".cmd", ".txt", ".html", ".yml", ".xaml"];

    [Fact]
    public void Codigo_e_documentacao_nao_tem_caractere_proibido()
    {
        var raiz = RaizDoRepositorio();
        var problemas = new List<string>();
        foreach (var arquivo in Directory.EnumerateFiles(raiz, "*", _todasAsPastas))
        {
            var relativo = Path.GetRelativePath(raiz, arquivo).Replace('\\', '/');
            if (relativo.Split('/').Any(p => p is "bin" or "obj" or ".git" or ".vs" or "publicar" or ".superpowers")
                || !_extensoes.Contains(Path.GetExtension(arquivo).ToLowerInvariant()))
            {
                continue;
            }

            var linhas = File.ReadAllLines(arquivo);
            for (var i = 0; i < linhas.Length; i++)
            {
                problemas.AddRange(Proibidos(linhas[i]).Select(p => $"{relativo}:{i + 1}: {p}"));
            }
        }

        Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
    }

    [Fact]
    public void Nome_de_arquivo_e_minusculo()
    {
        var raiz = RaizDoRepositorio();
        var fora = Directory.EnumerateFileSystemEntries(raiz, "*", _todasAsPastas)
            .Select(c => Path.GetRelativePath(raiz, c).Replace('\\', '/'))
            .Where(c => !c.Split('/').Any(p => p is "bin" or "obj" or ".git" or ".vs" or "publicar" or ".superpowers" or "TestResults"))
            .Where(c => !_nomesFixos.Contains(Path.GetFileName(c)) && !Path.GetFileName(c).StartsWith("CONSULTA-ADVOGADO-", StringComparison.Ordinal))
            .Where(c => Path.GetFileName(c) != Path.GetFileName(c).ToLowerInvariant())
            .ToList();

        Assert.True(fora.Count == 0, string.Join(Environment.NewLine, fora));
    }

    internal static IEnumerable<string> Proibidos(string texto) =>
        texto.Where(_proibidos.ContainsKey).Select(c => $"U+{(int)c:X4} ({_proibidos[c]})");

    internal static string RaizDoRepositorio()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta != null && !File.Exists(Path.Combine(pasta.FullName, "maphard.sln")))
        {
            pasta = pasta.Parent;
        }

        return pasta?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
