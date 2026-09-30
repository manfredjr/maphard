using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MapHard.Nucleo.Campos;
using Microsoft.Win32;

namespace MapHard.Nucleo.Firmware;

/// <summary>Resultado do Tbsi_GetDeviceInfo: versão do TPM (1 para 1.2, 2 para 2.0) ou o código de erro.</summary>
public readonly record struct RespostaTpm(uint Codigo, uint Versao);

/// <summary>Leituras de firmware que vêm de APIs do Windows. Os testes trocam por valores fixos.</summary>
public interface IFonteFirmware
{
    /// <summary>FIRMWARE_TYPE: 0 desconhecido, 1 BIOS legado, 2 UEFI. Null quando a API falha.</summary>
    int? TipoFirmware();

    /// <summary>IsProcessorFeaturePresent(PF_VIRT_FIRMWARE_ENABLED).</summary>
    bool VirtualizacaoLigadaNoFirmware();

    RespostaTpm? Tpm();
}

/// <summary>Leitura de valor em HKEY_LOCAL_MACHINE. Os testes trocam por um dicionário.</summary>
public interface IFonteRegistro
{
    object? Ler(string chave, string valor);
}

public sealed record DadosFirmware(
    Campo<string> Modo,
    Campo<bool> SecureBoot,
    Campo<string> TpmVersao,
    Campo<string> TpmFabricante,
    Campo<string> Microcodigo,
    Campo<bool> VirtualizacaoLigada);

/// <summary>
/// Firmware e segurança, sem administrador.
/// Fontes: GetFirmwareType e FIRMWARE_TYPE (winbase, winnt), IsProcessorFeaturePresent com
/// PF_VIRT_FIRMWARE_ENABLED = 21 e Tbsi_GetDeviceInfo com TBS_E_TPM_NOT_FOUND = 0x8028400F
/// (MicrosoftDocs/sdk-api). [CONFERIR] os valores TPM_VERSION_12 = 1 e TPM_VERSION_20 = 2 do tbs.h,
/// a chave do Secure Boot e o formato do valor "Update Revision", que a documentação lida não traz.
/// </summary>
public static class LeitorFirmware
{
    internal const string ChaveSecureBoot = @"SYSTEM\CurrentControlSet\Control\SecureBoot\State";
    internal const string ChaveProcessador = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
    internal const uint TpmNaoEncontrado = 0x8028400F;

    public static DadosFirmware Ler(IFonteFirmware firmware, IFonteRegistro registro, bool administrador)
    {
        var tipo = firmware.TipoFirmware();
        var modo = tipo switch
        {
            1 => Campo<string>.Lido("BIOS legado", FonteDado.Firmware),
            2 => Campo<string>.Lido("UEFI", FonteDado.Firmware),
            null => Campo<string>.Erro(FonteDado.Firmware, "GetFirmwareType falhou"),
            _ => Campo<string>.NaoInformado(FonteDado.Firmware),
        };

        // Em modo legado não existe Secure Boot. Em UEFI, a chave ausente é "não informado".
        var secureBoot = tipo == 1
            ? Campo<bool>.NaoSuportado(FonteDado.Registro, "firmware em modo legado")
            : registro.Ler(ChaveSecureBoot, "UEFISecureBootEnabled") is int ligado
                ? Campo<bool>.Lido(ligado == 1, FonteDado.Registro)
                : Campo<bool>.NaoInformado(FonteDado.Registro);

        var tpm = firmware.Tpm();
        var tpmVersao = tpm switch
        {
            null => Campo<string>.Erro(FonteDado.Tpm, "Tbsi_GetDeviceInfo indisponível"),
            { Codigo: TpmNaoEncontrado } => Campo<string>.NaoSuportado(FonteDado.Tpm, "TPM não encontrado"),
            { Codigo: not 0 } t => Campo<string>.Erro(FonteDado.Tpm, $"código 0x{t.Codigo:X8}"),
            { Versao: 1 } => Campo<string>.Lido("1.2", FonteDado.Tpm),
            { Versao: 2 } => Campo<string>.Lido("2.0", FonteDado.Tpm),
            _ => Campo<string>.NaoInformado(FonteDado.Tpm),
        };

        // O fabricante do TPM só sai com administrador. A leitura elevada entra quando a pergunta 2 do desenho for decidida.
        var tpmFabricante = tpmVersao.Estado == EstadoCampo.NaoSuportado
            ? Campo<string>.NaoSuportado(FonteDado.Tpm)
            : administrador ? Campo<string>.NaoInformado(FonteDado.Tpm, "leitura elevada ainda não implementada") : Campo<string>.RequerAdministrador(FonteDado.Tpm);

        return new DadosFirmware(
            modo,
            secureBoot,
            tpmVersao,
            tpmFabricante,
            Microcodigo(registro.Ler(ChaveProcessador, "Update Revision")),
            firmware.VirtualizacaoLigadaNoFirmware()
                ? Campo<bool>.Lido(true, FonteDado.Windows)
                : Campo<bool>.Lido(false, FonteDado.Windows, "com o Hyper-V ativo, o Windows pode informar desligado"));
    }

    /// <summary>
    /// "Update Revision" é binário de 8 bytes. Mostra a metade alta quando ela não é zero (formato usado
    /// pela Intel) e a baixa nos outros casos. [CONFERIR] o formato na documentação.
    /// </summary>
    internal static Campo<string> Microcodigo(object? valor)
    {
        if (valor is not byte[] { Length: >= 8 } bytes)
        {
            return Campo<string>.NaoInformado(FonteDado.Registro);
        }

        var alta = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        var baixa = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        var revisao = alta != 0 ? alta : baixa;
        return revisao == 0 ? Campo<string>.NaoInformado(FonteDado.Registro) : Campo<string>.Lido($"0x{revisao:X}", FonteDado.Registro);
    }
}

[SupportedOSPlatform("windows")]
public sealed class FonteRegistroWindows : IFonteRegistro
{
    public object? Ler(string chave, string valor)
    {
        using var aberta = Registry.LocalMachine.OpenSubKey(chave);
        return aberta?.GetValue(valor);
    }
}

[SupportedOSPlatform("windows")]
public sealed partial class FonteFirmwareWindows : IFonteFirmware
{
    private const int VirtualizacaoFirmware = 21;

    public int? TipoFirmware() => GetFirmwareType(out var tipo) ? tipo : null;

    public bool VirtualizacaoLigadaNoFirmware() => IsProcessorFeaturePresent(VirtualizacaoFirmware);

    public RespostaTpm? Tpm()
    {
        try
        {
            var info = new byte[16];
            var codigo = Tbsi_GetDeviceInfo((uint)info.Length, info);
            return new RespostaTpm(codigo, codigo == 0 ? BinaryPrimitives.ReadUInt32LittleEndian(info.AsSpan(4)) : 0);
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFirmwareType(out int tipo);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsProcessorFeaturePresent(int recurso);

    [LibraryImport("tbs.dll")]
    private static partial uint Tbsi_GetDeviceInfo(uint tamanho, byte[] info);
}
