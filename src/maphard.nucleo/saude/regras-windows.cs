using MapHard.Nucleo.Baterias;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Windows11;

namespace MapHard.Nucleo.Saude;

/// <summary>
/// Linhas da seção 8 do desenho para Windows 11 e Bateria, com os valores iniciais que o Manfred pode ajustar.
/// Windows 11: requisito que não se resolve sem trocar peça é Ruim; o que se resolve no firmware é Atenção; o
/// processador fora da lista do MapHard deixa o cartão em Desconhecido, nunca em Ruim.
/// </summary>
public static class RegrasWindows
{
    public const double DesgasteAtencao = 30;
    public const double DesgasteRuim = 50;

    public static SaudeArea Windows11(VerificacaoWindows11 v)
    {
        // O nome do item só entra quando o detalhe ainda não começa por ele ("Secure Boot desligado: ...").
        IReadOnlyList<string> Detalhes(EstadoRequisito estado) => v.Itens.Where(i => i.Estado == estado)
            .Select(i => i.Detalhe.StartsWith(i.Item, StringComparison.OrdinalIgnoreCase) ? i.Detalhe : $"{i.Item}: {i.Detalhe}").ToList();
        var nao = Detalhes(EstadoRequisito.NaoAtende);
        var configuracao = Detalhes(EstadoRequisito.Configuracao);
        var desconhecido = Detalhes(EstadoRequisito.Desconhecido);
        return nao.Count > 0 ? new SaudeArea(EstadoSaude.Ruim, [.. nao, .. configuracao, .. desconhecido])
            : configuracao.Count > 0 ? new SaudeArea(EstadoSaude.Atencao, [.. configuracao, .. desconhecido])
            : desconhecido.Count > 0 ? new SaudeArea(EstadoSaude.Desconhecido, desconhecido)
            : new SaudeArea(EstadoSaude.Bom, []);
    }

    /// <summary>"aceita", "não aceita: ...", "aceita depois de ajustar o firmware: ..." ou "não confirmado: ...".</summary>
    public static string TextoWindows11(VerificacaoWindows11 v)
    {
        var s = Windows11(v);
        var motivos = string.Join("; ", s.Motivos);
        return s.Estado switch
        {
            EstadoSaude.Bom => "aceita",
            EstadoSaude.Ruim => $"não aceita: {motivos}",
            EstadoSaude.Atencao => $"aceita depois de ajustar o firmware: {motivos}",
            _ => $"não confirmado: {motivos}",
        };
    }

    /// <summary>A pior bateria manda. Sem bateria, Bom e sem motivo: a tela diz que não há bateria no equipamento.</summary>
    public static SaudeArea Bateria(Campo<IReadOnlyList<Bateria>> baterias)
    {
        if (!baterias.FoiLido)
        {
            return new SaudeArea(EstadoSaude.Desconhecido, [baterias.Motivo ?? "bateria não lida"]);
        }

        var ruim = new List<string>();
        var atencao = new List<string>();
        var desconhecido = new List<string>();
        foreach (var b in baterias.Valor!)
        {
            if (!b.Desgaste.FoiLido)
            {
                desconhecido.Add(b.Desgaste.Motivo ?? "desgaste não informado");
                continue;
            }

            var texto = $"desgaste de {Formatador.Porcentagem(b.Desgaste.Valor)}";
            if (b.Desgaste.Valor > DesgasteRuim)
            {
                ruim.Add(texto);
            }
            else if (b.Desgaste.Valor > DesgasteAtencao)
            {
                atencao.Add(texto);
            }
        }

        return ruim.Count > 0 ? new SaudeArea(EstadoSaude.Ruim, [.. ruim, .. atencao])
            : atencao.Count > 0 ? new SaudeArea(EstadoSaude.Atencao, atencao)
            : desconhecido.Count > 0 ? new SaudeArea(EstadoSaude.Desconhecido, desconhecido)
            : new SaudeArea(EstadoSaude.Bom, []);
    }
}
