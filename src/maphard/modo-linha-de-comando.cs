using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.LinhaDeComando;

namespace MapHard;

/// <summary>
/// Liga o .exe de janela ao console de quem o chamou e passa a vez ao ExecutorCli. Copiado do MapDisk.
/// No Prompt de Comando, use "start /wait" para o prompt esperar o fim. No PowerShell, termine com "| Out-Host".
/// </summary>
internal static partial class ModoLinhaDeComando
{
    private const int ConsoleDoPai = -1;
    private const int SaidaPadrao = -11;
    private const uint TipoDisco = 1;
    private const uint TipoPipe = 3;
    private const int CodigoFalha = 3;

    public static int Executar(ArgumentosCli argumentos)
    {
        LigarConsole();
        using var cancelar = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancelar.Cancel();
            Console.WriteLine();
            Console.WriteLine("Interrompendo...");
        };

        try
        {
            Console.WriteLine();
            var coletor = new Coletor(FontesColeta.Windows(), TimeSpan.FromSeconds(10));
            return ExecutorCli.ExecutarAsync(
                argumentos,
                token => coletor.ColetarAsync(cancelar: token),
                Environment.CurrentDirectory,
                Console.Out,
                Console.Error,
                cancelar.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Coleta interrompida.");
            return CodigoFalha;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Erro: {e.Message}");
            return CodigoFalha;
        }
        finally
        {
            Console.Out.Flush();
        }
    }

    private static void LigarConsole()
    {
        if (GetFileType(GetStdHandle(SaidaPadrao)) is TipoDisco or TipoPipe)
        {
            // Arquivo ou pipe: grava em UTF-8, para os acentos chegarem inteiros.
            var utf8 = new UTF8Encoding(false);
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
            return;
        }

        if (!AttachConsole(ConsoleDoPai))
        {
            AllocConsole();
        }

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // Console sem suporte à troca de página de código: segue com a padrão.
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetStdHandle(int qual);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetFileType(nint arquivo);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processo);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();
}
