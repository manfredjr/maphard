using System.ComponentModel;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Relatorios;

namespace MapHard.Nucleo.Painel;

public enum EstadoPainel
{
    Coletando,
    Pronto,
    Falhou,
}

/// <summary>Estado e comandos da janela principal, sem nenhum tipo do WPF.</summary>
public sealed class PainelPrincipal : INotifyPropertyChanged
{
    private readonly Func<int, CancellationToken, Task<ColetaMaquina>> _coletar;
    private readonly Func<DateOnly> _hoje;

    public PainelPrincipal(Func<CancellationToken, Task<ColetaMaquina>> coletar, bool demonstracao = false, Func<DateOnly>? hoje = null)
        : this((_, cancelar) => coletar(cancelar), demonstracao, hoje)
    {
    }

    /// <summary>Painel cuja coleta recebe o período dos eventos de estabilidade, em dias.</summary>
    public PainelPrincipal(Func<int, CancellationToken, Task<ColetaMaquina>> coletarComDias, bool demonstracao = false, Func<DateOnly>? hoje = null, int dias = 30)
    {
        _coletar = coletarComDias;
        _hoje = hoje ?? (() => DateOnly.FromDateTime(DateTime.Now));
        Demonstracao = demonstracao;
        DiasEventos = dias;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Painel da máquina real.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static PainelPrincipal Padrao(int dias = 30) =>
        new((d, cancelar) => new Coletor(FontesColeta.Windows(), TimeSpan.FromSeconds(10), diasEventos: d).ColetarAsync(cancelar: cancelar), dias: dias);

    /// <summary>Períodos de eventos que o técnico escolhe na janela (R27).</summary>
    public static IReadOnlyList<int> OpcoesDias { get; } = [30, 90];

    /// <summary>Período dos eventos de estabilidade da coleta atual.</summary>
    public int DiasEventos { get; private set; }

    /// <summary>Troca o período e coleta de novo.</summary>
    public async Task AlterarDiasAsync(int dias, CancellationToken cancelar = default)
    {
        if (dias == DiasEventos || !OpcoesDias.Contains(dias))
        {
            return;
        }

        DiasEventos = dias;
        await AtualizarAsync(cancelar).ConfigureAwait(true);
    }

    /// <summary>Painel com a máquina fictícia, para imagem de tela. Não lê nada do computador.</summary>
    public static PainelPrincipal ComDemonstracao() => new(_ => Task.FromResult(DadosDemonstracao.Coleta()), demonstracao: true);

    public bool Demonstracao { get; }

    public EstadoPainel Estado { get; private set; } = EstadoPainel.Coletando;

    public ColetaMaquina? Coleta { get; private set; }

    public IReadOnlyList<SecaoTela> Secoes { get; private set; } = [];

    public string? Falha { get; private set; }

    /// <summary>Texto da barra de status.</summary>
    public string TextoStatus => Estado switch
    {
        EstadoPainel.Coletando => "coletando...",
        EstadoPainel.Falhou => $"a coleta falhou: {Falha}",
        _ => string.Join("   |   ", PartesStatus()),
    };

    public bool PodeSalvar => Estado == EstadoPainel.Pronto;

    public bool Coletando => Estado == EstadoPainel.Coletando;

    public bool PodeAtualizar => !Coletando && !Demonstracao;

    /// <summary>O botão "Ler como administrador" aparece depois de uma coleta como usuário comum.</summary>
    public bool PodeElevar => !Demonstracao && !Coletando && Coleta is { Administrador: false };

    /// <summary>Aviso da última ação da janela, na barra de status.</summary>
    public string? Aviso { get; private set; }

    /// <summary>O técnico recusou o pedido de elevação do Windows: a janela atual continua, com o aviso.</summary>
    public void AvisarElevacaoCancelada()
    {
        Aviso = "leitura como administrador cancelada";
        Avisar();
    }

    /// <summary>A seção foi copiada: a barra de status confirma.</summary>
    public void AvisarCopia(string secao)
    {
        Aviso = $"seção {secao} copiada";
        Avisar();
    }

    public async Task AtualizarAsync(CancellationToken cancelar = default)
    {
        Estado = EstadoPainel.Coletando;
        Falha = null;
        Aviso = null;
        Avisar();
        try
        {
            Coleta = await _coletar(DiasEventos, cancelar).ConfigureAwait(true);
            Secoes = MontadorSecoes.Montar(Coleta, _hoje());
            Estado = EstadoPainel.Pronto;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            Falha = erro.Message;
            Estado = EstadoPainel.Falhou;
        }

        Avisar();
    }

    /// <summary>Nome sugerido para salvar a coleta no formato pedido.</summary>
    public string NomeSugerido(FormatoRelatorio formato) => Coleta is null
        ? $"maphard{Relatorios.Relatorios.Extensao(formato)}"
        : Relatorios.Relatorios.NomePadrao(Coleta, formato);

    /// <summary>Grava a coleta no formato da extensão: HTML, JSON ou CSV.</summary>
    public void SalvarRelatorio(string caminho)
    {
        if (Coleta is null)
        {
            throw new InvalidOperationException("Ainda não há coleta para salvar.");
        }

        Relatorios.Relatorios.Gravar(Coleta, caminho, _hoje());
    }

    private IEnumerable<string> PartesStatus()
    {
        var c = Coleta!;
        yield return c.Identificacao.Computador.Valor ?? "computador";
        yield return c.Administrador ? "administrador" : "usuário comum";
        yield return $"coletado em {Formatador.DataHora(c.ColetadoEm)}";
        if (Demonstracao)
        {
            yield return "demonstração: dados fictícios";
        }

        if (Aviso is not null)
        {
            yield return Aviso;
        }
    }

    private void Avisar() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
