using System.Globalization;
using System.Reflection;

namespace MapHard.Nucleo.Tabelas;

/// <summary>
/// Códigos de problema do Gerenciador de Dispositivos, embutidos no programa (<c>tabelas/problemas-dispositivo.csv</c>),
/// com o texto em português e a página oficial de cada código.
/// </summary>
public sealed class ProblemasDispositivo
{
    private const int Colunas = 4;

    private readonly Dictionary<int, (string Constante, string Texto)> _codigos;

    public ProblemasDispositivo(IReadOnlyDictionary<int, (string Constante, string Texto)> codigos)
    {
        _codigos = new Dictionary<int, (string, string)>(codigos);
    }

    public int Quantidade => _codigos.Count;

    private static readonly Lazy<ProblemasDispositivo> _embutida = new(() => Ler(LerRecurso()));

    public static ProblemasDispositivo Embutida => _embutida.Value;

    /// <summary>O texto em português. Código fora da tabela: "problema código N".</summary>
    public string Texto(int codigo) => _codigos.TryGetValue(codigo, out var c) ? c.Texto : $"problema código {codigo}";

    public string? Constante(int codigo) => _codigos.TryGetValue(codigo, out var c) ? c.Constante : null;

    public static ProblemasDispositivo Ler(string texto)
    {
        var codigos = new Dictionary<int, (string, string)>();
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
            if (campos.Length != Colunas || campos.Skip(1).Any(string.IsNullOrWhiteSpace))
            {
                throw new FormatException($"Linha {numero} da tabela de problemas de dispositivo está incompleta ou sem fonte.");
            }

            if (!int.TryParse(campos[0], NumberStyles.None, CultureInfo.InvariantCulture, out var codigo) || !codigos.TryAdd(codigo, (campos[1].Trim(), campos[2].Trim())))
            {
                throw new FormatException($"Linha {numero} da tabela de problemas de dispositivo tem código inválido ou repetido.");
            }
        }

        return new ProblemasDispositivo(codigos);
    }

    private static string LerRecurso()
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream("problemas-dispositivo.csv")
            ?? throw new InvalidOperationException("Tabela de problemas de dispositivo não encontrada no programa.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }
}
