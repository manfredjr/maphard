using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace MapHard.Nucleo.Dispositivos;

/// <summary>Um dispositivo presente, como a SetupAPI o entrega. Propriedade que não veio fica nula.</summary>
public sealed record DispositivoBruto(string? Nome, string? Classe, IReadOnlyList<string> IdsHardware, uint? CodigoProblema, string? VersaoDriver, DateOnly? DataDriver = null);

/// <summary>Leitura dos dispositivos presentes. Os testes trocam por uma lista.</summary>
public interface IFonteDispositivos
{
    IReadOnlyList<DispositivoBruto> Ler();
}

/// <summary>
/// Leitura real, sem administrador: SetupDiGetClassDevsW com DIGCF_ALLCLASSES (0x4) e DIGCF_PRESENT (0x2)
/// (SetupAPI.h), e as propriedades pelo SetupDiGetDevicePropertyW com as chaves do devpkey.h:
/// DEVPKEY_Device_FriendlyName (14) e DeviceDesc (2), Class (9), HardwareIds (3), na categoria
/// a45c254e-df1c-4efd-8020-67d146a850e0; DEVPKEY_Device_ProblemCode (3) na 4340a6c5-93fa-4706-972c-7b648008a5a7;
/// DEVPKEY_Device_DriverDate (2) e DriverVersion (3) na a8b865dd-2e3d-4094-ad97-e593a70c75d6. Tipos pelo devpropdef.h:
/// UINT32 0x7, FILETIME 0x10, STRING 0x12, STRING_LIST 0x2012. Código de problema 0 é "sem problema" ("Retrieving the
/// Status and Problem Code for a Device Instance", learn.microsoft.com).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteDispositivosWindows : IFonteDispositivos
{
    private const uint TodasClasses = 0x4;
    private const uint Presentes = 0x2;
    private const uint TipoUint32 = 0x7;
    private const uint TipoTexto = 0x12;
    private const uint TipoListaTexto = 0x2012;
    private const uint TipoDataArquivo = 0x10;

    private static readonly Guid Dispositivo = new("a45c254e-df1c-4efd-8020-67d146a850e0");
    private static readonly Guid Estado = new("4340a6c5-93fa-4706-972c-7b648008a5a7");
    private static readonly Guid Driver = new("a8b865dd-2e3d-4094-ad97-e593a70c75d6");

    public IReadOnlyList<DispositivoBruto> Ler()
    {
        var lista = SetupDiGetClassDevsW(0, null, 0, TodasClasses | Presentes);
        if (lista == -1)
        {
            throw new InvalidOperationException($"a lista de dispositivos não abriu (erro {Marshal.GetLastPInvokeError()})");
        }

        var resultado = new List<DispositivoBruto>();
        try
        {
            for (uint i = 0; ; i++)
            {
                var dados = new DadosDispositivo { Tamanho = (uint)Marshal.SizeOf<DadosDispositivo>() };
                if (!SetupDiEnumDeviceInfo(lista, i, ref dados))
                {
                    break;
                }

                resultado.Add(new DispositivoBruto(
                    Texto(lista, ref dados, Dispositivo, 14) ?? Texto(lista, ref dados, Dispositivo, 2),
                    Texto(lista, ref dados, Dispositivo, 9),
                    Lista(lista, ref dados, Dispositivo, 3),
                    Numero(lista, ref dados, Estado, 3),
                    Texto(lista, ref dados, Driver, 3),
                    Propriedade(lista, ref dados, Driver, 2, TipoDataArquivo) is { Length: >= 8 } data
                        ? DateOnly.FromDateTime(DateTime.FromFileTimeUtc(BinaryPrimitives.ReadInt64LittleEndian(data)))
                        : null));
            }
        }
        finally
        {
            _ = SetupDiDestroyDeviceInfoList(lista);
        }

        return resultado;
    }

    private static byte[]? Propriedade(nint lista, ref DadosDispositivo dados, Guid categoria, uint id, uint tipoEsperado)
    {
        var chave = new ChavePropriedade { Categoria = categoria, Id = id };
        _ = SetupDiGetDevicePropertyW(lista, ref dados, ref chave, out _, null, 0, out var necessario, 0);
        if (necessario == 0)
        {
            return null;
        }

        var buffer = new byte[necessario];
        return SetupDiGetDevicePropertyW(lista, ref dados, ref chave, out var tipo, buffer, necessario, out _, 0) && tipo == tipoEsperado ? buffer : null;
    }

    private static string? Texto(nint lista, ref DadosDispositivo dados, Guid categoria, uint id) =>
        Propriedade(lista, ref dados, categoria, id, TipoTexto) is { } b && Encoding.Unicode.GetString(b).TrimEnd('\0').Trim() is { Length: > 0 } t ? t : null;

    private static IReadOnlyList<string> Lista(nint lista, ref DadosDispositivo dados, Guid categoria, uint id) =>
        Propriedade(lista, ref dados, categoria, id, TipoListaTexto) is { } b
            ? Encoding.Unicode.GetString(b).Split('\0', StringSplitOptions.RemoveEmptyEntries)
            : [];

    private static uint? Numero(nint lista, ref DadosDispositivo dados, Guid categoria, uint id) =>
        Propriedade(lista, ref dados, categoria, id, TipoUint32) is { Length: >= 4 } b ? BinaryPrimitives.ReadUInt32LittleEndian(b) : null;

    [StructLayout(LayoutKind.Sequential)]
    private struct DadosDispositivo
    {
        public uint Tamanho;
        public Guid Classe;
        public uint Instancia;
        public nint Reservado;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ChavePropriedade
    {
        public Guid Categoria;
        public uint Id;
    }

    [LibraryImport("setupapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint SetupDiGetClassDevsW(nint classe, string? enumerador, nint janela, uint sinais);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiEnumDeviceInfo(nint lista, uint indice, ref DadosDispositivo dados);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDevicePropertyW(nint lista, ref DadosDispositivo dados, ref ChavePropriedade chave, out uint tipo, [Out] byte[]? buffer, uint tamanho, out uint necessario, uint sinais);

    [LibraryImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiDestroyDeviceInfoList(nint lista);
}
