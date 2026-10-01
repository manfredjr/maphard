using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapHard.Nucleo.Eventos;

/// <summary>Leitura do log Sistema. Os testes trocam por XML montado à mão.</summary>
public interface IFonteEventos
{
    /// <summary>XML e mensagem de cada evento dos filtros, nos últimos <paramref name="dias"/> dias, do mais novo para o mais antigo, até <paramref name="limite"/> por consulta.</summary>
    IReadOnlyList<(string Xml, string? Mensagem)> Ler(IReadOnlyList<FiltroEvento> filtros, int dias, int limite);
}

/// <summary>
/// Leitura real pela wevtapi, sem administrador (o log Sistema abriu como usuário comum no teste de 01/10/2026).
/// Assinaturas e valores pela documentação do winevt.h (learn.microsoft.com) e pelo winevt.h do SDK:
/// EvtQueryChannelPath 0x1, EvtQueryReverseDirection 0x200, EvtRenderEventXml 1, EvtFormatMessageEvent 1.
/// Os handles são usados e fechados no mesmo thread, como a página do EvtQuery pede.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class FonteEventosWindows : IFonteEventos
{
    private const uint CaminhoCanal = 0x1;
    private const uint DoMaisNovo = 0x200;
    private const uint RenderizarXml = 1;
    private const uint MensagemDoEvento = 1;
    private const int SemMaisItens = 259;
    private const int BufferInsuficiente = 122;
    private const int Bloco = 32;
    private const uint EsperaMs = 2000;

    public IReadOnlyList<(string Xml, string? Mensagem)> Ler(IReadOnlyList<FiltroEvento> filtros, int dias, int limite)
    {
        var resultado = new List<(string, string?)>();
        var metadados = new Dictionary<string, nint>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var consulta in LeitorEvento.Consultas(filtros, dias))
            {
                var conjunto = EvtQuery(0, "System", consulta, CaminhoCanal | DoMaisNovo);
                if (conjunto == 0)
                {
                    throw new InvalidOperationException($"o log Sistema não abriu (erro {Marshal.GetLastPInvokeError()})");
                }

                try
                {
                    LerConjunto(conjunto, limite, resultado, metadados);
                }
                finally
                {
                    EvtClose(conjunto);
                }
            }
        }
        finally
        {
            foreach (var m in metadados.Values.Where(m => m != 0))
            {
                EvtClose(m);
            }
        }

        return resultado;
    }

    private static void LerConjunto(nint conjunto, int limite, List<(string, string?)> resultado, Dictionary<string, nint> metadados)
    {
        var eventos = new nint[Bloco];
        var lidos = 0;
        while (lidos < limite)
        {
            if (!EvtNext(conjunto, Bloco, eventos, EsperaMs, 0, out var devolvidos))
            {
                if (Marshal.GetLastPInvokeError() == SemMaisItens)
                {
                    return;
                }

                throw new InvalidOperationException($"leitura do log Sistema falhou (erro {Marshal.GetLastPInvokeError()})");
            }

            for (var i = 0; i < devolvidos; i++)
            {
                try
                {
                    if (lidos < limite && Xml(eventos[i]) is { } xml)
                    {
                        resultado.Add((xml, Mensagem(eventos[i], xml, metadados)));
                        lidos++;
                    }
                }
                finally
                {
                    EvtClose(eventos[i]);
                }
            }
        }
    }

    private static string? Xml(nint evento)
    {
        _ = EvtRender(0, evento, RenderizarXml, 0, null, out var necessario, out _);
        if (Marshal.GetLastPInvokeError() != BufferInsuficiente || necessario == 0)
        {
            return null;
        }

        var buffer = new byte[necessario];
        return EvtRender(0, evento, RenderizarXml, necessario, buffer, out var usado, out _)
            ? System.Text.Encoding.Unicode.GetString(buffer, 0, (int)usado).TrimEnd('\0')
            : null;
    }

    /// <summary>Mensagem no idioma do Windows. Provedor sem metadados (fonte antiga) fica sem mensagem.</summary>
    private static string? Mensagem(nint evento, string xml, Dictionary<string, nint> metadados)
    {
        var provedor = LeitorEvento.Interpretar(xml)?.Provedor;
        if (provedor is null)
        {
            return null;
        }

        if (!metadados.TryGetValue(provedor, out var publicador))
        {
            publicador = EvtOpenPublisherMetadata(0, provedor, null, 0, 0);
            metadados[provedor] = publicador;
        }

        if (publicador == 0)
        {
            return null;
        }

        _ = EvtFormatMessage(publicador, evento, 0, 0, 0, MensagemDoEvento, 0, null, out var necessario);
        if (Marshal.GetLastPInvokeError() != BufferInsuficiente || necessario == 0)
        {
            return null;
        }

        var buffer = new char[necessario];
        return EvtFormatMessage(publicador, evento, 0, 0, 0, MensagemDoEvento, necessario, buffer, out var usado)
            ? new string(buffer, 0, (int)Math.Max(usado - 1, 0))
            : null;
    }

    [LibraryImport("wevtapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint EvtQuery(nint sessao, string caminho, string consulta, uint sinais);

    [LibraryImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EvtNext(nint conjunto, int quantidade, [Out] nint[] eventos, uint espera, uint sinais, out int devolvidos);

    [LibraryImport("wevtapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EvtRender(nint contexto, nint fragmento, uint sinais, uint tamanho, [Out] byte[]? buffer, out uint usado, out uint propriedades);

    [LibraryImport("wevtapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint EvtOpenPublisherMetadata(nint sessao, string publicador, string? arquivo, uint idioma, uint sinais);

    [LibraryImport("wevtapi.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EvtFormatMessage(nint publicador, nint evento, uint mensagem, uint quantidade, nint valores, uint sinais, uint tamanho, [Out] char[]? buffer, out uint usado);

    [LibraryImport("wevtapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EvtClose(nint objeto);
}
