using System.Reflection;
using System.Text.RegularExpressions;

namespace MapHard.Nucleo.Windows11;

/// <summary>Onde o processador fica diante da lista do Windows 11.</summary>
public enum SituacaoProcessador
{
    NaLista,
    GeracaoAnterior,
    ForaDaLista,
}

/// <summary>A resposta: a situação, a série da lista que casou e a versão da lista.</summary>
public sealed record ProcessadorWindows11(SituacaoProcessador Situacao, string? Serie, string Versao);

/// <summary>
/// Listas de processadores do Windows 11 da Microsoft, embutidas no programa (<c>tabelas/processadores-windows11.csv</c>,
/// gerada por <c>ferramentas/gerar-processadores-windows11.ps1</c>). A lista da versão 25H2 é por série, não por modelo.
/// O casamento com o nome comercial é conservador: sem certeza, o processador fica fora da lista do MapHard, nunca
/// "não aceita", porque a Microsoft avisa que processadores lançados depois da lista também são aceitos.
/// </summary>
public sealed partial class TabelaWindows11
{
    private readonly List<(string Chave, string Serie)> _series = [];
    private readonly int? _primeiraGeracaoCore;
    private readonly int? _primeiraSerieRyzen;

    private TabelaWindows11(IEnumerable<(string Fabricante, string Produto, string Serie)> linhas, string versao)
    {
        Versao = versao;
        foreach (var (fabricante, produto, serie) in linhas)
        {
            if (ChaveDaLinha(fabricante, produto, serie) is { } chave)
            {
                _series.Add((chave, serie));
            }
        }

        var geracoes = _series.Select(s => GeracaoCore().Match(s.Chave)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).ToList();
        _primeiraGeracaoCore = geracoes.Count > 0 ? geracoes.Min() : null;
        var ryzen = _series.Select(s => SerieRyzen().Match(s.Chave)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value)).ToList();
        _primeiraSerieRyzen = ryzen.Count > 0 ? ryzen.Min() : null;
    }

    public string Versao { get; }

    public int Quantidade => _series.Count;

    private static readonly Lazy<TabelaWindows11> _embutida = new(() => Ler(LerRecurso()));

    public static TabelaWindows11 Embutida => _embutida.Value;

    public ProcessadorWindows11 Verificar(string? nomeComercial)
    {
        var chave = nomeComercial is null ? null : ChaveDoProcessador(Normalizar(nomeComercial));
        if (chave is null)
        {
            return new ProcessadorWindows11(SituacaoProcessador.ForaDaLista, null, Versao);
        }

        foreach (var candidata in chave.Split('|'))
        {
            if (_series.FirstOrDefault(s => Casa(s.Chave, candidata)) is { Serie: { } serie })
            {
                return new ProcessadorWindows11(SituacaoProcessador.NaLista, serie, Versao);
            }
        }

        var core = GeracaoCore().Match(chave);
        var ryzen = SerieRyzen().Match(chave);
        var anterior = (core.Success && int.Parse(core.Groups[1].Value) < _primeiraGeracaoCore)
            || (ryzen.Success && int.Parse(ryzen.Groups[1].Value) < _primeiraSerieRyzen);
        return new ProcessadorWindows11(anterior ? SituacaoProcessador.GeracaoAnterior : SituacaoProcessador.ForaDaLista, null, Versao);
    }

    public static TabelaWindows11 Ler(string texto)
    {
        var linhas = new List<(string, string, string)>();
        string? versao = null;
        var numero = 0;
        var cabecalhoLido = false;
        foreach (var bruta in texto.Split('\n'))
        {
            numero++;
            var linha = bruta.TrimEnd('\r');
            if (linha.Length == 0 || linha.StartsWith('#'))
            {
                continue;
            }

            if (!cabecalhoLido)
            {
                cabecalhoLido = true;
                continue;
            }

            var campos = linha.Split(';');
            if (campos.Length != 5 || campos.Any(string.IsNullOrWhiteSpace) || (versao is not null && versao != campos[3]))
            {
                throw new FormatException($"Linha {numero} da tabela do Windows 11 está incompleta, sem fonte ou de outra versão.");
            }

            versao = campos[3];
            linhas.Add((campos[0], campos[1], campos[2]));
        }

        return new TabelaWindows11(linhas, versao ?? throw new FormatException("Tabela do Windows 11 vazia."));
    }

    // Chaves. Intel Core: "core-i5-g12", "core-ultra-s2", "core-s1". AMD: "amd:ryzen:7/4" (série 7000, 4 algarismos),
    // "amd:ryzen:ai3/3", "amd:ryzen:z1", "amd:epyc:7-3" (7xx3). Qualcomm: "qc:X1E". Série com número: "intel:celeron:N:4/4:".

    internal static string? ChaveDaLinha(string fabricante, string produto, string serie)
    {
        if (fabricante.StartsWith("Intel", StringComparison.OrdinalIgnoreCase))
        {
            if (LinhaCoreI().Match(serie) is { Success: true } a)
            {
                return $"core-{Nivel(a.Groups["nivel"].Value)}-g{a.Groups["geracao"].Value}";
            }

            if (LinhaCoreI14().Match(serie) is { Success: true } b)
            {
                return $"core-{Nivel(b.Groups["nivel"].Value)}-g{b.Groups["geracao"].Value}";
            }

            if (LinhaCoreSerie().Match(serie) is { Success: true } c)
            {
                return c.Groups["ultra"].Success ? $"core-ultra-s{c.Groups["serie"].Value}" : $"core-s{c.Groups["serie"].Value}";
            }

            if (LinhaNumerada().Match(serie) is { Success: true } d)
            {
                var marca = Marca(d.Groups["marca"].Value.Replace("Processor", string.Empty, StringComparison.Ordinal).Trim());
                var numero = d.Groups["numero"].Value;
                return $"intel:{marca}:{d.Groups["letras"].Value.ToUpperInvariant()}:{numero.TrimEnd('0')}/{numero.Length}:{d.Groups["sufixo"].Value.ToUpperInvariant()}";
            }

            return null;
        }

        if (fabricante.StartsWith("AMD", StringComparison.OrdinalIgnoreCase))
        {
            var linha = LinhaAmd(produto);
            if (linha == "epyc")
            {
                return EpycLinha().Match(serie) is { Success: true } e ? $"amd:epyc:{e.Groups[1].Value}-{e.Groups[2].Value}" : null;
            }

            if (AmdZ().Match(serie) is { Success: true } z)
            {
                return $"amd:{linha}:{(z.Groups["ai"].Success ? "ai" : string.Empty)}{z.Groups["z"].Value.ToLowerInvariant()}";
            }

            if (AmdNumero().Match(serie) is { Success: true } n)
            {
                var prefixo = n.Groups["max"].Success ? "aimax" : n.Groups["ai"].Success ? "ai" : string.Empty;
                var numero = n.Groups["numero"].Value;
                return $"amd:{linha}:{prefixo}{numero.TrimEnd('0')}/{numero.Length}";
            }

            return null;
        }

        return fabricante.StartsWith("Qualcomm", StringComparison.OrdinalIgnoreCase) && QualcommLinha().Match(serie) is { Success: true } q
            ? $"qc:X1{q.Groups[1].Value.ToUpperInvariant()}"
            : null;
    }

    /// <summary>Uma ou mais chaves separadas por "|", na ordem de preferência.</summary>
    internal static string? ChaveDoProcessador(string nome)
    {
        if (nome.Contains("Snapdragon", StringComparison.OrdinalIgnoreCase))
        {
            return QualcommNome().Match(nome) is { Success: true } q ? $"qc:X1{q.Groups[1].Value.ToUpperInvariant()}" : null;
        }

        if (nome.Contains("AMD", StringComparison.OrdinalIgnoreCase))
        {
            return ChaveAmd(nome);
        }

        if (!nome.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (CoreUltraNome().Match(nome) is { Success: true } u)
        {
            return $"core-{(u.Groups["ultra"].Success ? "ultra-" : string.Empty)}s{u.Groups["serie"].Value}";
        }

        if (CoreINome().Match(nome) is { Success: true } i)
        {
            var algarismos = i.Groups["numero"].Value;
            var geracao = algarismos.Length switch
            {
                5 => algarismos[..2],
                4 when algarismos[0] == '1' => algarismos[..2],
                4 => algarismos[..1],
                _ => "1",
            };
            return $"core-{Nivel(i.Groups["nivel"].Value)}-g{geracao}";
        }

        var marcaNome = MarcaIntel(nome);
        var depois = marcaNome.Length == 0 ? nome : nome[(nome.IndexOf(marcaNome.Split(' ')[0], StringComparison.OrdinalIgnoreCase) + marcaNome.Split(' ')[0].Length)..];
        return ModeloNome().Match(depois) is { Success: true } m
            ? $"intel:{Marca(marcaNome)}:{m.Groups["letras"].Value.ToUpperInvariant()}:{m.Groups["numero"].Value}:{m.Groups["sufixo"].Value.ToUpperInvariant()}"
            : null;
    }

    private static string? ChaveAmd(string nome)
    {
        var pro = ProNome().IsMatch(nome);
        var linha = nome.Contains("EPYC", StringComparison.OrdinalIgnoreCase) ? "epyc"
            : nome.Contains("Athlon", StringComparison.OrdinalIgnoreCase) ? "athlon"
            : nome.Contains("Threadripper", StringComparison.OrdinalIgnoreCase) ? (pro ? "threadripper-pro" : "threadripper")
            : nome.Contains("Ryzen", StringComparison.OrdinalIgnoreCase) ? (pro ? "ryzen-pro" : "ryzen")
            : null;
        if (linha is null)
        {
            return null;
        }

        if (linha == "epyc")
        {
            return EpycNome().Match(nome) is { Success: true } e ? $"amd:epyc:{e.Groups[1].Value}-{e.Groups[2].Value}" : null;
        }

        string? codigo = null;
        if (AmdZNome().Match(nome) is { Success: true } z)
        {
            codigo = $"{(z.Groups["ai"].Success ? "ai" : string.Empty)}{z.Groups["z"].Value.ToLowerInvariant()}";
        }
        else if (AmdAiNome().Match(nome) is { Success: true } ai)
        {
            codigo = $"{(ai.Groups["max"].Success ? "aimax" : "ai")}{ai.Groups["numero"].Value[..1]}/3";
        }
        else if (AmdNumeroNome().Match(nome) is { Success: true } n)
        {
            var numero = n.Groups["numero"].Value;
            codigo = $"{numero}#{numero.Length}";
        }

        if (codigo is null)
        {
            return null;
        }

        // A linha PRO cai para a comum quando a lista não traz a PRO daquela série: é o mesmo processador.
        var linhas = linha.EndsWith("-pro", StringComparison.Ordinal) ? new[] { linha, linha[..^4] } : [linha];
        return string.Join('|', linhas.Select(l => $"amd:{l}:{codigo}"));
    }

    /// <summary>Compara a chave da lista com a do processador. Número da lista: os algarismos sem os zeros do fim são o começo do modelo.</summary>
    private static bool Casa(string lista, string processador)
    {
        if (lista == processador)
        {
            return true;
        }

        if (processador.StartsWith("intel:", StringComparison.Ordinal) && lista.StartsWith("intel:", StringComparison.Ordinal))
        {
            var l = lista.Split(':');
            var p = processador.Split(':');
            var numero = l[3].Split('/');
            return l[1] == p[1] && l[2] == p[2] && p[3].Length == int.Parse(numero[1]) && p[3].StartsWith(numero[0], StringComparison.Ordinal)
                && p[4].StartsWith(l[4], StringComparison.Ordinal);
        }

        if (processador.Contains('#', StringComparison.Ordinal) && lista.StartsWith("amd:", StringComparison.Ordinal))
        {
            var p = processador.Split(':');
            var l = lista.Split(':');
            var numero = p[2].Split('#');
            var serie = l[2].Split('/');
            return l[1] == p[1] && serie.Length == 2 && !char.IsLetter(serie[0][0]) && numero[1] == serie[1] && numero[0].StartsWith(serie[0], StringComparison.Ordinal);
        }

        return false;
    }

    private static string Normalizar(string nome) =>
        Espacos().Replace(Simbolos().Replace(nome, " "), " ").Trim();

    private static string Nivel(string nivel) => nivel.StartsWith('m') ? "m" : nivel.ToLowerInvariant();

    private static string Marca(string marca) => marca.ToLowerInvariant().Replace(' ', '-');

    private static string MarcaIntel(string nome)
    {
        foreach (var marca in new[] { "Pentium Gold", "Pentium Silver", "Pentium", "Celeron", "Atom", "Xeon", "Core" })
        {
            if (Regex.IsMatch(nome, $@"\b{marca}\b", RegexOptions.IgnoreCase))
            {
                return marca;
            }
        }

        return string.Empty;
    }

    private static string LinhaAmd(string produto) =>
        produto.Contains("EPYC", StringComparison.OrdinalIgnoreCase) ? "epyc"
        : produto.Contains("Athlon", StringComparison.OrdinalIgnoreCase) ? "athlon"
        : produto.Contains("Threadripper", StringComparison.OrdinalIgnoreCase) ? (produto.Contains("PRO", StringComparison.Ordinal) ? "threadripper-pro" : "threadripper")
        : produto.Contains("PRO", StringComparison.Ordinal) ? "ryzen-pro" : "ryzen";

    private static string LerRecurso()
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream("processadores-windows11.csv")
            ?? throw new InvalidOperationException("Tabela do Windows 11 não encontrada no programa.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }

    [GeneratedRegex(@"\(R\)|\(TM\)|®|™|\bCPU\b|@.*$|\bProcessor\b", RegexOptions.IgnoreCase)]
    private static partial Regex Simbolos();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacos();

    [GeneratedRegex(@"^core-[a-z0-9]+-g(\d+)$")]
    private static partial Regex GeracaoCore();

    // Série Ryzen de 4 algarismos, da lista ("amd:ryzen:3/4") ou do processador ("amd:ryzen:2700#4").
    [GeneratedRegex(@"^amd:(?:ryzen|threadripper)(?:-pro)?:(\d)(?:/4|\d{3}#4)")]
    private static partial Regex SerieRyzen();

    [GeneratedRegex(@"(?<geracao>\d+)(?:st|nd|rd|th) Generation Core (?<nivel>i\d|m)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LinhaCoreI();

    [GeneratedRegex(@"Core (?<nivel>i\d) Processors \((?<geracao>\d+)(?:st|nd|rd|th) Generation\)", RegexOptions.IgnoreCase)]
    private static partial Regex LinhaCoreI14();

    [GeneratedRegex(@"^Core (?<ultra>Ultra )?Processors \(Series (?<serie>\d)\)$", RegexOptions.IgnoreCase)]
    private static partial Regex LinhaCoreSerie();

    [GeneratedRegex(@"^(?<marca>.*?)\s*(?<letras>[A-Z]{1,2}-?)?(?<numero>\d{2,5})(?<sufixo>[A-Z]?)\s+Series$", RegexOptions.IgnoreCase)]
    private static partial Regex LinhaNumerada();

    [GeneratedRegex(@"^(\d)00(\d) Series$")]
    private static partial Regex EpycLinha();

    [GeneratedRegex(@"^(?<ai>AI )?(?<z>Z\d)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AmdZ();

    [GeneratedRegex(@"^(?:(?<ai>AI) )?(?:(?<max>Max) )?(?<numero>\d{2,4})\s+(?:[A-Z]+-|U )?Series$", RegexOptions.IgnoreCase)]
    private static partial Regex AmdNumero();

    [GeneratedRegex(@"\bX1([EP])?(?:\b|\d|-)", RegexOptions.IgnoreCase)]
    private static partial Regex QualcommLinha();

    [GeneratedRegex(@"\bX1([EP])?(?=\d|-|\b)", RegexOptions.IgnoreCase)]
    private static partial Regex QualcommNome();

    [GeneratedRegex(@"\bCore (?<ultra>Ultra )?[3579] (?<serie>[1-9])\d\d[A-Z]*\b", RegexOptions.IgnoreCase)]
    private static partial Regex CoreUltraNome();

    [GeneratedRegex(@"\bCore (?<nivel>i[3579]|m[357]?)[- ](?<numero>\d{3,5})[A-Z0-9]*\b", RegexOptions.IgnoreCase)]
    private static partial Regex CoreINome();

    [GeneratedRegex(@"(?<![\w])(?<letras>[A-Z]{1,2}-?)?(?<numero>\d{2,5})(?<sufixo>[A-Z][A-Z0-9]*)?(?![\w.])", RegexOptions.IgnoreCase)]
    private static partial Regex ModeloNome();

    [GeneratedRegex(@"\bPRO\b")]
    private static partial Regex ProNome();

    [GeneratedRegex(@"\bEPYC (\d)\d\d(\d)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EpycNome();

    [GeneratedRegex(@"\bRyzen (?<ai>AI )?(?<z>Z\d)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AmdZNome();

    [GeneratedRegex(@"\bRyzen AI (?<max>Max\+? )?(?:[3579] )?(?:PRO )?(?:HX )?(?<numero>\d{3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex AmdAiNome();

    [GeneratedRegex(@"\b(?:Ryzen|Athlon|Threadripper)\b.*?\b(?<numero>\d{2,4})(?:[A-Z]{0,3}\b)", RegexOptions.IgnoreCase)]
    private static partial Regex AmdNumeroNome();
}
