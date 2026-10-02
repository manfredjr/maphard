using System.Globalization;
using System.Text;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Painel;
using MapHard.Nucleo.Saude;

namespace MapHard.Nucleo.Relatorios;

/// <summary>
/// CSV de uma linha por máquina (R40), para juntar várias coletas numa planilha: ponto e vírgula, UTF-8 com BOM
/// e aspas quando o campo pede, no padrão do MapDisk. Campo não lido sai em branco, nunca com zero nem com o
/// texto do estado, para a planilha poder somar a coluna. Um arquivo por máquina (decisão do Manfred em 02/10/2026).
/// </summary>
public static class ExportadorCsv
{
    public const string Cabecalho =
        "Computador;Coletado em;Fabricante;Modelo;Número de série;Processador;Núcleos;Threads;Memória instalada (GB);Tipo de memória;Discos;"
        + "Windows;Versão;Compilação;Arquitetura;Ativação;Windows 11;Saúde dos discos;Saúde da memória;Saúde do processador;Estabilidade;Dispositivos;"
        + "Bateria (desgaste %);Placa-mãe;BIOS;Data da BIOS;Administrador";

    public static string NomePadrao(ColetaMaquina c) =>
        Path.ChangeExtension(ExportadorJson.NomePadrao(c.Identificacao.Computador.Valor ?? "computador", c.ColetadoEm), ".csv");

    public static void Gravar(ColetaMaquina c, string caminho) =>
        File.WriteAllText(caminho, Gerar(c), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    public static string Gerar(ColetaMaquina c) => $"{Cabecalho}\r\n{Linha(c)}\r\n";

    public static string Linha(ColetaMaquina c)
    {
        var id = c.Identificacao;
        var w = id.Windows;
        var saude = ResumoSaude.Montar(c).ToDictionary(k => k.Area, k => MontadorSecoes.NomeSaude(k.Estado));
        var bateria = c.Bateria.Valor?.FirstOrDefault()?.Desgaste;
        var placa = string.Join(' ', new[] { c.Placa.PlacaFabricante, c.Placa.PlacaModelo }.Where(p => p.FoiLido).Select(p => p.Valor));

        string[] campos =
        [
            T(id.Computador),
            Formatador.DataHora(c.ColetadoEm),
            T(id.Fabricante),
            T(id.Modelo),
            T(id.NumeroSerie),
            T(id.Processador),
            N(c.Processador.Nucleos),
            N(c.Processador.Threads),
            id.MemoriaInstalada.FoiLido ? (id.MemoriaInstalada.Valor / (double)(1L << 30)).ToString("0.##", Formatador.PtBr) : string.Empty,
            T(id.MemoriaTipo),
            T(id.Discos),
            T(w.Nome),
            T(w.Versao),
            T(w.Compilacao),
            T(w.Arquitetura),
            T(w.Ativacao),
            RegrasWindows.TextoWindows11(c.Windows11),
            saude["Discos"],
            saude["Memória"],
            saude["Processador"],
            saude["Estabilidade"],
            saude["Dispositivos"],
            bateria is { FoiLido: true } d ? d.Valor.ToString("0.#", Formatador.PtBr) : string.Empty,
            placa,
            T(c.Placa.BiosVersao),
            c.Placa.BiosData.FoiLido ? Formatador.Data(c.Placa.BiosData.Valor) : string.Empty,
            c.Administrador ? "sim" : "não",
        ];
        return string.Join(';', campos.Select(Campo));
    }

    private static string T(Campo<string> campo) => campo.FoiLido ? campo.Valor! : string.Empty;

    private static string N(Campo<int> campo) => campo.FoiLido ? campo.Valor.ToString(CultureInfo.InvariantCulture) : string.Empty;

    private static string Campo(string texto) => texto.IndexOfAny([';', '"', '\n', '\r']) >= 0
        ? "\"" + texto.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
        : texto;
}
