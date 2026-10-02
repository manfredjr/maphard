using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Firmware;

namespace MapHard.Nucleo.Windows;

/// <summary>R34. A ativação sai do SLIsGenuineLocal e entra pelo coletor; o leitor do registro a deixa como não lida.</summary>
public sealed record DadosWindows(Campo<string> Nome, Campo<string> Versao, Campo<string> Compilacao, Campo<string> Arquitetura, Campo<string> Ativacao);

/// <summary>
/// Versão do Windows pelo registro, em HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion.
/// O ProductName continua dizendo "Windows 10" no Windows 11, então o nome sai da compilação:
/// 22000 ou maior é Windows 11.
/// </summary>
public static class LeitorWindows
{
    internal const string Chave = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const int PrimeiraCompilacaoWindows11 = 22000;

    private static readonly Dictionary<string, string> _edicoes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Core"] = "Home",
        ["CoreSingleLanguage"] = "Home Single Language",
        ["CoreN"] = "Home N",
        ["Professional"] = "Pro",
        ["ProfessionalN"] = "Pro N",
        ["ProfessionalWorkstation"] = "Pro for Workstations",
        ["ProfessionalEducation"] = "Pro Education",
        ["Enterprise"] = "Enterprise",
        ["EnterpriseN"] = "Enterprise N",
        ["EnterpriseS"] = "Enterprise LTSC",
        ["Education"] = "Education",
        ["IoTEnterprise"] = "IoT Enterprise",
        ["IoTEnterpriseS"] = "IoT Enterprise LTSC",
    };

    public static DadosWindows Ler(IFonteRegistro registro)
    {
        var compilacaoTexto = registro.Ler(Chave, "CurrentBuild") as string;
        if (!int.TryParse(compilacaoTexto, out var compilacao) || compilacao <= 0)
        {
            var erro = Campo<string>.NaoInformado(FonteDado.Registro);
            return new DadosWindows(erro, erro, erro, Arquitetura(), NaoLida);
        }

        var familia = compilacao >= PrimeiraCompilacaoWindows11 ? "Windows 11" : "Windows 10";
        var edicaoId = registro.Ler(Chave, "EditionID") as string;
        var edicao = edicaoId is null ? null : _edicoes.GetValueOrDefault(edicaoId, edicaoId);
        var nome = edicao is null ? familia : $"{familia} {edicao}";

        var versao = registro.Ler(Chave, "DisplayVersion") as string ?? registro.Ler(Chave, "ReleaseId") as string;
        var revisao = registro.Ler(Chave, "UBR") is int ubr ? $".{ubr}" : string.Empty;

        return new DadosWindows(
            Campo<string>.Lido(nome, FonteDado.Registro),
            string.IsNullOrWhiteSpace(versao) ? Campo<string>.NaoInformado(FonteDado.Registro) : Campo<string>.Lido(versao, FonteDado.Registro),
            Campo<string>.Lido($"{compilacao}{revisao}", FonteDado.Registro),
            Arquitetura(),
            NaoLida);
    }

    private static readonly Campo<string> NaoLida = Campo<string>.NaoInformado(FonteDado.Windows, "ativação não lida");

    private static Campo<string> Arquitetura() => LeitorAtivacao.Arquitetura(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture);
}
