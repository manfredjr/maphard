using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace MapHard.Nucleo.Video;

/// <summary>Leitura do EDID dos monitores presentes. Os testes trocam por blocos montados à mão.</summary>
public interface IFonteMonitores
{
    IReadOnlyList<byte[]> Edids();
}

/// <summary>
/// Leitura real, sem administrador: monitores presentes pela SetupAPI, na classe Monitor
/// (4d36e96e-e325-11ce-bfc1-08002be10318, "System-Defined Device Setup Classes"), e o EDID na chave de hardware de
/// cada um, aberta pelo SetupDiOpenDevRegKey com DICS_FLAG_GLOBAL (1) e DIREG_DEV (1) do SetupAPI.h. A página
/// "Overriding monitor EDIDs" (learn.microsoft.com) diz que o EDID fica na chave de hardware do monitor; o nome do
/// valor, "EDID", só aparece em fórum: [CONFERIR]. Monitores que já foram desligados ficam de fora, porque só os
/// presentes entram na lista.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteMonitoresWindows : IFonteMonitores
{
    private const uint Presentes = 0x2;
    private const uint Global = 1;
    private const uint ChaveDispositivo = 1;
    private const uint Leitura = 0x20019;

    private static readonly Guid ClasseMonitor = new("4d36e96e-e325-11ce-bfc1-08002be10318");

    public IReadOnlyList<byte[]> Edids()
    {
        var classe = ClasseMonitor;
        var lista = SetupDiGetClassDevsW(ref classe, null, 0, Presentes);
        if (lista == -1)
        {
            throw new InvalidOperationException($"a lista de monitores não abriu (erro {Marshal.GetLastPInvokeError()})");
        }

        var edids = new List<byte[]>();
        try
        {
            for (uint i = 0; ; i++)
            {
                var dados = new DadosDispositivo { Tamanho = (uint)Marshal.SizeOf<DadosDispositivo>() };
                if (!SetupDiEnumDeviceInfo(lista, i, ref dados))
                {
                    break;
                }

                var chave = SetupDiOpenDevRegKey(lista, ref dados, Global, 0, ChaveDispositivo, Leitura);
                if (chave == -1 || chave == 0)
                {
                    continue;
                }

                using var registro = RegistryKey.FromHandle(new SafeRegistryHandle(chave, ownsHandle: true));
                if (registro.GetValue("EDID") is byte[] edid)
                {
                    edids.Add(edid);
                }
            }
        }
        finally
        {
            _ = SetupDiDestroyDeviceInfoList(lista);
        }

        return edids;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DadosDispositivo
    {
        public uint Tamanho;
        public Guid Classe;
        public uint Instancia;
        public nint Reservado;
    }

    [LibraryImport("setupapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint SetupDiGetClassDevsW(ref Guid classe, string? enumerador, nint janela, uint sinais);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiEnumDeviceInfo(nint lista, uint indice, ref DadosDispositivo dados);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    private static partial nint SetupDiOpenDevRegKey(nint lista, ref DadosDispositivo dados, uint escopo, uint perfil, uint tipo, uint acesso);

    [LibraryImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiDestroyDeviceInfoList(nint lista);
}
