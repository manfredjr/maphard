using System.Runtime.Versioning;
using System.Security.Principal;
using MapHard.Nucleo.Baterias;
using MapHard.Nucleo.Cpuid;
using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Dispositivos;
using MapHard.Nucleo.Eventos;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Processador;
using MapHard.Nucleo.Rede;
using MapHard.Nucleo.Smbios;
using MapHard.Nucleo.Video;
using MapHard.Nucleo.Windows;

namespace MapHard.Nucleo.Coleta;

/// <summary>Todas as fontes de uma coleta. Os testes montam com fontes simuladas.</summary>
public sealed record FontesColeta(
    IFonteSmbios Smbios,
    IFonteCpuid Cpuid,
    IFonteTopologia Topologia,
    IFonteClocks Clocks,
    IFonteFirmware Firmware,
    IFonteRegistro Registro,
    Func<string> NomeComputador,
    IFonteMemoria Memoria,
    IFonteDiscos Discos,
    IFonteEventos Eventos,
    IFonteSistema Sistema,
    IFonteDispositivos Dispositivos,
    IFonteVideo Video,
    IFonteMonitores Monitores,
    IFonteBaterias Baterias,
    IFonteRede Rede,
    IFonteAtivacao Ativacao,
    Func<string> LetraWindows,
    Func<bool> Administrador)
{
    [SupportedOSPlatform("windows")]
    public static FontesColeta Windows() => new(
        new FonteSmbiosWindows(),
        new FonteCpuidReal(),
        new FonteTopologiaWindows(),
        new FonteClocksWindows(),
        new FonteFirmwareWindows(),
        new FonteRegistroWindows(),
        () => Environment.MachineName,
        new FonteMemoriaWindows(),
        new FonteDiscosWindows(),
        new FonteEventosWindows(),
        new FonteSistemaWindows(),
        new FonteDispositivosWindows(),
        new FonteVideoWindows(),
        new FonteMonitoresWindows(),
        new FonteBateriasWindows(),
        new FonteRedeWindows(),
        new FonteAtivacaoWindows(),
        () => Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:",
        EhAdministrador);

    [SupportedOSPlatform("windows")]
    private static bool EhAdministrador()
    {
        using var identidade = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identidade).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
