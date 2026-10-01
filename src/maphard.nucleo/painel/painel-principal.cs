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
    private readonly Func<CancellationToken, Task<ColetaMaquina>> _coletar;
    private readonly Func<DateOnly> _hoje;

    public PainelPrincipal(Func<CancellationToken, Task<ColetaMaquina>> coletar, bool demonstracao = false, Func<DateOnly>? hoje = null)
    {
        _coletar = coletar;
        _hoje = hoje ?? (() => DateOnly.FromDateTime(DateTime.Now));
        Demonstracao = demonstracao;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Painel da máquina real.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static PainelPrincipal Padrao()
    {
        var coletor = new Coletor(FontesColeta.Windows(), TimeSpan.FromSeconds(10));
        return new PainelPrincipal(cancelar => coletor.ColetarAsync(cancelar: cancelar));
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

    public async Task AtualizarAsync(CancellationToken cancelar = default)
    {
        Estado = EstadoPainel.Coletando;
        Falha = null;
        Aviso = null;
        Avisar();
        try
        {
            Coleta = await _coletar(cancelar).ConfigureAwait(true);
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

    /// <summary>Nome sugerido para salvar o JSON.</summary>
    public string NomeSugeridoJson() => Coleta is null
        ? "maphard.json"
        : ExportadorJson.NomePadrao(Coleta.Identificacao.Computador.Valor ?? "computador", Coleta.ColetadoEm);

    public void SalvarJson(string caminho)
    {
        if (Coleta is null)
        {
            throw new InvalidOperationException("Ainda não há coleta para salvar.");
        }

        ExportadorJson.Gravar(Coleta, caminho);
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
