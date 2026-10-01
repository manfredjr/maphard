using System.Runtime.Versioning;
using System.Security.Principal;
using MapHard.Nucleo.Cpuid;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Processador;
using MapHard.Nucleo.Smbios;

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
        EhAdministrador);

    [SupportedOSPlatform("windows")]
    private static bool EhAdministrador()
    {
        using var identidade = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identidade).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
