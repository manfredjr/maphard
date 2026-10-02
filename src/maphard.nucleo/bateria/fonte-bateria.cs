using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using MapHard.Nucleo.Discos;
using Microsoft.Win32.SafeHandles;

namespace MapHard.Nucleo.Baterias;

/// <summary>Leitura das baterias. Os testes trocam por estruturas montadas à mão.</summary>
public interface IFonteBaterias
{
    IReadOnlyList<BateriaBruta> Ler();
}

/// <summary>
/// Leitura real, sem administrador, como no exemplo "Enumerating Battery Devices" do learn.microsoft.com, mas com
/// acesso só de leitura, que é o que os dois controles pedem (FILE_READ_ACCESS). Pelo poclass.h do SDK:
/// GUID_DEVICE_BATTERY 72631e54-78a4-11d0-bcf7-00aa00b7b32a; IOCTL_BATTERY_QUERY_TAG = CTL_CODE(FILE_DEVICE_BATTERY
/// 0x29, 0x10, METHOD_BUFFERED, FILE_READ_ACCESS) = 0x294040; IOCTL_BATTERY_QUERY_INFORMATION com a função 0x11 =
/// 0x294044. Níveis do BATTERY_QUERY_INFORMATION: 0 BatteryInformation, 4 BatteryDeviceName, 6 BatteryManufactureName.
/// O número de série (nível 8) não é lido.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteBateriasWindows : IFonteBaterias
{
    private const uint ConsultarEtiqueta = 0x294040;
    private const uint ConsultarInformacao = 0x294044;
    private const uint NivelInformacao = 0;
    private const uint NivelNome = 4;
    private const uint NivelFabricante = 6;
    private const uint LeituraGenerica = 0x80000000;
    private const uint CompartilharLeituraEscrita = 3;
    private const uint AbrirExistente = 3;
    private const int TamanhoTexto = 512;

    private static readonly Guid InterfaceBateria = new("72631e54-78a4-11d0-bcf7-00aa00b7b32a");

    public IReadOnlyList<BateriaBruta> Ler()
    {
        var baterias = new List<BateriaBruta>();
        foreach (var (caminho, _) in FonteDiscosWindows.InterfacesPresentes(InterfaceBateria))
        {
            using var bateria = FonteDiscosWindows.CreateFileW(caminho, LeituraGenerica, CompartilharLeituraEscrita, 0, AbrirExistente, 0, 0);
            if (bateria.IsInvalid)
            {
                continue;
            }

            var etiqueta = new byte[4];
            if (!DeviceIoControl(bateria, ConsultarEtiqueta, new byte[4], 4, etiqueta, 4, out _, 0))
            {
                continue;
            }

            var tag = BinaryPrimitives.ReadUInt32LittleEndian(etiqueta);
            if (tag == 0 || Consultar(bateria, tag, NivelInformacao, LeitorBateria.TamanhoInformacao) is not { } informacao)
            {
                continue;
            }

            baterias.Add(new BateriaBruta(informacao, Texto(bateria, tag, NivelNome), Texto(bateria, tag, NivelFabricante)));
        }

        return baterias;
    }

    private static string? Texto(SafeFileHandle bateria, uint etiqueta, uint nivel) =>
        Consultar(bateria, etiqueta, nivel, TamanhoTexto) is { } bytes ? Encoding.Unicode.GetString(bytes).Split('\0')[0] : null;

    private static byte[]? Consultar(SafeFileHandle bateria, uint etiqueta, uint nivel, int tamanho)
    {
        var pedido = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(pedido, etiqueta);
        BinaryPrimitives.WriteUInt32LittleEndian(pedido.AsSpan(4), nivel);
        var saida = new byte[tamanho];
        return DeviceIoControl(bateria, ConsultarInformacao, pedido, pedido.Length, saida, saida.Length, out var lidos, 0)
            ? saida.AsSpan(0, lidos).ToArray()
            : null;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(SafeFileHandle dispositivo, uint codigo, byte[] entrada, int tamanhoEntrada, byte[] saida, int tamanhoSaida, out int lidos, nint sobreposto);
}
