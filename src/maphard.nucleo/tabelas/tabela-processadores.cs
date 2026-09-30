using System.Globalization;
using System.Reflection;

namespace MapHard.Nucleo.Tabelas;

/// <summary>Uma linha da tabela: fabricante, família e faixa de modelo, com as revisões quando a linha depende delas.</summary>
public sealed record LinhaProcessador(
    string Fabricante,
    int Familia,
    int ModeloInicial,
    int ModeloFinal,
    IReadOnlySet<int>? Revisoes,
    string Nome,
    string? Litografia,
    string Fonte)
{
    public bool Atende(string fabricante, int familia, int modelo, int revisao) =>
        Fabricante == fabricante
        && Familia == familia
        && modelo >= ModeloInicial && modelo <= ModeloFinal
        && (Revisoes is null || Revisoes.Contains(revisao));
}

/// <summary>
/// Tabela de codinomes e microarquiteturas, embutida no programa (<c>tabelas/processadores.csv</c>).
/// Toda linha cita a fonte. Processador fora da tabela fica "não consta na tabela do MapHard".
/// </summary>
public sealed class TabelaProcessadores
{
    private const int Colunas = 8;

    private readonly IReadOnlyList<LinhaProcessador> _linhas;

    public TabelaProcessadores(IReadOnlyList<LinhaProcessador> linhas)
    {
        _linhas = linhas;
    }

    public IReadOnlyList<LinhaProcessador> Linhas => _linhas;

    private static readonly Lazy<TabelaProcessadores> _embutida = new(() => Ler(LerRecurso()));

    public static TabelaProcessadores Embutida => _embutida.Value;

    /// <summary>A linha mais específica: a que lista revisões ganha da que vale para todas.</summary>
    public LinhaProcessador? Buscar(string fabricante, int familia, int modelo, int revisao) =>
        _linhas
            .Where(l => l.Atende(fabricante, familia, modelo, revisao))
            .OrderBy(l => l.Revisoes is null ? 1 : 0)
            .ThenBy(l => l.ModeloFinal - l.ModeloInicial)
            .FirstOrDefault();

    public static TabelaProcessadores Ler(string texto)
    {
        var linhas = new List<LinhaProcessador>();
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

            linhas.Add(Interpretar(linha, numero));
        }

        return new TabelaProcessadores(linhas);
    }

    private static LinhaProcessador Interpretar(string linha, int numero)
    {
        var campos = linha.Split(';');
        if (campos.Length != Colunas)
        {
            throw new FormatException($"Linha {numero} da tabela de processadores tem {campos.Length} campos, e o esperado é {Colunas}.");
        }

        if (string.IsNullOrWhiteSpace(campos[5]) || string.IsNullOrWhiteSpace(campos[7]))
        {
            throw new FormatException($"Linha {numero} da tabela de processadores está sem nome ou sem fonte.");
        }

        return new LinhaProcessador(
            campos[0].Trim(),
            Hex(campos[1], numero),
            Hex(campos[2], numero),
            Hex(campos[3], numero),
            LerRevisoes(campos[4], numero),
            campos[5].Trim(),
            string.IsNullOrWhiteSpace(campos[6]) ? null : campos[6].Trim(),
            campos[7].Trim());
    }

    /// <summary>Revisões em decimal: "9", "10-13" ou "11,12". Vazio vale para todas.</summary>
    private static IReadOnlySet<int>? LerRevisoes(string texto, int numero)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var revisoes = new HashSet<int>();
        foreach (var parte in texto.Split(','))
        {
            var faixa = parte.Split('-');
            var inicio = Decimal(faixa[0], numero);
            var fim = faixa.Length > 1 ? Decimal(faixa[1], numero) : inicio;
            for (var r = inicio; r <= fim; r++)
            {
                revisoes.Add(r);
            }
        }

        return revisoes;
    }

    private static int Hex(string texto, int numero) =>
        int.TryParse(texto.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw new FormatException($"Linha {numero} da tabela de processadores tem número hexadecimal inválido: {texto}.");

    private static int Decimal(string texto, int numero) =>
        int.TryParse(texto.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw new FormatException($"Linha {numero} da tabela de processadores tem revisão inválida: {texto}.");

    private static string LerRecurso()
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream("processadores.csv")
            ?? throw new InvalidOperationException("Tabela de processadores não encontrada no programa.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }
}
