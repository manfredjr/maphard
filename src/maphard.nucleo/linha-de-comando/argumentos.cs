namespace MapHard.Nucleo.LinhaDeComando;

public enum ComandoCli
{
    Janela,
    Ajuda,
    Versao,
    Coletar,
}

/// <summary>Argumentos da linha de comando já interpretados. A linha de comando só lê a máquina e grava o arquivo pedido.</summary>
public sealed class ArgumentosCli
{
    public const string TextoAjuda = """
        MapHard - MT: o hardware deste computador

        Uso:
          maphard                              abre a janela
          maphard coletar                      mostra o resumo da máquina
          maphard coletar --json [arquivo]     grava a coleta completa em JSON
          maphard --demonstracao               abre a janela com dados fictícios
          maphard --ajuda                      mostra esta ajuda
          maphard --versao                     mostra a versão

        Sem nome depois de --json, o arquivo recebe o nome do computador e a
        data, na pasta atual. Exemplo: maphard-ESTACAO01-2026-09-30-1005.json

        Exemplos:
          maphard coletar --json
          maphard coletar --json estacao.json

        O MapHard só lê. Ele não altera nada no computador.
        """;

    public ComandoCli Comando { get; private set; } = ComandoCli.Janela;

    /// <summary>--json foi pedido. <see cref="ArquivoJson"/> nulo quer dizer nome padrão.</summary>
    public bool GravarJson { get; private set; }

    public string? ArquivoJson { get; private set; }

    public bool Demonstracao { get; private set; }

    public List<string> Erros { get; } = [];

    public bool Valido => Erros.Count == 0;

    public static ArgumentosCli Interpretar(IReadOnlyList<string> args)
    {
        var a = new ArgumentosCli();
        var comandos = 0;
        var jsonRepetido = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i].Trim();
            switch (arg.ToLowerInvariant())
            {
                case "--ajuda" or "-h" or "--help" or "-?" or "/?":
                    a.Comando = ComandoCli.Ajuda;
                    comandos++;
                    break;
                case "--versao" or "--version":
                    a.Comando = ComandoCli.Versao;
                    comandos++;
                    break;
                case "coletar":
                    a.Comando = ComandoCli.Coletar;
                    comandos++;
                    break;
                case "--demonstracao":
                    a.Demonstracao = true;
                    break;
                case "--json":
                    jsonRepetido |= a.GravarJson;
                    a.GravarJson = true;
                    if (i + 1 < args.Count && !args[i + 1].StartsWith('-') && !string.Equals(args[i + 1], "coletar", StringComparison.OrdinalIgnoreCase))
                    {
                        a.ArquivoJson = args[++i].Trim();
                    }

                    break;
                default:
                    a.Erros.Add(arg.StartsWith('-') ? $"Opção desconhecida: {arg}" : $"Argumento desconhecido: {arg}");
                    break;
            }
        }

        if (comandos > 1)
        {
            a.Erros.Add("Use só um comando por vez: coletar, --ajuda ou --versao.");
        }

        if (jsonRepetido)
        {
            a.Erros.Add("Use --json uma vez só.");
        }

        if (a.GravarJson && a.Comando != ComandoCli.Coletar)
        {
            a.Erros.Add("--json só vale com o comando coletar. Exemplo: maphard coletar --json");
        }

        if (a.Demonstracao && a.Comando != ComandoCli.Janela)
        {
            a.Erros.Add("--demonstracao só vale para abrir a janela.");
        }

        return a;
    }
}
