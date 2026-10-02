using MapHard.Nucleo.Coleta;

namespace MapHard.Nucleo.Saude;

/// <summary>
/// Linha Processador da seção 8 do desenho. Atenção: virtualização desligada no firmware com o Hyper-V ou o WSL
/// instalado. O Ruim por erro de processador no WHEA fica fora até o MapHard separar o componente do erro
/// (pendência da fatia 4, decisão do Manfred em 02/10/2026).
/// </summary>
public static class RegrasProcessador
{
    public const string MotivoVirtualizacao = "virtualização desligada no firmware, e o Hyper-V ou o WSL está instalado: ligar no firmware";

    public static SaudeArea Processador(SecaoProcessador p)
    {
        if (!p.Nome.FoiLido)
        {
            return new SaudeArea(EstadoSaude.Desconhecido, [p.Nome.Motivo ?? "processador não lido"]);
        }

        var desligada = p.VirtualizacaoNoProcessador is { FoiLido: true, Valor: true } && p.VirtualizacaoLigada is { FoiLido: true, Valor: false };
        return desligada && p.HyperVPedido is { FoiLido: true, Valor: true }
            ? new SaudeArea(EstadoSaude.Atencao, [MotivoVirtualizacao])
            : new SaudeArea(EstadoSaude.Bom, []);
    }
}
