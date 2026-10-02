using MapHard.Nucleo.Relatorios;

namespace MapHard.Nucleo.LinhaDeComando;

public enum ComandoCli
{
    Janela,
    Ajuda,
    Versao,
    Coletar,
}

/// <summary>Argumentos da linha de comando já interpretados. A linha de comando só lê a máquina e grava os arquivos pedidos.</summary>
public sealed class ArgumentosCli
{
    public const string TextoAjuda = """
        MapHard - MT: o hardware deste computador

        Uso:
          maphard                              abre a janela
          maphard coletar                      mostra o resumo da máquina
          maphard coletar --html [arquivo]     grava o relatório para o cliente em HTML
          maphard coletar --json [arquivo]     grava a coleta completa em JSON
          maphard coletar --csv [arquivo]      grava uma linha da máquina em CSV, para planilha
          maphard coletar --pasta <pasta>      grava na pasta, com o nome do computador e a data
          maphard coletar --dias 90            conta os eventos de estabilidade dos últimos 90 dias (padrão: 30)
          maphard --demonstracao               abre a janela com dados fictícios
          maphard --elevado                    abre a janela já como administrador (usado pelo botão "Ler como administrador")
          maphard --ajuda                      mostra esta ajuda
          maphard --versao                     mostra a versão

        Sem nome depois de --html, --json ou --csv, o arquivo recebe o nome do
        computador e a data, na pasta atual. Exemplo: maphard-ESTACAO01-2026-09-30-1005.json

        Com --pasta e sem formato, grava o JSON e o CSV. Com --html, --json ou
        --csv junto, grava só os pedidos, na pasta. Vários computadores podem
        gravar na mesma pasta de rede sem sobrescrever um ao outro.

        Exemplos:
          maphard coletar --html estacao.html
          maphard coletar --json estacao.json --csv estacao.csv
          maphard coletar --pasta \\servidor\inventario

        O MapHard só lê. Ele não altera nada no computador.
        """;

    private readonly Dictionary<FormatoRelatorio, string?> _formatos = [];

    public ComandoCli Comando { get; private set; } = ComandoCli.Janela;

    /// <summary>Formatos pedidos, com o nome do arquivo; nome nulo quer dizer nome padrão.</summary>
    public IReadOnlyDictionary<FormatoRelatorio, string?> Formatos => _formatos;

    /// <summary>--json foi pedido. <see cref="ArquivoJson"/> nulo quer dizer nome padrão.</summary>
    public bool GravarJson => _formatos.ContainsKey(FormatoRelatorio.Json);

    public string? ArquivoJson => _formatos.GetValueOrDefault(FormatoRelatorio.Json);

    /// <summary>Pasta de --pasta, onde os arquivos saem com o nome padrão.</summary>
    public string? Pasta { get; private set; }

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
        var repetidos = new HashSet<string>();

        bool Proximo(int i) => i + 1 < args.Count && !args[i + 1].StartsWith('-') && !string.Equals(args[i + 1], "coletar", StringComparison.OrdinalIgnoreCase);

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i].Trim();
            var opcao = arg.ToLowerInvariant();
            switch (opcao)
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
                case "--html" or "--json" or "--csv":
                    var formato = opcao switch
                    {
                        "--json" => FormatoRelatorio.Json,
                        "--csv" => FormatoRelatorio.Csv,
                        _ => FormatoRelatorio.Html,
                    };
                    if (!repetidos.Add(opcao))
                    {
                        a.Erros.Add($"Use {opcao} uma vez só.");
                    }

                    a._formatos[formato] = Proximo(i) ? args[++i].Trim() : null;
                    break;
                case "--pasta":
                    if (!repetidos.Add(opcao))
                    {
                        a.Erros.Add("Use --pasta uma vez só.");
                    }

                    if (Proximo(i))
                    {
                        a.Pasta = args[++i].Trim();
                    }
                    else
                    {
                        a.Erros.Add("Diga a pasta depois de --pasta. Exemplo: maphard coletar --pasta \\\\servidor\\inventario");
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

        if ((a._formatos.Count > 0 || a.Pasta is not null) && a.Comando != ComandoCli.Coletar)
        {
            a.Erros.Add("--html, --json, --csv e --pasta só valem com o comando coletar. Exemplo: maphard coletar --json");
        }

        if (a.Pasta is not null && a._formatos.Values.Any(nome => nome is not null))
        {
            a.Erros.Add("Com --pasta, o arquivo recebe o nome do computador e a data. Tire o nome depois de --html, --json ou --csv.");
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

    /// <summary>
    /// O que gravar: os formatos pedidos, ou JSON e CSV quando só a pasta foi dada (decisão do Manfred em 02/10/2026).
    /// </summary>
    public IReadOnlyList<(FormatoRelatorio Formato, string? Arquivo)> Gravacoes() =>
        _formatos.Count > 0
            ? _formatos.OrderBy(f => f.Key).Select(f => (f.Key, f.Value)).ToList()
            : Pasta is not null ? [(FormatoRelatorio.Json, null), (FormatoRelatorio.Csv, null)] : [];
}
