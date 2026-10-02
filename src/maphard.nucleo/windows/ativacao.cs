using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MapHard.Nucleo.Campos;

namespace MapHard.Nucleo.Windows;

/// <summary>Estado da licença do Windows. Os testes trocam por um número fixo.</summary>
public interface IFonteAtivacao
{
    /// <summary>O SL_GENUINE_STATE. Falha da chamada vira exceção.</summary>
    int Estado();
}

/// <summary>
/// Ativação e arquitetura do Windows (R34). O estado vem do SLIsGenuineLocal (slpublic.h, Slwga.dll), com os
/// valores do SL_GENUINE_STATE: 0 SL_GEN_STATE_IS_GENUINE, 1 SL_GEN_STATE_INVALID_LICENSE, 2 SL_GEN_STATE_TAMPERED,
/// 3 SL_GEN_STATE_OFFLINE. Nenhuma chave de produto é lida.
/// </summary>
public static class LeitorAtivacao
{
    public static Campo<string> Interpretar(int estado) => estado switch
    {
        0 => Campo<string>.Lido("ativado", FonteDado.Windows),
        1 => Campo<string>.Lido("licença inválida", FonteDado.Windows),
        2 => Campo<string>.Lido("licença adulterada", FonteDado.Windows),
        3 => Campo<string>.Lido("sem conexão para confirmar", FonteDado.Windows),
        _ => Campo<string>.NaoInformado(FonteDado.Windows, $"estado {estado} fora da documentação"),
    };

    /// <summary>"x64" ou "ARM64", pela arquitetura do Windows, não a do programa.</summary>
    public static Campo<string> Arquitetura(Architecture arquitetura) => arquitetura switch
    {
        Architecture.X64 => Campo<string>.Lido("x64", FonteDado.Windows),
        Architecture.Arm64 => Campo<string>.Lido("ARM64", FonteDado.Windows),
        Architecture.X86 => Campo<string>.Lido("x86", FonteDado.Windows),
        _ => Campo<string>.Lido(arquitetura.ToString(), FonteDado.Windows),
    };
}

/// <summary>
/// Leitura real, sem administrador. O identificador de aplicativo do Windows (55c92734-d682-4d71-983e-d6ec3f16059f)
/// não está na página do SLIsGenuineLocal: [CONFERIR] numa fonte oficial. Foi conferido em 02/10/2026 numa máquina
/// ativada, onde é o ApplicationID das licenças do Windows na classe SoftwareLicensingProduct e a chamada devolve
/// "ativado".
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteAtivacaoWindows : IFonteAtivacao
{
    private static readonly Guid AplicativoWindows = new("55c92734-d682-4d71-983e-d6ec3f16059f");

    public int Estado()
    {
        var aplicativo = AplicativoWindows;
        var resultado = SLIsGenuineLocal(ref aplicativo, out var estado, 0);
        return resultado == 0 ? estado : throw new InvalidOperationException($"a consulta da licença falhou (0x{resultado:X8})");
    }

    [LibraryImport("slwga.dll")]
    private static partial int SLIsGenuineLocal(ref Guid aplicativo, out int estado, nint opcoes);
}
