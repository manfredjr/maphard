using System.Buffers.Binary;
using System.Text;
using MapHard.Nucleo.Campos;

namespace MapHard.Nucleo.Video;

/// <summary>Um monitor presente (R32).</summary>
public sealed record Monitor(
    Campo<string> Fabricante,
    Campo<string> Modelo,
    Campo<string> NumeroSerie,
    Campo<int> AnoFabricacao,
    Campo<double> Polegadas,
    Campo<string> ResolucaoNativa);

/// <summary>
/// Interpreta o bloco base do EDID (128 bytes), pelo struct edid do Linux (include/drm/drm_edid.h) e pelo
/// drivers/gpu/drm/drm_edid.c: cabeçalho 00 FF FF FF FF FF FF 00; fabricante em 8 e 9 (big-endian, três letras de
/// 5 bits a partir de '@'); produto em 10 e 11; série em 12 a 15; semana em 16 (0xFF é "ano do modelo"); ano em 17,
/// somado a 1990; largura e altura em cm em 21 e 22; quatro descritores de 18 bytes a partir de 54. Descritor de
/// texto tem os dois primeiros bytes zerados e a marca no byte 3: 0xFC nome, 0xFF número de série. O primeiro
/// descritor de tempo traz a resolução ativa: horizontal = (byte 4 &amp; 0xF0) &lt;&lt; 4 | byte 2, vertical = (byte 7 &amp; 0xF0) &lt;&lt; 4 | byte 5.
/// </summary>
public static class LeitorEdid
{
    public const int TamanhoBloco = 128;
    private const double CentimetrosPorPolegada = 2.54;
    private static readonly byte[] _cabecalho = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    public static Monitor? Interpretar(byte[]? edid)
    {
        if (edid is null || edid.Length < TamanhoBloco || !edid.AsSpan(0, 8).SequenceEqual(_cabecalho))
        {
            return null;
        }

        const FonteDado f = FonteDado.Windows;
        var fabricante = Fabricante(BinaryPrimitives.ReadUInt16BigEndian(edid.AsSpan(8)));
        var produto = BinaryPrimitives.ReadUInt16LittleEndian(edid.AsSpan(10));
        string? nome = null;
        string? serie = null;
        string? resolucao = null;
        for (var i = 0; i < 4; i++)
        {
            var d = edid.AsSpan(54 + (i * 18), 18);
            if (d[0] == 0 && d[1] == 0 && d[2] == 0)
            {
                var texto = Texto(d[5..]);
                switch (d[3])
                {
                    case 0xFC:
                        nome ??= texto;
                        break;
                    case 0xFF:
                        serie ??= texto;
                        break;
                }
            }
            else if (resolucao is null && (d[0] != 0 || d[1] != 0))
            {
                var horizontal = ((d[4] & 0xF0) << 4) | d[2];
                var vertical = ((d[7] & 0xF0) << 4) | d[5];
                resolucao = horizontal > 0 && vertical > 0 ? $"{horizontal} x {vertical}" : null;
            }
        }

        var largura = edid[21];
        var altura = edid[22];
        var ano = edid[17] + 1990;
        var soma = 0;
        for (var i = 0; i < TamanhoBloco; i++)
        {
            soma += edid[i];
        }

        var observacao = (soma & 0xFF) == 0 ? null : "soma de verificação do EDID não confere";
        return new Monitor(
            fabricante is null ? Campo<string>.NaoInformado(f) : Campo<string>.Lido(fabricante, f, observacao),
            nome is not null ? Campo.Texto(nome, f) : Campo<string>.Lido($"produto {produto:X4}", f, "o monitor não grava o nome no EDID"),
            Campo.Texto(serie, f),
            edid[17] == 0 ? Campo<int>.NaoInformado(f) : Campo<int>.Lido(ano, f, edid[16] == 0xFF ? "ano do modelo" : null),
            largura == 0 || altura == 0
                ? Campo<double>.NaoInformado(f, "o EDID não informa o tamanho (projetor ou tamanho variável)")
                : Campo<double>.Lido(Math.Round(Math.Sqrt((largura * largura) + (altura * altura)) / CentimetrosPorPolegada, 1), f, $"{largura} x {altura} cm"),
            Campo.Texto(resolucao, f));
    }

    /// <summary>Três letras de 5 bits: 1 é "A". Letra fora de A a Z vira nulo.</summary>
    internal static string? Fabricante(ushort codigo)
    {
        var letras = new[] { (codigo >> 10) & 0x1F, (codigo >> 5) & 0x1F, codigo & 0x1F };
        return letras.All(l => l is >= 1 and <= 26) ? new string(letras.Select(l => (char)('@' + l)).ToArray()) : null;
    }

    /// <summary>Texto do descritor: 13 bytes, termina em 0x0A e o resto é espaço.</summary>
    private static string Texto(ReadOnlySpan<byte> bytes)
    {
        var texto = new StringBuilder();
        foreach (var b in bytes[..13])
        {
            if (b == 0x0A)
            {
                break;
            }

            texto.Append(b is >= 32 and < 127 ? (char)b : '.');
        }

        return texto.ToString().Trim();
    }
}
