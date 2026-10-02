using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MapHard.Nucleo.Painel;
using MapHard.Nucleo.Relatorios;
using MapHard.Nucleo.Sobre;
using Microsoft.Win32;

namespace MapHard;

/// <summary>Janela principal. A lógica fica no PainelPrincipal, no núcleo; aqui só a ligação com o WPF.</summary>
public partial class JanelaPrincipal : Window
{
    private readonly PainelPrincipal _painel;
    private string _secaoAberta = MontadorSecoes.Resumo;

    public JanelaPrincipal(PainelPrincipal painel)
    {
        InitializeComponent();
        _painel = painel;
        DataContext = painel;
        if (painel.Demonstracao)
        {
            Title += " (demonstração)";
        }

        _painel.PropertyChanged += AoMudarPainel;
        PeriodoEventos.SelectedItem = _painel.DiasEventos;
        Loaded += async (_, _) => await _painel.AtualizarAsync();
    }

    private async void AoEscolherPeriodo(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && PeriodoEventos.SelectedItem is int dias)
        {
            await _painel.AlterarDiasAsync(dias);
        }
    }

    /// <summary>
    /// Depois de cada coleta, a lista de seções é nova e perde a seleção. Volta para a seção que
    /// estava aberta, depois de a ligação do WPF trocar a lista.
    /// </summary>
    private void AoMudarPainel(object? sender, PropertyChangedEventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (Navegacao.SelectedItem is SecaoTela atual && _painel.Secoes.Contains(atual))
            {
                return;
            }

            Navegacao.SelectedItem = _painel.Secoes.FirstOrDefault(s => s.Id == _secaoAberta) ?? _painel.Secoes.FirstOrDefault();
        });

    private void AoEscolherSecao(object sender, SelectionChangedEventArgs e)
    {
        if (Navegacao.SelectedItem is SecaoTela secao)
        {
            _secaoAberta = secao.Id;
        }
    }

    /// <summary>Cartão do Resumo: o clique abre a seção da área (R1).</summary>
    private void AoClicarCartao(object sender, MouseButtonEventArgs e) => AbrirSecaoDoCartao(sender);

    private void AoTeclarCartao(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            AbrirSecaoDoCartao(sender);
            e.Handled = true;
        }
    }

    private void AbrirSecaoDoCartao(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is CartaoTela { Abre: { } id } && _painel.Secoes.FirstOrDefault(s => s.Id == id) is { } secao)
        {
            Navegacao.SelectedItem = secao;
        }
    }

    private async void AoAtualizar(object sender, RoutedEventArgs e) => await _painel.AtualizarAsync();

    /// <summary>Copia a seção aberta como texto (R37).</summary>
    private void AoCopiar(object sender, RoutedEventArgs e)
    {
        if (Navegacao.SelectedItem is SecaoTela secao && _painel.Coleta is { } coleta)
        {
            Clipboard.SetText(TextoSecao.Gerar(secao, coleta));
            _painel.AvisarCopia(secao.Titulo);
        }
    }

    /// <summary>Salva a coleta em HTML (para o cliente), JSON (coleta completa) ou CSV (planilha), pela extensão.</summary>
    private void AoSalvarRelatorio(object sender, RoutedEventArgs e)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Salvar relatório",
            FileName = _painel.NomeSugerido(FormatoRelatorio.Html),
            Filter = "Relatório para o cliente (*.html)|*.html|Coleta completa (*.json)|*.json|Planilha (*.csv)|*.csv",
            AddExtension = true,
            DefaultExt = ".html",
        };
        if (dialogo.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _painel.SalvarRelatorio(dialogo.FileName);
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Não foi possível gravar o arquivo:\n{erro.Message}", "MapHard - MT", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Com a elevação aceita, a janela nova assume e esta fecha. Recusada, esta continua com o aviso.</summary>
    private void AoLerComoAdministrador(object sender, RoutedEventArgs e)
    {
        if (Shell.ReabrirComoAdministrador())
        {
            Close();
        }
        else
        {
            _painel.AvisarElevacaoCancelada();
        }
    }

    private void AoAbrirSobre(object sender, RoutedEventArgs e) => new JanelaSobre { Owner = this }.ShowDialog();

    private void AoClicarLogo(object sender, RoutedEventArgs e) => Shell.AbrirNoNavegador(TextosSobre.SiteMt);
}
