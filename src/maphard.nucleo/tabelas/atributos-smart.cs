using System.Globalization;
using System.Reflection;

namespace MapHard.Nucleo.Tabelas;

/// <summary>Um atributo da tabela: o nome em português, o original do smartmontools e se vale só para HDD ou SSD.</summary>
public sealed record LinhaAtributoSmart(byte Id, string NomeOriginal, string Nome, string? TipoDisco);

/// <summary>
/// Nomes dos atributos SMART ATA, embutidos no programa (<c>tabelas/atributos-smart.csv</c>, gerada por
/// <c>ferramentas/gerar-atributos-smart.ps1</c> a partir da entrada DEFAULT do drivedb.h do smartmontools).
/// O significado de muitos atributos muda de fabricante para fabricante; a tabela traz o nome padrão.
/// </summary>
public sealed class AtributosSmart
{
    private const int Colunas = 5;

    private readonly Dictionary<byte, LinhaAtributoSmart> _linhas;

    public AtributosSmart(IEnumerable<LinhaAtributoSmart> linhas)
    {
        _linhas = linhas.ToDictionary(l => l.Id);
    }

    public int Quantidade => _linhas.Count;

    private static readonly Lazy<AtributosSmart> _embutida = new(() => Ler(LerRecurso()));

    public static AtributosSmart Embutida => _embutida.Value;

    public LinhaAtributoSmart? Buscar(byte id) => _linhas.GetValueOrDefault(id);

    /// <summary>O nome para a tela. Fora da tabela: "atributo do fabricante (0xNN)".</summary>
    public string Nome(byte id) => Buscar(id)?.Nome ?? $"atributo do fabricante (0x{id:X2})";

    public static AtributosSmart Ler(string texto)
    {
        var linhas = new List<LinhaAtributoSmart>();
        var ids = new HashSet<byte>();
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
            if (campos.Length != Colunas || string.IsNullOrWhiteSpace(campos[1]) || string.IsNullOrWhiteSpace(campos[2]) || string.IsNullOrWhiteSpace(campos[4]))
            {
                throw new FormatException($"Linha {numero} da tabela de atributos SMART está incompleta ou sem fonte.");
            }

            if (!byte.TryParse(campos[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || !ids.Add(id))
            {
                throw new FormatException($"Linha {numero} da tabela de atributos SMART tem id inválido ou repetido.");
            }

            linhas.Add(new LinhaAtributoSmart(id, campos[1].Trim(), campos[2].Trim(), string.IsNullOrWhiteSpace(campos[3]) ? null : campos[3].Trim()));
        }

        return new AtributosSmart(linhas);
    }

    private static string LerRecurso()
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream("atributos-smart.csv")
            ?? throw new InvalidOperationException("Tabela de atributos SMART não encontrada no programa.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }
}
