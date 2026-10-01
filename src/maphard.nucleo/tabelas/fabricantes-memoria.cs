using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using MapHard.Nucleo.Campos;

namespace MapHard.Nucleo.Tabelas;

/// <summary>
/// Fabricantes de memória pelo código JEDEC (JEP106), embutidos no programa (<c>tabelas/fabricantes-memoria.csv</c>,
/// gerada por <c>ferramentas/gerar-fabricantes-memoria.ps1</c> a partir do decode-dimms do i2c-tools).
/// O código tem dois bytes: o de banco (quantos códigos de continuação 0x7F vêm antes, com paridade
/// ímpar no bit 7) e o do fabricante no banco (também com paridade ímpar). Regra das funções
/// manufacturer_common e manufacturer_ddr3 do decode-dimms.
/// </summary>
public sealed partial class FabricantesMemoria
{
    private const int Colunas = 4;

    private readonly Dictionary<(int Banco, byte Codigo), string> _nomes;

    public FabricantesMemoria(IReadOnlyDictionary<(int Banco, byte Codigo), string> nomes)
    {
        _nomes = new Dictionary<(int, byte), string>(nomes);
    }

    public int Quantidade => _nomes.Count;

    public IEnumerable<int> Bancos => _nomes.Keys.Select(k => k.Banco).Distinct();

    private static readonly Lazy<FabricantesMemoria> _embutida = new(() => Ler(LerRecurso()));

    public static FabricantesMemoria Embutida => _embutida.Value;

    /// <summary>
    /// O fabricante do módulo. Vale o código do deslocamento 0x2C do tipo 17 quando existe; senão, o
    /// texto do firmware, que às vezes é o nome ("Samsung") e às vezes o código em hexadecimal
    /// ("80CE", "CE00", "0x80CE", "80AD000080AD").
    /// </summary>
    public Campo<string> Traduzir(string? texto, ushort? codigo)
    {
        if (codigo is { } c and not 0)
        {
            // O byte baixo é o de banco e o alto, o do fabricante, como no dmidecode.
            return Traduzir((byte)(c & 0xFF), (byte)(c >> 8));
        }

        var campo = Campo.Texto(texto, FonteDado.Smbios);
        if (!campo.FoiLido || _semNome.Contains(campo.Valor!))
        {
            return Campo<string>.NaoInformado(FonteDado.Smbios);
        }

        var hex = campo.Valor!.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? campo.Valor[2..] : campo.Valor;
        if (!CodigoEmTexto().IsMatch(hex))
        {
            return campo;
        }

        var b0 = byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b1 = byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        // "80CE": banco e depois fabricante, os dois com paridade. "CE00": fabricante e depois o banco sem paridade.
        return ParidadeImpar(b0) && ParidadeImpar(b1) && (b0 & 0x7F) < 16
            ? Traduzir(b0, b1)
            : ParidadeImpar(b0) && b1 < 16
                ? Traduzir((byte)(b1 == 0 ? 0x80 : b1), b0)
                : Campo<string>.NaoInformado(FonteDado.Smbios, $"código JEDEC inválido: {campo.Valor}");
    }

    private Campo<string> Traduzir(byte banco, byte fabricante)
    {
        var codigo = $"0x{banco:X2}{fabricante:X2}";
        if (!ParidadeImpar(fabricante) || (fabricante & 0x7F) == 0)
        {
            return Campo<string>.NaoInformado(FonteDado.Smbios, $"código JEDEC inválido: {codigo}");
        }

        var numeroBanco = (banco & 0x7F) + 1;
        return _nomes.TryGetValue((numeroBanco, fabricante), out var nome)
            ? Campo<string>.Lido(NomeCurto(nome), FonteDado.Tabela, $"código JEDEC {codigo}, banco {numeroBanco}")
            : Campo<string>.Lido($"código JEDEC {codigo}", FonteDado.Smbios, "fora da tabela do MapHard");
    }

    internal static bool ParidadeImpar(byte valor) => System.Numerics.BitOperations.PopCount(valor) % 2 == 1;

    /// <summary>Tira o " (former ...)" do fim, como o decode-dimms faz na saída lado a lado.</summary>
    internal static string NomeCurto(string nome) => AntigoNome().Replace(nome, string.Empty);

    // Textos que o firmware grava quando não sabe o fabricante. Os de fábrica gerais ficam em TextosDeFabrica.
    private static readonly HashSet<string> _semNome = new(StringComparer.OrdinalIgnoreCase) { "Unknown", "Undefined", "Manufacturer", "NO DIMM" };

    public static FabricantesMemoria Ler(string texto)
    {
        var nomes = new Dictionary<(int, byte), string>();
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
            if (campos.Length != Colunas || string.IsNullOrWhiteSpace(campos[2]) || string.IsNullOrWhiteSpace(campos[3]))
            {
                throw new FormatException($"Linha {numero} da tabela de fabricantes de memória está incompleta ou sem fonte.");
            }

            if (!int.TryParse(campos[0], NumberStyles.None, CultureInfo.InvariantCulture, out var banco) || banco < 1
                || !byte.TryParse(campos[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codigo))
            {
                throw new FormatException($"Linha {numero} da tabela de fabricantes de memória tem banco ou código inválido.");
            }

            if (!nomes.TryAdd((banco, codigo), campos[2].Trim()))
            {
                throw new FormatException($"Linha {numero} da tabela de fabricantes de memória repete o banco {banco} e o código {codigo:X2}.");
            }
        }

        return new FabricantesMemoria(nomes);
    }

    private static string LerRecurso()
    {
        using var fluxo = Assembly.GetExecutingAssembly().GetManifestResourceStream("fabricantes-memoria.csv")
            ?? throw new InvalidOperationException("Tabela de fabricantes de memória não encontrada no programa.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }

    [GeneratedRegex("^([0-9A-Fa-f]{2}){2,}$")]
    private static partial Regex CodigoEmTexto();

    [GeneratedRegex(@" \(former .*\)$")]
    private static partial Regex AntigoNome();
}
