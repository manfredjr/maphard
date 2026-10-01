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
          maphard coletar --dias 90            conta os eventos de estabilidade dos últimos 90 dias (padrão: 30)
          maphard --demonstracao               abre a janela com dados fictícios
          maphard --elevado                    abre a janela já como administrador (usado pelo botão "Ler como administrador")
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

    /// <summary>A janela foi reaberta pelo botão "Ler como administrador". O pedido de elevação é do Windows; o argumento só marca a origem.</summary>
    public bool Elevado { get; private set; }

    /// <summary>Período dos eventos de estabilidade: 30 (padrão) ou 90 dias, como pede o R27.</summary>
    public int Dias { get; private set; } = 30;

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
                case "--elevado":
                    a.Elevado = true;
                    break;
                case "--dias":
                    if (i + 1 < args.Count && int.TryParse(args[i + 1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var dias) && dias is 30 or 90)
                    {
                        a.Dias = dias;
                        i++;
                    }
                    else
                    {
                        a.Erros.Add("Use --dias 30 ou --dias 90.");
                    }

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

        if (a.Elevado && (a.Comando != ComandoCli.Janela || a.Demonstracao))
        {
            a.Erros.Add("--elevado só vale para abrir a janela com a leitura da máquina.");
        }

        return a;
    }
}
