using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapHard.Nucleo.Discos;

/// <summary>
/// Um volume com letra. <see cref="ProtecaoBitLocker"/> segue o ProtectionStatus do Win32_EncryptableVolume
/// (0 desligado, 1 ligado, 2 desconhecido); nulo quando não foi lido, com o motivo em <see cref="FalhaBitLocker"/>.
/// </summary>
public sealed record VolumeBruto(
    string Letra,
    string? Rotulo,
    string? SistemaArquivos,
    long? LivreBytes,
    long? TotalBytes,
    int? Disco,
    int? ProtecaoBitLocker,
    string? FalhaBitLocker);

/// <summary>Liga os volumes aos discos pelo número do disco.</summary>
public static class Volumes
{
    public const string RequerAdministrador = "requer administrador";

    public static IReadOnlyList<VolumeBruto> DoDisco(IEnumerable<VolumeBruto> volumes, int disco) =>
        volumes.Where(v => v.Disco == disco).OrderBy(v => v.Letra, StringComparer.Ordinal).ToList();

    /// <summary>Volumes que não se ligaram a nenhum disco (volume em mais de um disco, por exemplo).</summary>
    public static IReadOnlyList<VolumeBruto> SemDisco(IEnumerable<VolumeBruto> volumes, IEnumerable<int> discos)
    {
        var conhecidos = discos.ToHashSet();
        return volumes.Where(v => v.Disco is not { } d || !conhecidos.Contains(d)).OrderBy(v => v.Letra, StringComparer.Ordinal).ToList();
    }

    public static string? NomeBitLocker(int? protecao) => protecao switch
    {
        0 => "desligado",
        1 => "ligado",
        2 => "não foi possível saber (volume bloqueado?)",
        _ => null,
    };
}

/// <summary>
/// Leitura real dos volumes com letra. Fontes (learn.microsoft.com, 01/10/2026): GetLogicalDriveStringsW,
/// GetDriveTypeW (2 removível, 3 fixo; os outros tipos não entram), GetVolumeInformationW,
/// GetDiskFreeSpaceExW; o disco do volume pelo IOCTL_STORAGE_GET_DEVICE_NUMBER em \\.\X:, com acesso 0;
/// BitLocker pela classe Win32_EncryptableVolume (Root\CIMV2\Security\MicrosoftVolumeEncryption), que exige
/// administrador e conexão cifrada, lida pelo moniker do WMI, sem pacote externo.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteVolumesWindows
{
    private const uint Removivel = 2;
    private const uint Fixo = 3;
    private const string ConsultaBitLocker = "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume";
    private const string Moniker = @"winmgmts:{impersonationLevel=impersonate,authenticationLevel=pktPrivacy}!\\.\root\CIMV2\Security\MicrosoftVolumeEncryption";

    public IReadOnlyList<VolumeBruto> Ler(bool administrador)
    {
        var (bitLocker, falha) = administrador ? LerBitLocker() : (null, Volumes.RequerAdministrador);
        var resultado = new List<VolumeBruto>();
        foreach (var raiz in Raizes())
        {
            var tipo = GetDriveTypeW(raiz);
            if (tipo is not (Removivel or Fixo))
            {
                continue;
            }

            var letra = raiz[..2];
            var rotulo = new char[261];
            var sistema = new char[261];
            var temInformacao = GetVolumeInformationW(raiz, rotulo, rotulo.Length, out _, out _, out _, sistema, sistema.Length);
            var temEspaco = GetDiskFreeSpaceExW(raiz, out var livreUsuario, out var total, out _);
            var protecao = bitLocker?.GetValueOrDefault(letra);
            resultado.Add(new VolumeBruto(
                letra,
                temInformacao ? Texto(rotulo) : null,
                temInformacao ? Texto(sistema) : null,
                temEspaco ? (long)livreUsuario : null,
                temEspaco ? (long)total : null,
                Disco(letra),
                protecao,
                protecao is null ? falha ?? "volume fora da lista do BitLocker" : null));
        }

        return resultado;
    }

    private static IEnumerable<string> Raizes()
    {
        var buffer = new char[1024];
        var tamanho = GetLogicalDriveStringsW((uint)buffer.Length, buffer);
        return tamanho == 0 || tamanho > buffer.Length
            ? []
            : new string(buffer, 0, (int)tamanho).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// O disco do volume. Só vale o tipo FILE_DEVICE_DISK (7, devioctl.h): unidade virtual, como a de
    /// sincronização de nuvem, volta FILE_DEVICE_DISK_FILE_SYSTEM (8) com o número de um disco real e
    /// partição -1, e não pode aparecer como partição dele.
    /// </summary>
    private static int? Disco(string letra)
    {
        const int dispositivoDisco = 7;
        using var volume = FonteDiscosWindows.CreateFileW($@"\\.\{letra}", 0, 3, 0, 3, 0, 0);
        return !volume.IsInvalid
            && FonteDiscosWindows.Ioctl(volume, FonteDiscosWindows.NumeroDispositivo, null, 12) is { } n
            && BinaryPrimitives.ReadInt32LittleEndian(n) == dispositivoDisco
                ? BinaryPrimitives.ReadInt32LittleEndian(n.AsSpan(4))
                : null;
    }

    /// <summary>ProtectionStatus por letra ("C:"), ou o motivo da falha.</summary>
    private static (Dictionary<string, int>? Protecao, string? Falha) LerBitLocker()
    {
        try
        {
            dynamic servico = Marshal.BindToMoniker(Moniker);
            var protecao = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (dynamic volume in servico.ExecQuery(ConsultaBitLocker))
            {
                if (volume.DriveLetter is string letra && volume.ProtectionStatus is not null)
                {
                    protecao[letra] = Convert.ToInt32(volume.ProtectionStatus);
                }
            }

            return (protecao, null);
        }
        catch (Exception erro) when (erro is COMException or UnauthorizedAccessException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return (null, $"BitLocker não lido: {erro.Message}");
        }
    }

    private static string? Texto(char[] buffer)
    {
        var fim = Array.IndexOf(buffer, '\0');
        var texto = new string(buffer, 0, fim < 0 ? buffer.Length : fim).Trim();
        return texto.Length == 0 ? null : texto;
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint GetLogicalDriveStringsW(uint tamanho, [Out] char[] buffer);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint GetDriveTypeW(string raiz);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetVolumeInformationW(string raiz, [Out] char[] rotulo, int tamanhoRotulo, out uint serie, out uint maximoNome, out uint sinais, [Out] char[] sistema, int tamanhoSistema);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceExW(string raiz, out ulong livreUsuario, out ulong total, out ulong livreTotal);
}
