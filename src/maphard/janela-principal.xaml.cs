using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MapHard.Nucleo.Painel;
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
        Loaded += async (_, _) => await _painel.AtualizarAsync();
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

    private async void AoAtualizar(object sender, RoutedEventArgs e) => await _painel.AtualizarAsync();

    private void AoSalvarJson(object sender, RoutedEventArgs e)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Salvar a coleta em JSON",
            FileName = _painel.NomeSugeridoJson(),
            Filter = "Coleta do MapHard (*.json)|*.json",
            AddExtension = true,
            DefaultExt = ".json",
        };
        if (dialogo.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _painel.SalvarJson(dialogo.FileName);
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Não foi possível gravar o arquivo:\n{erro.Message}", "MapHard - MT", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AoAbrirSobre(object sender, RoutedEventArgs e) => new JanelaSobre { Owner = this }.ShowDialog();

    private void AoClicarLogo(object sender, RoutedEventArgs e) => Shell.AbrirNoNavegador(TextosSobre.SiteMt);
}
