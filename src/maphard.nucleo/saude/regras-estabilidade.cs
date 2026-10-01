using MapHard.Nucleo.Dispositivos;
using MapHard.Nucleo.Eventos;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Memoria;

namespace MapHard.Nucleo.Saude;

/// <summary>Estado de uma área do Resumo, com os motivos escritos (Ruim primeiro).</summary>
public sealed record SaudeArea(EstadoSaude Estado, IReadOnlyList<string> Motivos);

/// <summary>
/// Linhas da seção 8 do desenho para Estabilidade, Memória e Dispositivos, com os valores iniciais que o Manfred
/// pode ajustar. O erro de memória registrado pelo WHEA ainda não entra no cartão Memória: falta fonte para saber
/// o componente do evento (pendência da fatia 4).
/// </summary>
public static class RegrasEstabilidade
{
    public const int TelasAzuisRuim = 3;

    public static SaudeArea Estabilidade(SecaoEstabilidade s)
    {
        if (!s.Grupos.FoiLido)
        {
            return new SaudeArea(EstadoSaude.Desconhecido, [s.Grupos.Motivo ?? "log Sistema não lido"]);
        }

        int Quantos(string codigo) => s.Grupos.Valor!.FirstOrDefault(g => g.Codigo == codigo)?.Quantidade ?? 0;
        var periodo = $"nos últimos {s.Dias} dias";
        var telas = Quantos(Eventos.Estabilidade.TelasAzuis);
        var desligamentos = Quantos(Eventos.Estabilidade.Desligamentos);
        var naoCorrigidos = Quantos(Eventos.Estabilidade.WheaNaoCorrigido);

        var ruim = new List<string>();
        var atencao = new List<string>();
        if (telas > 0)
        {
            var texto = $"{Formatador.Plural(telas, "tela azul", "telas azuis")} {periodo}";
            if (telas >= TelasAzuisRuim)
            {
                ruim.Add(texto);
            }
            else
            {
                atencao.Add(texto);
            }
        }

        if (naoCorrigidos > 0)
        {
            ruim.Add($"{Formatador.Plural(naoCorrigidos, "erro de hardware não corrigido", "erros de hardware não corrigidos")} {periodo}");
        }

        if (desligamentos > 0)
        {
            atencao.Add($"{Formatador.Plural(desligamentos, "desligamento inesperado", "desligamentos inesperados")} {periodo}");
        }

        return Montar(ruim, atencao);
    }

    public static SaudeArea Memoria(SecaoMemoria memoria, SecaoEstabilidade estabilidade)
    {
        var ruim = estabilidade.DiagnosticoMemoriaComErro == true ? new List<string> { "o Diagnóstico de Memória do Windows encontrou erro" } : [];
        var atencao = memoria.Alertas.Select(a => a.Texto).ToList();
        return Montar(ruim, atencao);
    }

    public static SaudeArea Dispositivos(SecaoDispositivos d)
    {
        if (!d.ComProblema.FoiLido)
        {
            return new SaudeArea(EstadoSaude.Desconhecido, [d.ComProblema.Motivo ?? "dispositivos não lidos"]);
        }

        var n = d.ComProblema.Valor!.Count;
        return n == 0
            ? new SaudeArea(EstadoSaude.Bom, [])
            : new SaudeArea(EstadoSaude.Atencao, [$"{Formatador.Plural(n, "dispositivo com problema", "dispositivos com problema")}"]);
    }

    private static SaudeArea Montar(List<string> ruim, List<string> atencao) =>
        ruim.Count > 0
            ? new SaudeArea(EstadoSaude.Ruim, [.. ruim, .. atencao])
            : atencao.Count > 0 ? new SaudeArea(EstadoSaude.Atencao, atencao) : new SaudeArea(EstadoSaude.Bom, []);
}
