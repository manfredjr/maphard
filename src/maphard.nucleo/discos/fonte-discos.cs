using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace MapHard.Nucleo.Discos;

/// <summary>Leituras de disco que vêm do Windows. Os testes trocam por valores fixos.</summary>
public interface IFonteDiscos
{
    IReadOnlyList<DiscoBruto> Discos();

    /// <summary>Log de saúde NVMe (512 bytes) do disco <paramref name="numero"/>, ou null.</summary>
    byte[]? LogSaudeNvme(int numero);

    /// <summary>IDENTIFY do controlador NVMe (4096 bytes), ou null.</summary>
    byte[]? IdentificacaoNvme(int numero);
}

/// <summary>
/// Leitura real, sem administrador: cada disco é aberto com acesso 0, que basta para as consultas de
/// propriedade e para os IOCTLs com FILE_ANY_ACCESS (conferido numa máquina real em 01/10/2026).
/// Fontes: códigos pela macro CTL_CODE do devioctl.h e pelos ntddstor.h e ntdddisk.h do SDK (cópia do
/// microsoft/win32metadata); DIGCF_* do SetupAPI.h; CR_SUCCESS do cfgmgr32.h; DEVPROP_TYPE_UINT32 do
/// devpropdef.h; DEVPKEY_PciDevice_CurrentLinkSpeed (9) e CurrentLinkWidth (10) do pciprop.h.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteDiscosWindows : IFonteDiscos
{
    internal const uint ConsultaPropriedade = 0x002D1400;
    internal const uint NumeroDispositivo = 0x002D1080;
    internal const uint Geometria = 0x000700A0;
    internal const uint Layout = 0x00070050;

    private const int PropriedadeDispositivo = 0;
    private const int PropriedadePenalidadeBusca = 7;
    private const int PropriedadeTrim = 8;
    private const int PropriedadeProtocoloAdaptador = 49;
    private const int PropriedadeProtocoloDispositivo = 50;
    private const int ProtocoloNvmeTipo = 3;
    private const int DadoIdentify = 1;
    private const int DadoLogPage = 2;
    private const int LogSaude = 2;
    private const int CnsControlador = 1;

    private const uint DigcfPresente = 0x02;
    private const uint DigcfInterface = 0x10;
    private const uint CompartilharLeituraGravacao = 3;
    private const uint AbrirExistente = 3;
    private const int BufferInsuficiente = 122;
    private const int MaisDados = 234;
    private const uint TipoUint32 = 7;
    private const int NiveisAcima = 6;

    private static readonly Guid InterfaceDisco = new("53f56307-b6bf-11d0-94f2-00a0c91efb8b");
    private static readonly Guid PropriedadesPci = new("3ab22e31-8264-4b4e-9af5-a8d2d8e33e62");

    public IReadOnlyList<DiscoBruto> Discos()
    {
        var discos = new Dictionary<int, DiscoBruto>();
        foreach (var (caminho, instancia) in InterfacesDeDisco())
        {
            using var disco = CreateFileW(caminho, 0, CompartilharLeituraGravacao, 0, AbrirExistente, 0, 0);
            if (disco.IsInvalid || Ioctl(disco, NumeroDispositivo, null, 12) is not { } numero)
            {
                continue;
            }

            var n = BinaryPrimitives.ReadInt32LittleEndian(numero.AsSpan(4));
            discos.TryAdd(n, new DiscoBruto(
                n,
                Propriedade(disco, PropriedadeDispositivo, 4096),
                Propriedade(disco, PropriedadePenalidadeBusca, 12) is { } p ? p[8] != 0 : null,
                Propriedade(disco, PropriedadeTrim, 12) is { } t ? t[8] != 0 : null,
                Ioctl(disco, Geometria, null, 256) is { } g ? BinaryPrimitives.ReadInt64LittleEndian(g.AsSpan(24)) : null,
                LayoutCrescente(disco) is { } l ? BinaryPrimitives.ReadInt32LittleEndian(l) : null,
                PropriedadePci(instancia, 9),
                PropriedadePci(instancia, 10)));
        }

        return discos.Values.OrderBy(d => d.Numero).ToList();
    }

    public byte[]? LogSaudeNvme(int numero) => ProtocoloNvme(numero, PropriedadeProtocoloDispositivo, DadoLogPage, LogSaude, 512);

    public byte[]? IdentificacaoNvme(int numero) => ProtocoloNvme(numero, PropriedadeProtocoloAdaptador, DadoIdentify, CnsControlador, 4096);

    /// <summary>
    /// Consulta de protocolo NVMe, como em "Working with NVMe drives" (learn.microsoft.com): STORAGE_PROPERTY_QUERY
    /// (8 bytes até AdditionalParameters) seguida do STORAGE_PROTOCOL_SPECIFIC_DATA (40 bytes) e da área de dados;
    /// a resposta é um STORAGE_PROTOCOL_DATA_DESCRIPTOR (Version e Size mais a mesma estrutura de 40 bytes), com os
    /// dados no ProtocolDataOffset contado do início da estrutura de 40 bytes. ProtocolTypeNvme 3,
    /// NVMeDataTypeIdentify 1, NVMeDataTypeLogPage 2, NVME_LOG_PAGE_HEALTH_INFO 2 e NVME_IDENTIFY_CNS_CONTROLLER 1
    /// pelos ntddstor.h e nvme.h do SDK.
    /// </summary>
    private static byte[]? ProtocoloNvme(int numero, int propriedade, int tipoDado, int pedido, int tamanho)
    {
        const int inicioDados = 8;
        const int tamanhoProtocolo = 40;
        using var disco = CreateFileW($@"\\.\PhysicalDrive{numero}", 0, CompartilharLeituraGravacao, 0, AbrirExistente, 0, 0);
        if (disco.IsInvalid)
        {
            return null;
        }

        var buffer = new byte[inicioDados + tamanhoProtocolo + tamanho];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0), propriedade);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(inicioDados + 0), ProtocoloNvmeTipo);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(inicioDados + 4), tipoDado);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(inicioDados + 8), pedido);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(inicioDados + 16), tamanhoProtocolo);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(inicioDados + 20), tamanho);
        if (!DeviceIoControl(disco, ConsultaPropriedade, buffer, buffer.Length, buffer, buffer.Length, out _, 0))
        {
            return null;
        }

        // Resposta: Version (0) e Size (4) do descritor, depois a estrutura de protocolo a partir do byte 8.
        var deslocamento = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(inicioDados + 16));
        var comprimento = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(inicioDados + 20));
        var inicio = inicioDados + deslocamento;
        return deslocamento <= 0 || comprimento < tamanho || inicio + tamanho > buffer.Length ? null : buffer.AsSpan(inicio, tamanho).ToArray();
    }

    /// <summary>Caminho e instância de cada interface de disco presente (SetupDiGetClassDevsW com GUID_DEVINTERFACE_DISK).</summary>
    private static List<(string Caminho, uint Instancia)> InterfacesDeDisco()
    {
        var resultado = new List<(string, uint)>();
        var guid = InterfaceDisco;
        var lista = SetupDiGetClassDevsW(ref guid, null, 0, DigcfPresente | DigcfInterface);
        if (lista == -1)
        {
            return resultado;
        }

        try
        {
            for (uint i = 0; ; i++)
            {
                var interfaceDados = new DadosInterface { Tamanho = (uint)Marshal.SizeOf<DadosInterface>() };
                if (!SetupDiEnumDeviceInterfaces(lista, 0, ref guid, i, ref interfaceDados))
                {
                    break;
                }

                var dispositivo = new DadosDispositivo { Tamanho = (uint)Marshal.SizeOf<DadosDispositivo>() };
                _ = SetupDiGetDeviceInterfaceDetailW(lista, ref interfaceDados, 0, 0, out var necessario, ref dispositivo);
                if (necessario == 0)
                {
                    continue;
                }

                var detalhe = Marshal.AllocHGlobal((int)necessario);
                try
                {
                    // SP_DEVICE_INTERFACE_DETAIL_DATA_W: cbSize é 8 em 64 bits; o caminho começa no byte 4.
                    Marshal.WriteInt32(detalhe, IntPtr.Size == 8 ? 8 : 6);
                    if (SetupDiGetDeviceInterfaceDetailW(lista, ref interfaceDados, detalhe, necessario, out _, ref dispositivo)
                        && Marshal.PtrToStringUni(detalhe + 4) is { Length: > 0 } caminho)
                    {
                        resultado.Add((caminho, dispositivo.Instancia));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detalhe);
                }
            }
        }
        finally
        {
            _ = SetupDiDestroyDeviceInfoList(lista);
        }

        return resultado;
    }

    /// <summary>Sobe pela árvore de dispositivos até achar a propriedade de PCIe do controlador.</summary>
    private static uint? PropriedadePci(uint instancia, uint id)
    {
        var chave = new ChavePropriedade { Categoria = PropriedadesPci, Id = id };
        var atual = instancia;
        for (var nivel = 0; nivel < NiveisAcima; nivel++)
        {
            if (CM_Get_Parent(out var pai, atual, 0) != 0)
            {
                return null;
            }

            var valor = new byte[4];
            uint tamanho = 4;
            if (CM_Get_DevNode_PropertyW(pai, ref chave, out var tipo, valor, ref tamanho, 0) == 0 && tipo == TipoUint32 && tamanho == 4)
            {
                return BinaryPrimitives.ReadUInt32LittleEndian(valor);
            }

            atual = pai;
        }

        return null;
    }

    private static byte[]? Propriedade(SafeFileHandle disco, int propriedade, int tamanho)
    {
        var consulta = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(consulta, propriedade);
        return Ioctl(disco, ConsultaPropriedade, consulta, tamanho);
    }

    /// <summary>O layout cresce com o número de partições: o buffer dobra até caber.</summary>
    private static byte[]? LayoutCrescente(SafeFileHandle disco)
    {
        for (var tamanho = 4096; tamanho <= 1 << 20; tamanho *= 2)
        {
            if (Ioctl(disco, Layout, null, tamanho) is { } layout)
            {
                return layout;
            }

            if (Marshal.GetLastPInvokeError() is not (BufferInsuficiente or MaisDados))
            {
                return null;
            }
        }

        return null;
    }

    internal static byte[]? Ioctl(SafeFileHandle disco, uint codigo, byte[]? entrada, int tamanhoSaida)
    {
        var saida = new byte[tamanhoSaida];
        return DeviceIoControl(disco, codigo, entrada, entrada?.Length ?? 0, saida, saida.Length, out _, 0) ? saida : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DadosInterface
    {
        public uint Tamanho;
        public Guid Classe;
        public uint Sinais;
        public nint Reservado;
    }

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

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial SafeFileHandle CreateFileW(string nome, uint acesso, uint compartilhar, nint seguranca, uint disposicao, uint atributos, nint modelo);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(SafeFileHandle disco, uint codigo, byte[]? entrada, int tamanhoEntrada, byte[] saida, int tamanhoSaida, out int lidos, nint sobreposto);

    [LibraryImport("setupapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint SetupDiGetClassDevsW(ref Guid classe, string? enumerador, nint janela, uint sinais);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiEnumDeviceInterfaces(nint lista, nint dispositivo, ref Guid classe, uint indice, ref DadosInterface dados);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDeviceInterfaceDetailW(nint lista, ref DadosInterface dados, nint detalhe, uint tamanho, out uint necessario, ref DadosDispositivo dispositivo);

    [LibraryImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiDestroyDeviceInfoList(nint lista);

    [LibraryImport("cfgmgr32.dll")]
    private static partial uint CM_Get_Parent(out uint pai, uint filho, uint sinais);

    [LibraryImport("cfgmgr32.dll")]
    private static partial uint CM_Get_DevNode_PropertyW(uint instancia, ref ChavePropriedade chave, out uint tipo, byte[] valor, ref uint tamanho, uint sinais);
}
