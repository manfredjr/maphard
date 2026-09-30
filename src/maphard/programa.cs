using System.Windows;
using System.Windows.Threading;
using MapHard.Nucleo.LinhaDeComando;
using MapHard.Nucleo.Painel;

namespace MapHard;

internal static class Programa
{
    private static bool _erroMostrado;

    /// <summary>
    /// Sem argumentos abre a janela. Com argumentos roda a linha de comando, no mesmo .exe.
    /// O argumento --demonstracao abre a janela com a máquina fictícia, sem ler o computador.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        var argumentos = ArgumentosCli.Interpretar(args);
        if (args.Length > 0 && !(argumentos.Valido && argumentos.Comando == ComandoCli.Janela))
        {
            return ModoLinhaDeComando.Executar(argumentos);
        }

        var aplicativo = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        aplicativo.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/maphard;component/tema/tema-mt.xaml", UriKind.Absolute),
        });
        aplicativo.DispatcherUnhandledException += AoErroNaoTratado;

        var painel = argumentos.Demonstracao ? PainelPrincipal.ComDemonstracao() : PainelPrincipal.Padrao();
        return aplicativo.Run(new JanelaPrincipal(painel));
    }

    /// <summary>
    /// Erro que escapou da tela: mostra a mensagem uma vez e fecha o programa, como no MapDisk.
    /// </summary>
    private static void AoErroNaoTratado(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (_erroMostrado)
        {
            return;
        }

        _erroMostrado = true;
        MessageBox.Show(
            $"Aconteceu um erro inesperado: {e.Exception.Message}\n\nO MapHard vai fechar. Abra de novo e, se o erro voltar, avise a MT.",
            "MapHard - MT",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Application.Current.Shutdown(1);
    }
}
