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

    /// <summary>
    /// Reabre o próprio .exe com o verbo "runas", que faz o Windows pedir a elevação, e o argumento --elevado.
    /// Devolve false quando o técnico recusa (ERROR_CANCELLED, 1223, WinError.h) ou o Windows não abre o programa.
    /// </summary>
    public static bool ReabrirComoAdministrador()
    {
        const int cancelado = 1223;
        if (Environment.ProcessPath is not { } exe)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(exe, "--elevado") { UseShellExecute = true, Verb = "runas" });
            return true;
        }
        catch (System.ComponentModel.Win32Exception erro) when (erro.NativeErrorCode == cancelado)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception erro)
        {
            MessageBox.Show($"Não foi possível abrir o MapHard como administrador:\n{erro.Message}", "MapHard - MT",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
