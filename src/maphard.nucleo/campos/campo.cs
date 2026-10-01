namespace MapHard.Nucleo.Campos;

/// <summary>
/// Os estados de um campo, da seção 6 do desenho. Nenhum campo fica sem estado: é o que
/// garante a regra "nunca mostrar 0 ou vazio onde não houve leitura".
/// </summary>
public enum EstadoCampo
{
    Lido,
    NaoInformado,
    RequerAdministrador,
    NaoSuportado,
    ErroLeitura,
}

/// <summary>De onde veio o dado. A dica do campo na tela mostra a fonte.</summary>
public enum FonteDado
{
    Cpuid,
    Smbios,
    Topologia,
    Contador,
    Registro,
    Firmware,
    Tpm,
    Windows,
    Tabela,
    Demonstracao,
    Armazenamento,
    Smart,
}

/// <summary>Um dado lido, com o estado e a fonte. Fora do estado <see cref="EstadoCampo.Lido"/>, o valor é sempre nulo.</summary>
public sealed record Campo<T>
{
    private Campo(T? valor, EstadoCampo estado, FonteDado fonte, string? motivo)
    {
        Valor = valor;
        Estado = estado;
        Fonte = fonte;
        Motivo = motivo;
    }

    public T? Valor { get; }

    public EstadoCampo Estado { get; }

    public FonteDado Fonte { get; }

    /// <summary>Por que o campo não foi lido, ou uma observação sobre o valor lido.</summary>
    public string? Motivo { get; }

    public bool FoiLido => Estado == EstadoCampo.Lido;

    public static Campo<T> Lido(T valor, FonteDado fonte, string? observacao = null)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return new Campo<T>(valor, EstadoCampo.Lido, fonte, observacao);
    }

    public static Campo<T> NaoInformado(FonteDado fonte, string? motivo = null) => new(default, EstadoCampo.NaoInformado, fonte, motivo);

    public static Campo<T> RequerAdministrador(FonteDado fonte) => new(default, EstadoCampo.RequerAdministrador, fonte, null);

    public static Campo<T> NaoSuportado(FonteDado fonte, string? motivo = null) => new(default, EstadoCampo.NaoSuportado, fonte, motivo);

    public static Campo<T> Erro(FonteDado fonte, string motivo) => new(default, EstadoCampo.ErroLeitura, fonte, motivo);

    /// <summary>Transforma o valor lido e mantém o estado dos outros casos.</summary>
    public Campo<TNovo> Mapear<TNovo>(Func<T, TNovo> transformar) =>
        FoiLido ? Campo<TNovo>.Lido(transformar(Valor!), Fonte, Motivo) : Campo<TNovo>.Copiar(this);

    internal static Campo<T> Copiar<TOrigem>(Campo<TOrigem> origem) => new(default, origem.Estado, origem.Fonte, origem.Motivo);
}

/// <summary>Atalhos para montar campos.</summary>
public static class Campo
{
    /// <summary>
    /// Texto que veio de uma fonte do firmware. Texto de fábrica (placeholder do fabricante) e texto
    /// vazio viram <see cref="EstadoCampo.NaoInformado"/>, nunca um valor.
    /// </summary>
    public static Campo<string> Texto(string? texto, FonteDado fonte)
    {
        if (texto is null)
        {
            return Campo<string>.NaoInformado(fonte);
        }

        var limpo = texto.Trim();
        return TextosDeFabrica.EhTextoDeFabrica(limpo) ? Campo<string>.NaoInformado(fonte) : Campo<string>.Lido(limpo, fonte);
    }

    /// <summary>Valor opcional: nulo vira <see cref="EstadoCampo.NaoInformado"/>.</summary>
    public static Campo<T> DeOpcional<T>(T? valor, FonteDado fonte)
        where T : struct => valor.HasValue ? Campo<T>.Lido(valor.Value, fonte) : Campo<T>.NaoInformado(fonte);
}

/// <summary>Textos que o fabricante deixa no firmware no lugar do dado de verdade.</summary>
public static class TextosDeFabrica
{
    private static readonly HashSet<string> _textos = new(StringComparer.OrdinalIgnoreCase)
    {
        "To Be Filled By O.E.M.",
        "Default string",
        "System Product Name",
        "System manufacturer",
        "System Manufacturer",
        "System Version",
        "System Serial Number",
        "Base Board Serial Number",
        "Chassis Serial Number",
        "Not Applicable",
        "Not Specified",
        "Not Available",
        "None",
        "N/A",
        "0123456789",
        "INVALID",
        "O.E.M.",
        "OEM",
        "Type2 - Board Serial Number",
        "Type1ProductConfigId",
    };

    public static bool EhTextoDeFabrica(string texto)
    {
        var limpo = texto.Trim();
        return limpo.Length == 0 || _textos.Contains(limpo) || limpo.All(c => c == '0') || limpo.All(c => c == 'F' || c == 'f');
    }
}

/// <summary>Os textos de cada estado, iguais aos da seção 6 do desenho.</summary>
public static class TextosEstado
{
    public const string NaoInformado = "não informado pelo fabricante";
    public const string RequerAdministrador = "requer administrador";
    public const string NaoSuportado = "não disponível neste equipamento";
    public const string ErroLeitura = "erro de leitura";

    public static string Texto(EstadoCampo estado) => estado switch
    {
        EstadoCampo.NaoInformado => NaoInformado,
        EstadoCampo.RequerAdministrador => RequerAdministrador,
        EstadoCampo.NaoSuportado => NaoSuportado,
        EstadoCampo.ErroLeitura => ErroLeitura,
        _ => string.Empty,
    };

    public static string NomeFonte(FonteDado fonte) => fonte switch
    {
        FonteDado.Cpuid => "CPUID",
        FonteDado.Smbios => "SMBIOS",
        FonteDado.Topologia => "topologia do Windows",
        FonteDado.Contador => "contador de desempenho do Windows",
        FonteDado.Registro => "registro do Windows",
        FonteDado.Firmware => "firmware",
        FonteDado.Tpm => "TPM",
        FonteDado.Windows => "Windows",
        FonteDado.Tabela => "tabela do MapHard",
        FonteDado.Demonstracao => "demonstração",
        FonteDado.Armazenamento => "consulta de armazenamento do Windows",
        FonteDado.Smart => "SMART do disco",
        _ => fonte.ToString(),
    };
}
