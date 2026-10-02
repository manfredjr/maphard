using System.Buffers.Binary;
using System.Text;
using MapHard.Nucleo.Campos;

namespace MapHard.Nucleo.Baterias;

/// <summary>Uma bateria do sistema (R33). Desgaste em porcentagem.</summary>
public sealed record Bateria(
    Campo<string> Nome,
    Campo<string> Fabricante,
    Campo<string> Quimica,
    Campo<long> CapacidadeProjetoMwh,
    Campo<long> CapacidadeAtualMwh,
    Campo<double> Desgaste,
    Campo<int> Ciclos);

/// <summary>O que a fonte lê de cada bateria: o BATTERY_INFORMATION cru, o nome e o fabricante.</summary>
public sealed record BateriaBruta(byte[] Informacao, string? Nome, string? Fabricante);

/// <summary>
/// Interpreta o BATTERY_INFORMATION (Poclass.h, página "BATTERY_INFORMATION structure"): Capabilities em 0,
/// Chemistry em 8 (4 bytes, sem zero no fim), DesignedCapacity em 12, FullChargedCapacity em 16, CycleCount em 32;
/// 36 bytes ao todo. BATTERY_SYSTEM_BATTERY 0x80000000, BATTERY_CAPACITY_RELATIVE 0x40000000,
/// BATTERY_IS_SHORT_TERM 0x20000000 e BATTERY_UNKNOWN_CAPACITY 0xFFFFFFFF vêm do poclass.h do SDK.
/// </summary>
public static class LeitorBateria
{
    public const int TamanhoInformacao = 36;
    public const string SemBateria = "não disponível neste equipamento";

    private const uint BateriaDoSistema = 0x80000000;
    private const uint CapacidadeRelativa = 0x40000000;
    private const uint CurtoPrazo = 0x20000000;
    private const uint CapacidadeDesconhecida = 0xFFFFFFFF;

    /// <summary>Abreviações da página da Microsoft. Outra química aparece como veio.</summary>
    private static readonly Dictionary<string, string> Quimicas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PbAc"] = "chumbo-ácido",
        ["LION"] = "íon de lítio",
        ["Li-I"] = "íon de lítio",
        ["NiCd"] = "níquel-cádmio",
        ["NiMH"] = "níquel-hidreto metálico",
        ["NiZn"] = "níquel-zinco",
        ["RAM"] = "alcalina recarregável de manganês",
    };

    public static IReadOnlyList<Bateria> Montar(IEnumerable<BateriaBruta> brutas) =>
        brutas.Select(Interpretar).OfType<Bateria>().ToList();

    /// <summary>Nulo para bateria de nobreak, bateria que não alimenta o sistema ou informação curta.</summary>
    public static Bateria? Interpretar(BateriaBruta bruta)
    {
        var b = bruta.Informacao;
        if (b is null || b.Length < TamanhoInformacao)
        {
            return null;
        }

        var capacidades = BinaryPrimitives.ReadUInt32LittleEndian(b);
        if ((capacidades & BateriaDoSistema) == 0 || (capacidades & CurtoPrazo) != 0)
        {
            return null;
        }

        const FonteDado f = FonteDado.Windows;
        var relativa = (capacidades & CapacidadeRelativa) != 0;
        var projeto = Capacidade(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(12)));
        var atual = Capacidade(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(16)));
        var ciclos = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(32));

        return new Bateria(
            Campo.Texto(bruta.Nome, f),
            Campo.Texto(bruta.Fabricante, f),
            Quimica(b.AsSpan(8, 4)),
            CampoCapacidade(projeto, relativa),
            CampoCapacidade(atual, relativa),
            projeto is null || atual is null
                ? Campo<double>.NaoInformado(f, "a bateria não informa a capacidade")
                : Campo<double>.Lido(Math.Max(0, Math.Round((1 - ((double)atual.Value / projeto.Value)) * 100, 1)), f, relativa ? "pela razão entre as capacidades relativas" : null),
            ciclos == 0 || ciclos > int.MaxValue ? Campo<int>.NaoInformado(f, "a bateria não informa") : Campo<int>.Lido((int)ciclos, f));
    }

    private static long? Capacidade(uint valor) => valor is 0 or CapacidadeDesconhecida ? null : valor;

    private static Campo<long> CampoCapacidade(long? valor, bool relativa) =>
        relativa ? Campo<long>.NaoInformado(FonteDado.Windows, "a bateria informa capacidade relativa, sem mWh")
        : valor is null ? Campo<long>.NaoInformado(FonteDado.Windows, "a bateria não informa a capacidade")
        : Campo<long>.Lido(valor.Value, FonteDado.Windows);

    private static Campo<string> Quimica(ReadOnlySpan<byte> bytes)
    {
        var texto = new StringBuilder();
        foreach (var c in bytes)
        {
            if (c == 0)
            {
                break;
            }

            texto.Append(c is >= 32 and < 127 ? (char)c : '.');
        }

        var abreviacao = texto.ToString().Trim();
        return abreviacao.Length == 0 ? Campo<string>.NaoInformado(FonteDado.Windows)
            : Quimicas.TryGetValue(abreviacao, out var nome) ? Campo<string>.Lido(nome, FonteDado.Windows, abreviacao)
            : Campo<string>.Lido(abreviacao, FonteDado.Windows, "abreviação fora da tabela da Microsoft");
    }
}
