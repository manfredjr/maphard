using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using MapHard.Nucleo.Campos;

namespace MapHard.Nucleo.Dispositivos;

/// <summary>
/// Dispositivos PCI que identificam o chipset, embutidos no programa (<c>tabelas/chipsets.csv</c>, gerada por
/// <c>ferramentas/gerar-chipsets.ps1</c> a partir do pci.ids do pciutils).
/// </summary>
public sealed partial class TabelaChipsets
{
    private readonly Dictionary<(string Fabricante, string Dispositivo), string> _nomes;

    public TabelaChipsets(IReadOnlyDictionary<(string, string), string> nomes)
    {
        _nomes = new Dictionary<(string, string), string>(nomes);
    }

    public int Quantidade => _nomes.Count;

    private static readonly Lazy<TabelaChipsets> _embutida = new(() => Ler(LerRecurso()));

    public static TabelaChipsets Embutida => _embutida.Value;

    public string? Buscar(string fabricante, string dispositivo) => _nomes.GetValueOrDefault((fabricante.ToUpperInvariant(), dispositivo.ToUpperInvariant()));

    /// <summary>
    /// O chipset pelo controlador LPC ou eSPI (classe PCI 06, subclasse 01, "CC_0601" nos IDs de hardware);
    /// sem ele, pela ponte do processador (subclasse 00, "CC_0600"). Classes pelo pci.ids. O nome vem da tabela;
    /// fora dela, o nome que o Windows dá ao dispositivo.
    /// </summary>
    public Campo<string> Identificar(IReadOnlyList<DispositivoBruto> dispositivos)
    {
        foreach (var classe in new[] { "CC_0601", "CC_0600" })
        {
            // A classe vem como "CC_0601" ou com a interface de programação no fim, "CC_060100".
            var d = dispositivos.FirstOrDefault(x => x.IdsHardware.Any(id => Regex.IsMatch(id, $@"&{classe}([0-9A-F]{{2}})?$", RegexOptions.IgnoreCase)));
            if (d is null || d.IdsHardware.Select(id => IdPci().Match(id)).FirstOrDefault(m => m.Success) is not { } m)
            {
                continue;
            }

            var fabricante = m.Groups[1].Value.ToUpperInvariant();
            var dispositivo = m.Groups[2].Value.ToUpperInvariant();
            var codigo = $"{fabricante}:{dispositivo}";
            return Buscar(fabricante, dispositivo) is { } nome
                ? Campo<string>.Lido(nome, FonteDado.Tabela, $"dispositivo PCI {codigo}")
                : d.Nome is { } nomeWindows
                    ? Campo<string>.Lido(nomeWindows, FonteDado.Windows, $"dispositivo PCI {codigo}, fora da tabela do MapHard")
                    : Campo<string>.NaoInformado(FonteDado.Windows, $"dispositivo PCI {codigo}, fora da tabela do MapHard");
        }

        return Campo<string>.NaoInformado(FonteDado.Windows, "nenhuma ponte PCI do chipset encontrada");
    }

    public static TabelaChipsets Ler(string texto)
    {
        var nomes = new Dictionary<(string, string), string>();
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
            if (campos.Length != 4 || campos.Any(string.IsNullOrWhiteSpace)
                || !int.TryParse(campos[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
                || !int.TryParse(campos[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            {
                throw new FormatException($"Linha {numero} da tabela de chipsets está incompleta ou sem fonte.");
            }

            if (!nomes.TryAdd((campos[0].ToUpperInvariant(), campos[1].ToUpperInvariant()), campos[2].Trim()))
            {
                throw new FormatException($"Linha {numero} da tabela de chipsets repete o dispositivo.");
            }
        }

        return new TabelaChipsets(nomes);
    }

    private static string LerRecurso()
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream("chipsets.csv")
            ?? throw new InvalidOperationException("Tabela de chipsets não encontrada no programa.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }

    [GeneratedRegex(@"^PCI\\VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex IdPci();
}
