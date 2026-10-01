using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapHard.Nucleo.Video;

/// <summary>Leitura das placas de vídeo. Os testes trocam por descrições montadas à mão.</summary>
public interface IFonteVideo
{
    /// <summary>O DXGI_ADAPTER_DESC1 de cada adaptador, na ordem da DXGI.</summary>
    IReadOnlyList<byte[]> Adaptadores();
}

/// <summary>
/// Leitura real pela DXGI, sem administrador e sem pacote externo: CreateDXGIFactory1 com o IID do IDXGIFactory1
/// (770aae78-f26f-4dba-a829-253c83d1b387), EnumAdapters1 na posição 12 da tabela de métodos e
/// IDXGIAdapter1::GetDesc1 na posição 10, pela ordem do dxgi.h do SDK. A lista acaba em DXGI_ERROR_NOT_FOUND.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe partial class FonteVideoWindows : IFonteVideo
{
    private const int PosicaoEnumAdapters1 = 12;
    private const int PosicaoGetDesc1 = 10;
    private const int Limite = 16;

    private static readonly Guid Fabrica1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    public IReadOnlyList<byte[]> Adaptadores()
    {
        var iid = Fabrica1;
        var resultado = CreateDXGIFactory1(ref iid, out var fabrica);
        if (resultado != 0)
        {
            throw new InvalidOperationException($"a DXGI não abriu (0x{resultado:X8})");
        }

        var lista = new List<byte[]>();
        try
        {
            var enumerar = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)(*(nint**)fabrica)[PosicaoEnumAdapters1];
            for (uint i = 0; i < Limite; i++)
            {
                nint adaptador;
                if (enumerar(fabrica, i, &adaptador) != 0)
                {
                    break;
                }

                try
                {
                    var descricao = new byte[LeitorVideo.TamanhoDescricao];
                    var obter = (delegate* unmanaged[Stdcall]<nint, byte*, int>)(*(nint**)adaptador)[PosicaoGetDesc1];
                    fixed (byte* p = descricao)
                    {
                        if (obter(adaptador, p) == 0)
                        {
                            lista.Add(descricao);
                        }
                    }
                }
                finally
                {
                    Marshal.Release(adaptador);
                }
            }
        }
        finally
        {
            Marshal.Release(fabrica);
        }

        return lista;
    }

    [LibraryImport("dxgi.dll")]
    private static partial int CreateDXGIFactory1(ref Guid iid, out nint fabrica);
}
