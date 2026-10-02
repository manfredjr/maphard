using MapHard.Nucleo.Baterias;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Dispositivos;
using MapHard.Nucleo.Eventos;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Saude;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Painel;
using MapHard.Nucleo.Rede;
using MapHard.Nucleo.Relatorios;
using MapHard.Nucleo.Video;

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

        var gravacoes = argumentos.Gravacoes();
        if (gravacoes.Count == 0)
        {
            return CodigoSucesso;
        }

        var pasta = Path.GetFullPath(Path.Combine(pastaAtual, argumentos.Pasta ?? string.Empty));
        if (argumentos.Pasta is not null && !Directory.Exists(pasta))
        {
            await erro.WriteLineAsync($"Não foi possível gravar em {pasta}: a pasta não existe ou não está acessível.").ConfigureAwait(false);
            return CodigoGravacao;
        }

        // Grava tudo ou nada: se um arquivo falhar, os que já foram gravados nesta vez saem.
        var hoje = DateOnly.FromDateTime(coleta.ColetadoEm.DateTime);
        var gravados = new List<string>();
        foreach (var (formato, arquivo) in gravacoes)
        {
            var nome = arquivo ?? Relatorios.Relatorios.NomePadrao(coleta, formato);
            var caminho = Path.GetFullPath(Path.Combine(pasta, Path.GetExtension(nome).Length == 0 ? nome + Relatorios.Relatorios.Extensao(formato) : nome));
            try
            {
                Relatorios.Relatorios.Gravar(coleta, caminho, hoje, formato);
                gravados.Add(caminho);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                foreach (var feito in gravados)
                {
                    File.Delete(feito);
                }

                await erro.WriteLineAsync($"Não foi possível gravar {caminho}: {e.Message}").ConfigureAwait(false);
                return CodigoGravacao;
            }
        }

        foreach (var caminho in gravados)
        {
            await saida.WriteLineAsync($"Coleta gravada em {caminho}").ConfigureAwait(false);
        }

        return CodigoSucesso;
    }

    /// <summary>O resumo que a linha de comando mostra, com o texto de cada estado quando o campo não foi lido.</summary>
    public static IEnumerable<string> Resumo(ColetaMaquina c)
    {
        var id = c.Identificacao;
        var p = c.Processador;
        yield return "Saúde";
        foreach (var cartao in ResumoSaude.Montar(c))
        {
            yield return LinhaSaude(cartao);
        }

        yield return string.Empty;
        yield return $"Computador:  {Texto(id.Computador)}";
        yield return $"Equipamento: {Texto(id.Fabricante)} {Texto(id.Modelo)}";
        yield return $"Série:       {Texto(id.NumeroSerie)}";
        yield return $"Windows:     {Texto(id.Windows.Nome)} {Texto(id.Windows.Versao)} ({Texto(id.Windows.Compilacao)}), {Texto(id.Windows.Arquitetura)}, {Texto(id.Windows.Ativacao)}";
        yield return $"Windows 11:  {RegrasWindows.TextoWindows11(c.Windows11)}";
        yield return $"Processador: {Texto(p.Nome)}";
        yield return $"Núcleos:     {Texto(p.Nucleos)} núcleos, {Texto(p.Threads)} threads";
        yield return $"Clock:       base {Texto(p.Clocks.Base, v => Formatador.Mhz(v))}, máximo {Texto(p.Clocks.Maximo, v => Formatador.Mhz(v))}";
        yield return $"Memória:     {LinhaMemoria(c.Memoria)} ({Texto(id.MemoriaUtilizavel, Formatador.Bytes)} utilizáveis)";
        if (!c.Discos.Discos.FoiLido)
        {
            yield return $"Discos:      {Texto(c.Discos.Discos)}";
        }

        foreach (var d in c.Discos.Discos.Valor ?? [])
        {
            yield return $"Disco {d.Numero}:     {LinhaDisco(d)}";
        }

        yield return $"Placa-mãe:   {Texto(c.Placa.PlacaFabricante)} {Texto(c.Placa.PlacaModelo)}, chipset {Texto(c.Placa.Chipset)}";
        yield return $"Estabilidade: {LinhaEstabilidade(c.Estabilidade)}";

        yield return $"Dispositivos: {LinhaDispositivos(c.Dispositivos)}";
        yield return $"Vídeo:       {LinhaVideo(c.Video)}";
        yield return $"Bateria:     {LinhaBateria(c.Bateria)}";
        yield return $"Rede:        {LinhaRede(c.Rede)}";
        yield return $"BIOS:        {Texto(c.Placa.BiosVersao)} de {Texto(c.Placa.BiosData, Formatador.Data)}, {Texto(c.Placa.Firmware.Modo)}";
        yield return $"Coletado em: {Formatador.DataHora(c.ColetadoEm)}{(c.Administrador ? " como administrador" : string.Empty)}";
    }

    /// <summary>
    /// "SSD NVMe 1,02 TB, Bom, 44 °C, 3% da vida usada". Sem SMART, a saúde sai Desconhecido com o motivo,
    /// e temperatura e vida usada só entram quando foram lidas.
    /// </summary>
    internal static string LinhaDisco(DiscoTela d)
    {
        var partes = new List<string>
        {
            d.Tamanho.FoiLido ? $"{Texto(d.Tipo)} {Formatador.BytesDecimais(d.Tamanho.Valor)}" : Texto(d.Tipo),
            d.Saude.Estado == EstadoSaude.Desconhecido && d.Saude.Motivos.Count > 0
                ? $"saúde {MontadorSecoes.NomeSaude(d.Saude.Estado)} ({d.Saude.Motivos[0]})"
                : MontadorSecoes.NomeSaude(d.Saude.Estado),
        };
        if (d.Temperatura.FoiLido)
        {
            partes.Add(Formatador.Temperatura(d.Temperatura.Valor));
        }

        if (d.VidaUsada.FoiLido)
        {
            partes.Add($"{d.VidaUsada.Valor}% da vida usada");
        }

        return string.Join(", ", partes);
    }

    /// <summary>"2 telas azuis, 1 desligamento inesperado em 30 dias; índice 6,2". Sem nada no período, "nenhum problema em 30 dias".</summary>
    internal static string LinhaEstabilidade(SecaoEstabilidade e)
    {
        var indice = e.IndiceEstabilidade.FoiLido ? $"; índice {e.IndiceEstabilidade.Valor.ToString("0.0", Formatador.PtBr)}" : string.Empty;
        if (!e.Grupos.FoiLido)
        {
            return $"{Texto(e.Grupos)}{indice}";
        }

        var partes = e.Grupos.Valor!.Where(g => g.Quantidade > 0).Select(g => g.Codigo switch
        {
            Estabilidade.TelasAzuis => Formatador.Plural(g.Quantidade, "tela azul", "telas azuis"),
            Estabilidade.Desligamentos => Formatador.Plural(g.Quantidade, "desligamento inesperado", "desligamentos inesperados"),
            Estabilidade.Disco => Formatador.Plural(g.Quantidade, "erro de disco", "erros de disco"),
            Estabilidade.SistemaArquivos => Formatador.Plural(g.Quantidade, "erro do sistema de arquivos", "erros do sistema de arquivos"),
            Estabilidade.WheaCorrigido => Formatador.Plural(g.Quantidade, "erro de hardware corrigido", "erros de hardware corrigidos"),
            Estabilidade.WheaNaoCorrigido => Formatador.Plural(g.Quantidade, "erro de hardware não corrigido", "erros de hardware não corrigidos"),
            _ => $"{Formatador.Numero(g.Quantidade)} {g.Titulo.ToLower(Formatador.PtBr)}",
        }).ToList();
        return partes.Count == 0
            ? $"nenhum problema em {e.Dias} dias{indice}"
            : $"{string.Join(", ", partes)} em {e.Dias} dias{indice}";
    }

    /// <summary>"  Discos:       Atenção (Disco 1: 3 setores realocados)" ou "  Bateria:      Bom".</summary>
    internal static string LinhaSaude(CartaoSaude c)
    {
        var estado = MontadorSecoes.NomeSaude(c.Estado);
        return $"  {c.Area + ":",-14}{(c.Estado == EstadoSaude.Bom ? estado : $"{estado} ({c.Frase})")}";
    }

    /// <summary>"Placa Exemplo, 4 GB; Vídeo Integrado Exemplo". A memória só entra quando é dedicada e foi lida.</summary>
    internal static string LinhaVideo(Campo<IReadOnlyList<PlacaVideo>> placas) =>
        !placas.FoiLido ? Texto(placas)
        : placas.Valor!.Count == 0 ? "nenhuma placa encontrada"
        : string.Join("; ", placas.Valor.Select(p => p.MemoriaDedicada.FoiLido ? $"{Texto(p.Nome)}, {Formatador.Bytes(p.MemoriaDedicada.Valor)}" : Texto(p.Nome)));

    /// <summary>"desgaste de 12%", com o aviso quando passa do limite; sem bateria, "não disponível neste equipamento".</summary>
    internal static string LinhaBateria(Campo<IReadOnlyList<Bateria>> baterias)
    {
        if (!baterias.FoiLido)
        {
            return Texto(baterias);
        }

        if (baterias.Valor!.Count == 0)
        {
            return LeitorBateria.SemBateria;
        }

        var saude = RegrasWindows.Bateria(baterias);
        var desgaste = string.Join("; ", baterias.Valor.Select(b => b.Desgaste.FoiLido ? $"desgaste de {Formatador.Porcentagem(b.Desgaste.Valor)}" : $"desgaste {Texto(b.Desgaste)}"));
        return saude.Estado is EstadoSaude.Atencao or EstadoSaude.Ruim ? $"{desgaste} ({MontadorSecoes.NomeSaude(saude.Estado)})" : desgaste;
    }

    /// <summary>"Ethernet desconectada; Wi-Fi 721 Mb/s".</summary>
    internal static string LinhaRede(Campo<IReadOnlyList<PlacaRede>> rede) =>
        !rede.FoiLido ? Texto(rede)
        : rede.Valor!.Count == 0 ? "nenhuma placa física"
        : string.Join("; ", rede.Valor.Select(p => $"{Texto(p.Nome)} {(p.VelocidadeBps.FoiLido ? LeitorRede.TextoVelocidade(p.VelocidadeBps.Valor) : p.Conectada.Valor ? "conectada" : "desconectada")}"));

    /// <summary>"1 com problema" ou "nenhum com problema".</summary>
    internal static string LinhaDispositivos(SecaoDispositivos d) =>
        d.ComProblema.FoiLido
            ? d.ComProblema.Valor!.Count == 0 ? "nenhum com problema" : $"{Formatador.Numero(d.ComProblema.Valor.Count)} com problema: {string.Join("; ", d.ComProblema.Valor.Select(p => $"{p.Nome.Valor ?? "sem nome"} (código {p.Codigo})"))}"
            : Texto(d.ComProblema);

    /// <summary>"16 GB DDR4-3200, 2 de 4 slots". O que não foi lido sai com o texto do estado.</summary>
    internal static string LinhaMemoria(SecaoMemoria m)
    {
        var primeiro = m.Modulos.Valor?.FirstOrDefault(x => !x.Vazio);
        var tipoVelocidade = primeiro is null ? Texto(m.Tipo) : Texto(MontadorSecoes.TipoVelocidade(primeiro));
        var slots = m.SlotsOcupados.FoiLido && m.SlotsTotal.FoiLido
            ? $"{m.SlotsOcupados.Valor} de {Formatador.Plural(m.SlotsTotal.Valor, "slot", "slots")}"
            : $"slots {Texto(m.SlotsTotal)}";
        return $"{Texto(m.Instalada, Formatador.Bytes)} {tipoVelocidade}, {slots}";
    }

    private static string Texto<T>(Campo<T> campo, Func<T, string>? formatar = null) =>
        campo.FoiLido ? formatar?.Invoke(campo.Valor!) ?? Convert.ToString(campo.Valor, Formatador.PtBr)! : $"[{TextosEstado.Texto(campo.Estado)}]";
}
