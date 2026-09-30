using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapHard.Nucleo.Smbios;

/// <summary>De onde vem a tabela SMBIOS bruta. Os testes trocam por uma tabela montada à mão.</summary>
public interface IFonteSmbios
{
    /// <summary>A tabela com o cabeçalho RawSMBIOSData, ou null quando a API falha.</summary>
    byte[]? LerTabelaBruta();
}

/// <summary>
/// Lê a tabela pelo <c>GetSystemFirmwareTable</c>, provedor 'RSMB', sem administrador.
/// Documentação: MicrosoftDocs/sdk-api, sysinfoapi/nf-sysinfoapi-getsystemfirmwaretable.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteSmbiosWindows : IFonteSmbios
{
    // 'RSMB' como constante de quatro caracteres do C: 0x52534D42.
    private const uint ProvedorRsmb = 0x52534D42;

    public byte[]? LerTabelaBruta()
    {
        var tamanho = GetSystemFirmwareTable(ProvedorRsmb, 0, null, 0);
        if (tamanho == 0)
        {
            return null;
        }

        var buffer = new byte[tamanho];
        var lidos = GetSystemFirmwareTable(ProvedorRsmb, 0, buffer, tamanho);
        return lidos == 0 || lidos > tamanho ? null : buffer[..(int)lidos];
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint GetSystemFirmwareTable(uint provedor, uint identificador, byte[]? buffer, uint tamanho);
}
