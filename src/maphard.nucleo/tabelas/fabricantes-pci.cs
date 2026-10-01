using System.Globalization;
using System.Reflection;

namespace MapHard.Nucleo.Tabelas;

/// <summary>
/// Fabricantes PCI pelo código, embutidos no programa (<c>tabelas/fabricantes-pci.csv</c>, gerada por
/// <c>ferramentas/gerar-fabricantes-pci.ps1</c> a partir do pci.ids do pciutils).
/// </summary>
public sealed class FabricantesPci
{
    private readonly Dictionary<uint, string> _nomes;

    public FabricantesPci(IReadOnlyDictionary<uint, string> nomes)
    {
        _nomes = new Dictionary<uint, string>(nomes);
    }

    public int Quantidade => _nomes.Count;

    private static readonly Lazy<FabricantesPci> _embutida = new(() => Ler(LerRecurso()));

    public static FabricantesPci Embutida => _embutida.Value;

    public string? Nome(uint codigo) => _nomes.GetValueOrDefault(codigo);

    public static FabricantesPci Ler(string texto)
    {
        var nomes = new Dictionary<uint, string>();
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
            if (campos.Length != 3 || campos.Any(string.IsNullOrWhiteSpace)
                || !uint.TryParse(campos[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codigo)
                || !nomes.TryAdd(codigo, campos[1].Trim()))
            {
                throw new FormatException($"Linha {numero} da tabela de fabricantes PCI está incompleta, sem fonte ou repetida.");
            }
        }

        return new FabricantesPci(nomes);
    }

    private static string LerRecurso()
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream("fabricantes-pci.csv")
            ?? throw new InvalidOperationException("Tabela de fabricantes PCI não encontrada no programa.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }
}
