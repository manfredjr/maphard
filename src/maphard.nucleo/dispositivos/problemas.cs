using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Tabelas;

namespace MapHard.Nucleo.Dispositivos;

/// <summary>Um dispositivo com problema (R30): nome, classe, código e o texto em português.</summary>
public sealed record DispositivoProblema(Campo<string> Nome, Campo<string> Classe, int Codigo, string Texto);

/// <summary>Seção Dispositivos. Os desativados (código 22) ficam à parte: costumam ser escolha de quem usa a máquina.</summary>
public sealed record SecaoDispositivos(
    Campo<IReadOnlyList<DispositivoProblema>> ComProblema,
    Campo<IReadOnlyList<DispositivoProblema>> Desativados,
    Campo<int> Total);

public static class Problemas
{
    public const int Desativado = 22;

    public static SecaoDispositivos Montar(IReadOnlyList<DispositivoBruto>? dispositivos, string? falha, ProblemasDispositivo textos)
    {
        if (dispositivos is null)
        {
            var erro = Campo<IReadOnlyList<DispositivoProblema>>.Erro(FonteDado.Windows, falha ?? "lista de dispositivos indisponível");
            return new SecaoDispositivos(erro, erro, Campo<int>.Erro(FonteDado.Windows, falha ?? "lista de dispositivos indisponível"));
        }

        var comCodigo = dispositivos
            .Where(d => d.CodigoProblema is > 0)
            .Select(d => new DispositivoProblema(
                Campo.Texto(d.Nome, FonteDado.Windows),
                Campo.Texto(d.Classe, FonteDado.Windows),
                (int)d.CodigoProblema!.Value,
                textos.Texto((int)d.CodigoProblema.Value)))
            .OrderBy(d => d.Nome.Valor, StringComparer.CurrentCulture)
            .ToList();

        return new SecaoDispositivos(
            Campo<IReadOnlyList<DispositivoProblema>>.Lido(comCodigo.Where(d => d.Codigo != Desativado).ToList(), FonteDado.Windows),
            Campo<IReadOnlyList<DispositivoProblema>>.Lido(comCodigo.Where(d => d.Codigo == Desativado).ToList(), FonteDado.Windows),
            Campo<int>.Lido(dispositivos.Count, FonteDado.Windows));
    }
}
