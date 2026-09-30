using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Relatorios;

namespace MapHard.Nucleo.LinhaDeComando;

/// <summary>A linha de comando sem console: recebe a coleta e onde escrever, para ser testada.</summary>
public static class ExecutorCli
{
    public const int CodigoSucesso = 0;
    public const int CodigoArgumentos = 1;
    public const int CodigoGravacao = 2;

    public static async Task<int> ExecutarAsync(
        ArgumentosCli argumentos,
        Func<CancellationToken, Task<ColetaMaquina>> coletar,
        string pastaAtual,
        TextWriter saida,
        TextWriter erro,
        CancellationToken cancelar = default)
    {
        if (!argumentos.Valido)
        {
            foreach (var e in argumentos.Erros)
            {
                await erro.WriteLineAsync(e).ConfigureAwait(false);
            }

            await erro.WriteLineAsync("Use --ajuda para ver as opções.").ConfigureAwait(false);
            return CodigoArgumentos;
        }

        switch (argumentos.Comando)
        {
            case ComandoCli.Versao:
                await saida.WriteLineAsync($"MapHard - MT {Coletor.VersaoPrograma}").ConfigureAwait(false);
                return CodigoSucesso;
            case ComandoCli.Ajuda or ComandoCli.Janela:
                await saida.WriteLineAsync(ArgumentosCli.TextoAjuda).ConfigureAwait(false);
                return CodigoSucesso;
        }

        await saida.WriteLineAsync("Coletando...").ConfigureAwait(false);
        var coleta = await coletar(cancelar).ConfigureAwait(false);
        foreach (var linha in Resumo(coleta))
        {
            await saida.WriteLineAsync(linha).ConfigureAwait(false);
        }

        if (!argumentos.GravarJson)
        {
            return CodigoSucesso;
        }

        var nome = argumentos.ArquivoJson ?? ExportadorJson.NomePadrao(coleta.Identificacao.Computador.Valor ?? "computador", coleta.ColetadoEm);
        var caminho = Path.GetFullPath(Path.Combine(pastaAtual, nome));
        try
        {
            ExportadorJson.Gravar(coleta, caminho);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            await erro.WriteLineAsync($"Não foi possível gravar {caminho}: {e.Message}").ConfigureAwait(false);
            return CodigoGravacao;
        }

        await saida.WriteLineAsync($"Coleta gravada em {caminho}").ConfigureAwait(false);
        return CodigoSucesso;
    }

    /// <summary>O resumo que a linha de comando mostra, com o texto de cada estado quando o campo não foi lido.</summary>
    public static IEnumerable<string> Resumo(ColetaMaquina c)
    {
        var id = c.Identificacao;
        var p = c.Processador;
        yield return $"Computador:  {Texto(id.Computador)}";
        yield return $"Equipamento: {Texto(id.Fabricante)} {Texto(id.Modelo)}";
        yield return $"Série:       {Texto(id.NumeroSerie)}";
        yield return $"Windows:     {Texto(id.Windows.Nome)} {Texto(id.Windows.Versao)} ({Texto(id.Windows.Compilacao)})";
        yield return $"Processador: {Texto(p.Nome)}";
        yield return $"Núcleos:     {Texto(p.Nucleos)} núcleos, {Texto(p.Threads)} threads";
        yield return $"Clock:       base {Texto(p.Clocks.Base, v => Formatador.Mhz(v))}, máximo {Texto(p.Clocks.Maximo, v => Formatador.Mhz(v))}";
        yield return $"Memória:     {Texto(id.MemoriaUtilizavel, Formatador.Bytes)} utilizáveis";
        yield return $"Placa-mãe:   {Texto(c.Placa.PlacaFabricante)} {Texto(c.Placa.PlacaModelo)}";
        yield return $"BIOS:        {Texto(c.Placa.BiosVersao)} de {Texto(c.Placa.BiosData, Formatador.Data)}, {Texto(c.Placa.Firmware.Modo)}";
        yield return $"Coletado em: {Formatador.DataHora(c.ColetadoEm)}{(c.Administrador ? " como administrador" : string.Empty)}";
    }

    private static string Texto<T>(Campo<T> campo, Func<T, string>? formatar = null) =>
        campo.FoiLido ? formatar?.Invoke(campo.Valor!) ?? Convert.ToString(campo.Valor, Formatador.PtBr)! : $"[{TextosEstado.Texto(campo.Estado)}]";
}
