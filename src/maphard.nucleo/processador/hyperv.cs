using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Firmware;

namespace MapHard.Nucleo.Processador;

/// <summary>
/// Se alguém pediu a virtualização do Windows: o serviço do Hyper-V (vmms) ou o do WSL (WslService) existe em
/// HKLM\SYSTEM\CurrentControlSet\Services, que o usuário comum lê. Decisão do Manfred em 02/10/2026 (plano da
/// fatia 6). Os dois nomes foram vistos em 02/10/2026 numa máquina com Hyper-V e WSL ligados; a ausência deles numa
/// máquina sem os recursos fica a conferir.
/// </summary>
public static class LeitorHyperV
{
    internal const string ChaveHyperV = @"SYSTEM\CurrentControlSet\Services\vmms";
    internal const string ChaveWsl = @"SYSTEM\CurrentControlSet\Services\WslService";

    public static Campo<bool> Pedido(IFonteRegistro registro) =>
        registro.Ler(ChaveHyperV, "Start") is not null ? Campo<bool>.Lido(true, FonteDado.Registro, "serviço do Hyper-V instalado")
        : registro.Ler(ChaveWsl, "Start") is not null ? Campo<bool>.Lido(true, FonteDado.Registro, "serviço do WSL instalado")
        : Campo<bool>.Lido(false, FonteDado.Registro);
}
