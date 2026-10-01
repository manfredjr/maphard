using System.Text.RegularExpressions;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Formatacao;

namespace MapHard.Nucleo.Memoria;

/// <summary>
/// Alertas do R13 e a resposta de ampliação do R14, com os valores iniciais do plano da fatia 2.
/// A regra do canal é heurística: só usa padrões de slot conhecidos, e o texto diz "provável".
/// </summary>
public static partial class AlertasMemoria
{
    public const string ModulosDiferentes = "modulos-diferentes";
    public const string AbaixoDaVelocidade = "abaixo-da-velocidade";
    public const string CanalUnico = "canal-unico";
    public const string ReservaAlta = "reserva-alta";

    /// <summary>Reserva acima desta fração da instalada gera alerta.</summary>
    public const double LimiteReserva = 0.25;

    public static IReadOnlyList<AlertaMemoria> Calcular(SecaoMemoria s)
    {
        var alertas = new List<AlertaMemoria>();
        var instalados = s.Modulos.Valor?.Where(m => !m.Vazio).ToList() ?? [];

        if (Diferenca(instalados) is { } diferenca)
        {
            alertas.Add(new AlertaMemoria(ModulosDiferentes, $"módulos diferentes: {diferenca}"));
        }

        var configuradas = instalados.Select(m => m.VelocidadeConfigurada).Where(c => c.FoiLido).Select(c => c.Valor).ToList();
        var nominais = instalados.Select(m => m.VelocidadeNominal).Where(c => c.FoiLido).Select(c => c.Valor).ToList();
        if (configuradas.Count > 0 && nominais.Count > 0 && configuradas.Min() < nominais.Min())
        {
            alertas.Add(new AlertaMemoria(AbaixoDaVelocidade,
                $"rodando a {configuradas.Min()} MT/s; os módulos aceitam {nominais.Min()}. Pode ser limite do processador ou da placa"));
        }

        if (CanalUnicoProvavel(instalados, s.SlotsTotal))
        {
            alertas.Add(new AlertaMemoria(CanalUnico, "provável canal único: o desempenho da memória cai"));
        }

        if (s.Instalada.FoiLido && s.Utilizavel.FoiLido && s.Reservada.FoiLido && s.Reservada.Valor > s.Instalada.Valor * LimiteReserva)
        {
            alertas.Add(new AlertaMemoria(ReservaAlta, $"o Windows usa {Formatador.Bytes(s.Utilizavel.Valor)} dos {Formatador.Bytes(s.Instalada.Valor)} instalados"));
        }

        return alertas;
    }

    /// <summary>"cabem até 64 GB (informado pelo firmware); 2 slots livres; tipo DDR4, formato SODIMM".</summary>
    public static Campo<string> Ampliacao(SecaoMemoria s)
    {
        if (!s.SlotsTotal.FoiLido || !s.SlotsOcupados.FoiLido)
        {
            return Campo<string>.NaoInformado(FonteDado.Smbios, "o firmware não informa os slots");
        }

        var partes = new List<string>();
        if (s.CapacidadeMaxima.FoiLido)
        {
            partes.Add($"cabem até {Formatador.Bytes(s.CapacidadeMaxima.Valor)} (informado pelo firmware)");
        }

        var livres = s.SlotsTotal.Valor - s.SlotsOcupados.Valor;
        partes.Add(livres > 0 ? Formatador.Plural(livres, "slot livre", "slots livres") : "sem slot livre: ampliar exige trocar módulos");

        var formatos = s.Modulos.Valor?.Where(m => !m.Vazio && m.Formato.FoiLido).Select(m => m.Formato.Valor!).Distinct().ToList() ?? [];
        var tipoFormato = string.Join(", ", new[]
        {
            s.Tipo.FoiLido ? $"tipo {s.Tipo.Valor}" : null,
            formatos.Count > 0 ? $"formato {string.Join(" e ", formatos)}" : null,
        }.OfType<string>());
        if (tipoFormato.Length > 0)
        {
            partes.Add(tipoFormato);
        }

        return Campo<string>.Lido(string.Join("; ", partes), FonteDado.Smbios);
    }

    /// <summary>
    /// A primeira diferença entre os módulos instalados: capacidade ou velocidade nominal. O part number
    /// não entra, por decisão do Manfred em 01/10/2026: com capacidade e velocidade iguais, part number
    /// diferente é comum em pentes de fábrica (muda só a revisão do chip).
    /// </summary>
    private static string? Diferenca(IReadOnlyList<ModuloTela> instalados)
    {
        if (instalados.Count < 2)
        {
            return null;
        }

        var tamanhos = Distintos(instalados.Select(m => m.Tamanho));
        if (tamanhos.Count > 1)
        {
            return string.Join(" e ", tamanhos.Select(Formatador.Bytes));
        }

        var velocidades = Distintos(instalados.Select(m => m.VelocidadeNominal));
        return velocidades.Count > 1 ? $"{string.Join(" e ", velocidades)} MT/s" : null;
    }

    private static List<T> Distintos<T>(IEnumerable<Campo<T>> campos) =>
        campos.Where(c => c.FoiLido).Select(c => c.Valor!).Distinct().ToList();

    /// <summary>
    /// Um módulo só numa placa com dois ou mais slots; ou dois ou mais módulos que os textos de slot
    /// põem no mesmo canal. Texto sem padrão conhecido não gera alerta.
    /// </summary>
    private static bool CanalUnicoProvavel(IReadOnlyList<ModuloTela> instalados, Campo<int> slotsTotal)
    {
        if (instalados.Count == 1)
        {
            return slotsTotal.FoiLido && slotsTotal.Valor >= 2;
        }

        if (instalados.Count < 2)
        {
            return false;
        }

        var canais = instalados.Select(Canal).ToList();
        return canais.All(c => c is not null) && canais.Distinct().Count() == 1;
    }

    /// <summary>O canal pelo texto do slot ou do banco: "ChannelA-DIMM0", "P0 CHANNEL A", "A1", "DIMM_A1".</summary>
    internal static char? Canal(ModuloTela m)
    {
        foreach (var texto in new[] { m.Slot.Valor, m.Banco.Valor })
        {
            if (texto is null)
            {
                continue;
            }

            var canal = PadraoChannel().Match(texto);
            if (canal.Success)
            {
                return char.ToUpperInvariant(canal.Groups[1].Value[0]);
            }

            var letraNumero = PadraoLetraNumero().Match(texto.Trim());
            if (letraNumero.Success)
            {
                return char.ToUpperInvariant(letraNumero.Groups[1].Value[0]);
            }
        }

        return null;
    }

    [GeneratedRegex(@"channel\s*-?\s*([a-h])(?![a-z])", RegexOptions.IgnoreCase)]
    private static partial Regex PadraoChannel();

    [GeneratedRegex(@"^(?:DIMM[_ ]?)?([A-H])[0-9]$", RegexOptions.IgnoreCase)]
    private static partial Regex PadraoLetraNumero();
}
