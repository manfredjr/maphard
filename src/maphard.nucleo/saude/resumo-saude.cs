using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Painel;

namespace MapHard.Nucleo.Saude;

/// <summary>Um cartão do Resumo (R1): a área, a seção que o clique abre, o estado, a frase curta e todos os motivos.</summary>
public sealed record CartaoSaude(string Area, string Secao, EstadoSaude Estado, string Frase, IReadOnlyList<string> Motivos);

/// <summary>
/// Junta as regras que as seções já usam num cartão por área, na ordem do R1. O Resumo não lê nada: assim o
/// cartão e a seção nunca discordam. No desktop sem bateria, o cartão Bateria não aparece (decisão do Manfred em
/// 02/10/2026).
/// </summary>
public static class ResumoSaude
{
    public const string SemProblema = "nenhum problema encontrado";

    public static IReadOnlyList<CartaoSaude> Montar(ColetaMaquina c)
    {
        var cartoes = new List<CartaoSaude>
        {
            Cartao("Discos", MontadorSecoes.Discos, RegrasDisco.Conjunto(c.Discos)),
            Cartao("Memória", MontadorSecoes.Memoria, RegrasEstabilidade.Memoria(c.Memoria, c.Estabilidade)),
            Cartao("Processador", MontadorSecoes.Processador, RegrasProcessador.Processador(c.Processador)),
            Cartao("Estabilidade", MontadorSecoes.Estabilidade, RegrasEstabilidade.Estabilidade(c.Estabilidade)),
            Cartao("Dispositivos", MontadorSecoes.Dispositivos, RegrasEstabilidade.Dispositivos(c.Dispositivos)),
            Cartao("Windows 11", MontadorSecoes.Windows, RegrasWindows.Windows11(c.Windows11)),
        };

        if (c.Bateria is not { FoiLido: true, Valor.Count: 0 })
        {
            cartoes.Add(Cartao("Bateria", MontadorSecoes.Bateria, RegrasWindows.Bateria(c.Bateria)));
        }

        return cartoes;
    }

    /// <summary>Sem motivo, "nenhum problema encontrado"; com um, o próprio; com mais, "o primeiro, e mais N".</summary>
    public static string Frase(SaudeArea s) => s.Motivos.Count switch
    {
        0 => SemProblema,
        1 => s.Motivos[0],
        _ => $"{s.Motivos[0]}, e mais {s.Motivos.Count - 1}",
    };

    private static CartaoSaude Cartao(string area, string secao, SaudeArea s) => new(area, secao, s.Estado, Frase(s), s.Motivos);
}
