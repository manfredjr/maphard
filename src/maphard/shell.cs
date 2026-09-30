using System.Diagnostics;
using System.Windows;

namespace MapHard;

internal static class Shell
{
    /// <summary>
    /// Abre o endereço no navegador padrão, só quando o técnico clica. O programa em si não manda nada
    /// para a internet. Se o Windows não tiver navegador, o endereço vai para a área de transferência.
    /// </summary>
    public static void AbrirNoNavegador(string endereco)
    {
        try
        {
            Process.Start(new ProcessStartInfo(endereco) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Clipboard.SetText(endereco);
            MessageBox.Show($"Não foi possível abrir o navegador. O endereço foi copiado:\n{endereco}", "MapHard - MT",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
