using MapHard.Nucleo.Baterias;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Dispositivos;
using MapHard.Nucleo.Eventos;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Saude;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Processador;
using MapHard.Nucleo.Rede;
using MapHard.Nucleo.Video;
using MapHard.Nucleo.Windows;
using MapHard.Nucleo.Windows11;

namespace MapHard.Nucleo.Painel;

/// <summary>Uma linha da tela: rótulo, valor em texto, estado (para a cor) e a dica com a fonte e o motivo.</summary>
public sealed record LinhaTela(string Rotulo, string Texto, EstadoCampo Estado, string Dica)
{
    public bool Lido => Estado == EstadoCampo.Lido;
}

/// <summary>
/// Um cartão da tela, com título e linhas. O selo, quando existe, aparece grande ao lado do título, na cor do
/// tom ("bom", "atencao", "ruim" ou "desconhecido"): é a saúde de cada disco.
/// </summary>
public sealed record CartaoTela(string Titulo, IReadOnlyList<LinhaTela> Linhas, string? Selo = null, string? TomSelo = null);

/// <summary>Uma seção da navegação à esquerda.</summary>
public sealed record SecaoTela(string Id, string Titulo, IReadOnlyList<CartaoTela> Cartoes);

/// <summary>Transforma a coleta nas seções da tela. Toda linha passa pelo texto de estado: nada fica vazio.</summary>
public static class MontadorSecoes
{
    public const string Resumo = "resumo";
    public const string Processador = "processador";
    public const string Memoria = "memoria";
    public const string Discos = "discos";
    public const string Placa = "placa";
    public const string Estabilidade = "estabilidade";
    public const string Dispositivos = "dispositivos";
    public const string Video = "video";
    public const string Bateria = "bateria";
    public const string Windows = "windows";

    public static IReadOnlyList<SecaoTela> Montar(ColetaMaquina c, DateOnly hoje) =>
    [
        new(Resumo, "Resumo", MontarResumo(c)),
        new(Processador, "Processador", MontarProcessador(c.Processador)),
        new(Memoria, "Memória", MontarMemoria(c.Memoria, c.Estabilidade)),
        new(Discos, "Discos", MontarDiscos(c.Discos)),
        new(Placa, "Placa-mãe e firmware", MontarPlaca(c.Placa, hoje)),
        new(Estabilidade, "Estabilidade", MontarEstabilidade(c.Estabilidade)),
        new(Video, "Vídeo e monitores", MontarVideo(c.Video, c.Monitores)),
        new(Bateria, "Bateria", MontarBateria(c.Bateria)),
        new(Windows, "Windows", MontarWindows(c.Identificacao.Windows, c.Windows11, c.Rede)),
        new(Dispositivos, "Dispositivos", MontarDispositivos(c.Dispositivos)),
    ];

    /// <summary>Linha a partir de um campo. O texto do valor sai de <paramref name="formatar"/>; os outros estados, do texto do estado.</summary>
    public static LinhaTela Linha<T>(string rotulo, Campo<T> campo, Func<T, string>? formatar = null)
    {
        var fonte = TextosEstado.NomeFonte(campo.Fonte);
        if (campo.FoiLido)
        {
            var texto = formatar?.Invoke(campo.Valor!) ?? Convert.ToString(campo.Valor, Formatador.PtBr) ?? string.Empty;
            var dica = campo.Motivo is null ? $"Fonte: {fonte}" : $"Fonte: {fonte}. {Maiuscula(campo.Motivo)}";
            return new LinhaTela(rotulo, texto, EstadoCampo.Lido, dica);
        }

        var estado = TextosEstado.Texto(campo.Estado);
        var motivo = campo.Motivo is null ? string.Empty : $": {campo.Motivo}";
        return new LinhaTela(rotulo, $"{estado}{motivo}", campo.Estado, $"Fonte: {fonte}");
    }

    private static string SimNao(bool valor) => valor ? "sim" : "não";

    private static string Maiuscula(string texto) => texto.Length == 0 ? texto : char.ToUpper(texto[0], Formatador.PtBr) + texto[1..];

    private static IReadOnlyList<CartaoTela> MontarResumo(ColetaMaquina c)
    {
        var id = c.Identificacao;
        var p = c.Processador;
        return
        [
            new("Este computador",
            [
                Linha("Nome", id.Computador),
                Linha("Fabricante", id.Fabricante),
                Linha("Modelo", id.Modelo),
                Linha("Número de série", id.NumeroSerie),
                Linha("Processador", id.Processador),
                Linha("Núcleos e threads", Juntar(p.Nucleos, p.Threads), v => v),
                Linha("Memória", MemoriaComTipo(id.MemoriaInstalada, id.MemoriaTipo), v => v),
                Linha("Memória utilizável", id.MemoriaUtilizavel, Formatador.Bytes),
                Linha("Discos", id.Discos),
            ]),
            new("Windows",
            [
                Linha("Sistema", id.Windows.Nome),
                Linha("Versão", id.Windows.Versao),
                Linha("Compilação", id.Windows.Compilacao),
                Linha("Ativação", id.Windows.Ativacao),
                LinhaWindows11(c.Windows11),
            ]),
        ];
    }

    private static IReadOnlyList<CartaoTela> MontarProcessador(SecaoProcessador p)
    {
        var clocks = p.Clocks;
        return
        [
            new("Identificação",
            [
                Linha("Nome", p.Nome),
                Linha("Fabricante", p.Fabricante),
                Linha("Codinome", p.Codinome),
                Linha("Litografia", p.Litografia),
                Linha("Família, modelo e revisão", p.Assinatura),
                Linha("Soquete", p.Soquete),
                Linha("Revisão do microcódigo", p.Microcodigo),
            ]),
            new("Núcleos e clock",
            [
                Linha("Núcleos", p.Nucleos),
                Linha("Threads", p.Threads),
                Linha("Núcleos de desempenho", p.NucleosDesempenho),
                Linha("Núcleos de eficiência", p.NucleosEficiencia),
                Linha("Clock base", clocks.Base, v => Formatador.Mhz(v)),
                Linha("Clock máximo", clocks.Maximo, v => Formatador.Mhz(v)),
                Linha("Clock atual", clocks.Atual, v => Formatador.Mhz(v)),
                Linha("Uso", clocks.UsoTotal, v => Formatador.Porcentagem(v)),
            ]),
            new("Caches", LinhasCaches(p.Caches)),
            new("Instruções",
            [
                Linha("Conjunto", p.Instrucoes, v => string.Join(", ", v)),
                Linha("Nível x86-64", p.NivelX8664, v => $"x86-64-v{v}"),
            ]),
            new("Virtualização",
            [
                Linha("Suporte no processador", p.VirtualizacaoNoProcessador, SimNao),
                Linha("Ligada no firmware", p.VirtualizacaoLigada, SimNao),
                Linha("Hipervisor ativo", p.Hipervisor, SimNao),
                Linha("Hyper-V ou WSL instalado", p.HyperVPedido, SimNao),
            ]),
        ];
    }

    internal static IReadOnlyList<LinhaTela> LinhasCaches(Campo<IReadOnlyList<CacheCpu>> caches)
    {
        if (!caches.FoiLido)
        {
            return [Linha("Caches", caches)];
        }

        // Processador híbrido: os grupos do mesmo nível vão numa linha só, "4 x 48 KB + 8 x 32 KB".
        return caches.Valor!
            .GroupBy(c => c.Rotulo)
            .Select(g => new LinhaTela(
                g.Key,
                string.Join(" + ", g.Select(c => c.Unidades == 1 ? Formatador.Bytes(c.TamanhoPorUnidade) : $"{c.Unidades} x {Formatador.Bytes(c.TamanhoPorUnidade)}")),
                EstadoCampo.Lido,
                $"Fonte: {TextosEstado.NomeFonte(caches.Fonte)}. "
                    + string.Join("; ", g.Select(c => $"{(c.Associatividade is { } a ? $"{a} vias" : "totalmente associativo")}, linha de {c.TamanhoLinha} bytes").Distinct())))
            .ToList();
    }

    private static IReadOnlyList<CartaoTela> MontarMemoria(SecaoMemoria m, SecaoEstabilidade e)
    {
        var cartoes = new List<CartaoTela>
        {
            new("Resumo",
            [
                Linha("Instalada", m.Instalada, Formatador.Bytes),
                Linha("Utilizável", m.Utilizavel, Formatador.Bytes),
                Linha("Reservada pelo hardware", m.Reservada, Formatador.Bytes),
                Linha("Tipo", m.Tipo),
                Linha("Slots", Ocupacao(m.SlotsOcupados, m.SlotsTotal), v => v),
                Linha("Capacidade máxima", m.CapacidadeMaxima, Formatador.Bytes),
                Linha("Correção de erro (ECC)", m.Ecc),
            ]),
        };

        if (m.Alertas.Count > 0)
        {
            cartoes.Add(new("Atenção", m.Alertas.Select(a => new LinhaTela(RotuloAlerta(a.Codigo), a.Texto, EstadoCampo.Lido, "Regra do MapHard sobre os dados lidos")).ToList()));
        }

        if (m.Modulos.FoiLido)
        {
            cartoes.AddRange(m.Modulos.Valor!.Select((modulo, i) => CartaoSlot(modulo, i)));
        }
        else
        {
            cartoes.Add(new("Slots", [Linha("Módulos", m.Modulos)]));
        }

        var saudeMemoria = RegrasEstabilidade.Memoria(m, e);
        cartoes.Add(new("Erros de memória",
        [
            Linha("Diagnóstico de Memória", e.DiagnosticoMemoria),
            new LinhaTela("Erros registrados pelo hardware", "contados na seção Estabilidade", EstadoCampo.Lido, "Regra do MapHard: o erro de memória do WHEA ainda não é separado dos outros erros de hardware"),
        ], NomeSaude(saudeMemoria.Estado), TomSaude(saudeMemoria.Estado)));
        cartoes.Add(new("Ampliação", [Linha("Resposta", m.Ampliacao)]));
        cartoes.Add(new("Uso agora",
        [
            Linha("Em uso", m.EmUso, Formatador.Bytes),
            Linha("Disponível", m.Disponivel, Formatador.Bytes),
            Linha("Carga", m.Carga, v => Formatador.Porcentagem(v)),
            Linha("Em cache", m.Cache, Formatador.Bytes),
            Linha("Confirmada", m.Confirmada, Formatador.Bytes),
            Linha("Limite da confirmada", m.LimiteConfirmada, Formatador.Bytes),
        ]));
        return cartoes;
    }

    /// <summary>Um cartão por slot, com o nome do slot no título. Slot vazio tem uma linha só.</summary>
    private static CartaoTela CartaoSlot(ModuloTela m, int indice)
    {
        var titulo = m.Slot.Valor ?? m.Banco.Valor ?? $"Slot {indice + 1}";
        if (m.Vazio)
        {
            return new CartaoTela(titulo, [new LinhaTela("Situação", "vazio", EstadoCampo.Lido, "Fonte: SMBIOS")]);
        }

        return new CartaoTela(titulo,
        [
            Linha("Tamanho", m.Tamanho, Formatador.Bytes),
            Linha("Tipo e velocidade", TipoVelocidade(m), v => v),
            Linha("Formato", m.Formato),
            Linha("Fabricante", m.Fabricante),
            Linha("Part number", m.PartNumber),
            Linha("Número de série", m.NumeroSerie),
            Linha("Ranks", m.Ranks),
            Linha("Voltagem", m.VoltagemMv, mv => $"{(mv / 1000.0).ToString("0.0##", Formatador.PtBr)} V"),
            Linha("Banco", m.Banco),
        ]);
    }

    /// <summary>"DDR4-3200", com a nominal entre parênteses quando o módulo roda abaixo dela.</summary>
    internal static Campo<string> TipoVelocidade(ModuloTela m)
    {
        if (!m.VelocidadeConfigurada.FoiLido && !m.VelocidadeNominal.FoiLido)
        {
            return m.Tipo;
        }

        var emUso = m.VelocidadeConfigurada.FoiLido ? m.VelocidadeConfigurada : m.VelocidadeNominal;
        var texto = m.Tipo.FoiLido ? $"{m.Tipo.Valor}-{emUso.Valor}" : $"{emUso.Valor} MT/s";
        if (m.VelocidadeConfigurada.FoiLido && m.VelocidadeNominal.FoiLido && m.VelocidadeConfigurada.Valor < m.VelocidadeNominal.Valor)
        {
            texto += $" (o módulo aceita {m.VelocidadeNominal.Valor})";
        }

        return Campo<string>.Lido(texto, emUso.Fonte, m.VelocidadeConfigurada.FoiLido ? "velocidade configurada" : "velocidade nominal");
    }

    private static string RotuloAlerta(string codigo) => codigo switch
    {
        AlertasMemoria.ModulosDiferentes => "Módulos",
        AlertasMemoria.AbaixoDaVelocidade => "Velocidade",
        AlertasMemoria.CanalUnico => "Canal",
        AlertasMemoria.ReservaAlta => "Reserva",
        _ => "Alerta",
    };

    /// <summary>"2 de 4 ocupados".</summary>
    private static Campo<string> Ocupacao(Campo<int> ocupados, Campo<int> total) =>
        ocupados.FoiLido && total.FoiLido
            ? Campo<string>.Lido($"{ocupados.Valor} de {total.Valor} ocupados", total.Fonte, total.Motivo)
            : total.Mapear(t => $"{t} no total");

    /// <summary>"16 GB DDR4". Sem o tipo, só o tamanho.</summary>
    private static Campo<string> MemoriaComTipo(Campo<long> instalada, Campo<string> tipo) =>
        instalada.Mapear(b => tipo.FoiLido ? $"{Formatador.Bytes(b)} {tipo.Valor}" : Formatador.Bytes(b));

    /// <summary>
    /// Um cartão por disco, com a saúde no selo e o motivo na primeira linha; depois os dados do disco e os
    /// volumes; e um cartão com a tabela SMART logo abaixo de cada disco.
    /// </summary>
    private static IReadOnlyList<CartaoTela> MontarDiscos(SecaoDiscos s)
    {
        if (!s.Discos.FoiLido)
        {
            return [new("Discos", [Linha("Discos", s.Discos)])];
        }

        var cartoes = new List<CartaoTela>();
        foreach (var d in s.Discos.Valor!)
        {
            var titulo = $"Disco {d.Numero}: {d.Modelo.Valor ?? "modelo não informado"}";
            var linhas = new List<LinhaTela>
            {
                new("Saúde", d.Saude.Motivos.Count == 0 ? "nenhum problema encontrado" : string.Join("; ", d.Saude.Motivos), EstadoCampo.Lido, "Regras de saúde do MapHard sobre o SMART do disco"),
                Linha("Temperatura", d.Temperatura, Formatador.Temperatura),
                Linha("Horas ligado", d.HorasLigado, Formatador.Horas),
                Linha("Ciclos de energia", d.CiclosEnergia, v => Formatador.Numero(v)),
                Linha("Dados gravados", d.DadosGravadosBytes, Formatador.Gravados),
                Linha("Vida usada", d.VidaUsada, v => Formatador.Porcentagem(v)),
                Linha("Tipo", d.Tipo),
                Linha("Interface", d.Interface),
                Linha("Capacidade", d.Tamanho, v => Formatador.BytesDecimais(v)),
                Linha("Rotação", d.Rotacao, v => $"{Formatador.Numero(v)} rpm"),
                Linha("TRIM", d.Trim, v => v ? "ligado" : "desligado"),
                Linha("Partição", d.EstiloParticao),
                Linha("Firmware", d.Firmware),
                Linha("Número de série", d.NumeroSerie),
            };
            linhas.AddRange(d.Volumes.Select(LinhaVolume));
            cartoes.Add(new CartaoTela(titulo, linhas, NomeSaude(d.Saude.Estado), TomSaude(d.Saude.Estado)));
            cartoes.Add(new CartaoTela($"SMART do disco {d.Numero}", LinhasSmart(d.Smart)));
        }

        if (s.VolumesSemDisco.Count > 0)
        {
            cartoes.Add(new CartaoTela("Volumes sem disco físico ligado", s.VolumesSemDisco.Select(LinhaVolume).ToList()));
        }

        return cartoes;
    }

    /// <summary>
    /// Cartão "Eventos" com uma linha por grupo e a saúde no selo; um cartão por grupo com os detalhes, só quando houve
    /// evento; e o cartão "Este Windows" com o índice, o tempo ligado, o último boot e a instalação.
    /// </summary>
    private static IReadOnlyList<CartaoTela> MontarEstabilidade(SecaoEstabilidade e)
    {
        var saude = RegrasEstabilidade.Estabilidade(e);
        var cartoes = new List<CartaoTela>();
        if (e.Grupos.FoiLido)
        {
            var linhas = new List<LinhaTela>
            {
                new("Situação", saude.Motivos.Count == 0 ? "nenhum problema no período" : string.Join("; ", saude.Motivos), EstadoCampo.Lido, "Regras de saúde do MapHard sobre o log Sistema"),
            };
            linhas.AddRange(e.Grupos.Valor!.Select(g => new LinhaTela(
                g.Titulo,
                g.Quantidade == 0 ? "nenhum" : $"{Formatador.Numero(g.Quantidade)}, o último em {Formatador.DataHora(g.Ultimo!.Value.ToLocalTime())}",
                EstadoCampo.Lido,
                $"Fonte: log Sistema do Windows, últimos {e.Dias} dias")));
            cartoes.Add(new CartaoTela($"Eventos dos últimos {e.Dias} dias", linhas, NomeSaude(saude.Estado), TomSaude(saude.Estado)));
            cartoes.AddRange(e.Grupos.Valor!.Where(g => g.Detalhes.Count > 0).Select(g =>
                new CartaoTela(g.Titulo, g.Detalhes.Select((d, i) => new LinhaTela(i == 0 ? "Mais recentes" : string.Empty, d, EstadoCampo.Lido, "Fonte: log Sistema do Windows")).ToList())));
        }
        else
        {
            cartoes.Add(new CartaoTela("Eventos", [Linha("Log Sistema", e.Grupos)], NomeSaude(saude.Estado), TomSaude(saude.Estado)));
        }

        cartoes.Add(new CartaoTela("Este Windows",
        [
            Linha("Índice de estabilidade", e.IndiceEstabilidade, v => $"{v.ToString("0.0", Formatador.PtBr)} de 10"),
            Linha("Tempo ligado", e.TempoLigado, Formatador.Duracao),
            Linha("Último boot", e.UltimoBoot, d => Formatador.DataHora(d.ToLocalTime())),
            Linha("Windows instalado em", e.InstalacaoWindows, d => Formatador.DataHora(d.ToLocalTime())),
        ]));
        return cartoes;
    }

    /// <summary>Cartão com os dispositivos com problema e a saúde no selo; os desativados à parte.</summary>
    private static IReadOnlyList<CartaoTela> MontarDispositivos(SecaoDispositivos d)
    {
        var saude = RegrasEstabilidade.Dispositivos(d);
        if (!d.ComProblema.FoiLido)
        {
            return [new CartaoTela("Dispositivos com problema", [Linha("Dispositivos", d.ComProblema)], NomeSaude(saude.Estado), TomSaude(saude.Estado))];
        }

        static LinhaTela LinhaDispositivo(DispositivoProblema p) =>
            new(p.Nome.Valor ?? "dispositivo sem nome", $"código {p.Codigo}: {p.Texto}", EstadoCampo.Lido, $"Fonte: Gerenciador de Dispositivos do Windows. Classe: {p.Classe.Valor ?? "não informada"}");

        var linhas = d.ComProblema.Valor!.Count == 0
            ? new List<LinhaTela> { new("Situação", "nenhum dispositivo com problema", EstadoCampo.Lido, "Fonte: Gerenciador de Dispositivos do Windows") }
            : d.ComProblema.Valor!.Select(LinhaDispositivo).ToList();
        linhas.Add(Linha("Dispositivos verificados", d.Total, v => Formatador.Numero(v)));

        var cartoes = new List<CartaoTela> { new("Dispositivos com problema", linhas, NomeSaude(saude.Estado), TomSaude(saude.Estado)) };
        if (d.Desativados.Valor is { Count: > 0 } desativados)
        {
            cartoes.Add(new CartaoTela("Dispositivos desativados", desativados.Select(LinhaDispositivo).ToList()));
        }

        return cartoes;
    }

    private static LinhaTela LinhaWindows11(VerificacaoWindows11 v) =>
        new("Windows 11", RegrasWindows.TextoWindows11(v), EstadoCampo.Lido, $"Regras do MapHard sobre a lista de processadores da Microsoft, Windows 11 {v.VersaoLista}");

    /// <summary>Um cartão por placa de vídeo e um por monitor.</summary>
    private static IReadOnlyList<CartaoTela> MontarVideo(Campo<IReadOnlyList<PlacaVideo>> placas, Campo<IReadOnlyList<MonitorVideo>> monitores)
    {
        var cartoes = new List<CartaoTela>();
        if (!placas.FoiLido)
        {
            cartoes.Add(new("Placas de vídeo", [Linha("Placas de vídeo", placas)]));
        }
        else if (placas.Valor!.Count == 0)
        {
            cartoes.Add(new("Placas de vídeo", [new LinhaTela("Situação", "nenhuma placa de vídeo encontrada", EstadoCampo.Lido, "Fonte: Windows")]));
        }
        else
        {
            cartoes.AddRange(placas.Valor.Select((p, i) => new CartaoTela(p.Nome.Valor ?? $"Placa de vídeo {i + 1}",
            [
                Linha("Fabricante", p.Fabricante),
                Linha("Memória dedicada", p.MemoriaDedicada, Formatador.Bytes),
                Linha("Memória compartilhada", p.MemoriaCompartilhada, Formatador.Bytes),
                Linha("Versão do driver", p.VersaoDriver),
                Linha("Data do driver", p.DataDriver, Formatador.Data),
            ])));
        }

        if (!monitores.FoiLido)
        {
            cartoes.Add(new("Monitores", [Linha("Monitores", monitores)]));
        }
        else if (monitores.Valor!.Count == 0)
        {
            cartoes.Add(new("Monitores", [new LinhaTela("Situação", "nenhum monitor informou o EDID", EstadoCampo.Lido, "Fonte: Windows")]));
        }
        else
        {
            cartoes.AddRange(monitores.Valor.Select((m, i) => new CartaoTela($"Monitor {i + 1}: {m.Modelo.Valor ?? "modelo não informado"}",
            [
                Linha("Fabricante", m.Fabricante),
                Linha("Modelo", m.Modelo),
                Linha("Número de série", m.NumeroSerie),
                Linha("Ano de fabricação", m.AnoFabricacao, v => v.ToString(Formatador.PtBr)),
                Linha("Tamanho", m.Polegadas, v => $"{v.ToString("0.0", Formatador.PtBr)} polegadas"),
                Linha("Resolução nativa", m.ResolucaoNativa),
            ])));
        }

        return cartoes;
    }

    /// <summary>Um cartão por bateria, com a saúde no selo. Desktop sem bateria tem uma linha só.</summary>
    private static IReadOnlyList<CartaoTela> MontarBateria(Campo<IReadOnlyList<Bateria>> baterias)
    {
        var saude = RegrasWindows.Bateria(baterias);
        if (!baterias.FoiLido)
        {
            return [new CartaoTela("Bateria", [Linha("Bateria", baterias)], NomeSaude(saude.Estado), TomSaude(saude.Estado))];
        }

        if (baterias.Valor!.Count == 0)
        {
            return [new CartaoTela("Bateria", [new LinhaTela("Situação", LeitorBateria.SemBateria, EstadoCampo.Lido, "Fonte: Windows")])];
        }

        return baterias.Valor.Select((b, i) =>
        {
            var desta = RegrasWindows.Bateria(Campo<IReadOnlyList<Bateria>>.Lido([b], baterias.Fonte));
            return new CartaoTela(baterias.Valor.Count == 1 ? "Bateria" : $"Bateria {i + 1}",
            [
                new LinhaTela("Saúde", desta.Motivos.Count == 0 ? "nenhum problema encontrado" : string.Join("; ", desta.Motivos), EstadoCampo.Lido, "Regras de saúde do MapHard sobre a capacidade da bateria"),
                Linha("Desgaste", b.Desgaste, v => Formatador.Porcentagem(v)),
                Linha("Capacidade de projeto", b.CapacidadeProjetoMwh, Wh),
                Linha("Carga total hoje", b.CapacidadeAtualMwh, Wh),
                Linha("Ciclos de carga", b.Ciclos, v => Formatador.Numero(v)),
                Linha("Química", b.Quimica),
                Linha("Fabricante", b.Fabricante),
                Linha("Nome", b.Nome),
            ], NomeSaude(desta.Estado), TomSaude(desta.Estado));
        }).ToList();
    }

    private static string Wh(long mwh) => $"{(mwh / 1000.0).ToString("0.0", Formatador.PtBr)} Wh";

    /// <summary>Cartões "Windows", "Windows 11, item por item" (com o selo) e "Placas de rede".</summary>
    private static IReadOnlyList<CartaoTela> MontarWindows(DadosWindows w, VerificacaoWindows11 v, Campo<IReadOnlyList<PlacaRede>> rede)
    {
        var saude = RegrasWindows.Windows11(v);
        var dica = $"Lista de processadores da Microsoft, Windows 11 {v.VersaoLista}, e as leituras das outras seções";
        var itens = new List<LinhaTela> { LinhaWindows11(v) };
        itens.AddRange(v.Itens.Select(i => new LinhaTela(i.Item, $"{TextoRequisito(i.Estado)}: {i.Detalhe}", EstadoCampo.Lido, dica)));

        IReadOnlyList<LinhaTela> placas = !rede.FoiLido ? [Linha("Placas de rede", rede)]
            : rede.Valor!.Count == 0 ? [new LinhaTela("Situação", "nenhuma placa de rede física", EstadoCampo.Lido, "Fonte: Windows")]
            : rede.Valor.Select(LinhaRede).ToList();

        return
        [
            new("Windows",
            [
                Linha("Sistema", w.Nome),
                Linha("Versão", w.Versao),
                Linha("Compilação", w.Compilacao),
                Linha("Arquitetura", w.Arquitetura),
                Linha("Ativação", w.Ativacao),
            ]),
            new("Windows 11, item por item", itens, NomeSaude(saude.Estado), TomSaude(saude.Estado)),
            new("Placas de rede", placas),
        ];
    }

    public static string TextoRequisito(EstadoRequisito estado) => estado switch
    {
        EstadoRequisito.Atende => "atende",
        EstadoRequisito.Configuracao => "ajustar no firmware",
        EstadoRequisito.NaoAtende => "não atende",
        _ => "não confirmado",
    };

    /// <summary>"Ethernet" e "Wi-Fi, 02-00-5E-10-20-AB, 721 Mb/s" ou "..., desconectada".</summary>
    private static LinhaTela LinhaRede(PlacaRede p)
    {
        var partes = new List<string> { p.Tipo.Valor ?? "tipo não informado" };
        partes.Add(p.Mac.FoiLido ? p.Mac.Valor! : "MAC não informado");
        partes.Add(p.VelocidadeBps.FoiLido ? LeitorRede.TextoVelocidade(p.VelocidadeBps.Valor) : p.Conectada.Valor ? "velocidade não informada" : "desconectada");
        return new LinhaTela(p.Nome.Valor ?? "placa sem nome", string.Join(", ", partes), EstadoCampo.Lido, $"Fonte: Windows. {p.Descricao.Valor ?? "sem descrição"}");
    }

    public static string NomeSaude(EstadoSaude estado) => estado switch
    {
        EstadoSaude.Bom => "Bom",
        EstadoSaude.Atencao => "Atenção",
        EstadoSaude.Ruim => "Ruim",
        _ => "Desconhecido",
    };

    private static string TomSaude(EstadoSaude estado) => estado switch
    {
        EstadoSaude.Bom => "bom",
        EstadoSaude.Atencao => "atencao",
        EstadoSaude.Ruim => "ruim",
        _ => "desconhecido",
    };

    /// <summary>"Volume C: Sistema" e "412 GB livres de 930 GB, NTFS, BitLocker ligado".</summary>
    private static LinhaTela LinhaVolume(VolumeTela v)
    {
        var rotulo = v.Rotulo.FoiLido ? $"Volume {v.Letra.Valor} {v.Rotulo.Valor}" : $"Volume {v.Letra.Valor}";
        var espaco = v.Livre.FoiLido && v.Total.FoiLido ? $"{Formatador.Bytes(v.Livre.Valor)} livres de {Formatador.Bytes(v.Total.Valor)}" : "espaço não informado";
        var sistema = v.SistemaArquivos.Valor is { } fs ? $", {fs}" : string.Empty;
        var bitLocker = v.BitLocker.FoiLido ? $"BitLocker {v.BitLocker.Valor}" : $"BitLocker: {TextosEstado.Texto(v.BitLocker.Estado)}";
        return new LinhaTela(rotulo, $"{espaco}{sistema}, {bitLocker}", EstadoCampo.Lido, $"Fonte: {TextosEstado.NomeFonte(v.Total.Fonte)}; BitLocker pelo {TextosEstado.NomeFonte(v.BitLocker.Fonte)}");
    }

    /// <summary>Uma linha por atributo: "05h Setores realocados" e "atual 99, pior 99, limite 36, bruto 3". Em NVMe, o valor do log.</summary>
    private static IReadOnlyList<LinhaTela> LinhasSmart(Campo<IReadOnlyList<LinhaSmart>> smart)
    {
        if (!smart.FoiLido)
        {
            return [Linha("SMART", smart)];
        }

        return smart.Valor!.Select(l => l.Atual is null
            ? new LinhaTela(l.Nome, Formatador.Numero((long)Math.Min(l.Bruto, long.MaxValue)), EstadoCampo.Lido, $"Fonte: SMART do disco. {l.NomeOriginal}")
            : new LinhaTela(
                $"{l.Id} {l.Nome}",
                $"atual {l.Atual}, pior {l.Pior}, limite {(l.Limite is { } lim ? lim.ToString(Formatador.PtBr) : "não informado")}, bruto {Formatador.Numero((long)Math.Min(l.Bruto, long.MaxValue))}",
                EstadoCampo.Lido,
                $"Fonte: SMART do disco. {(l.NomeOriginal.Length > 0 ? l.NomeOriginal : "atributo do fabricante")}"))
            .ToList();
    }

    private static IReadOnlyList<CartaoTela> MontarPlaca(SecaoPlaca p, DateOnly hoje)
    {
        var f = p.Firmware;
        return
        [
            new("Equipamento",
            [
                Linha("Fabricante", p.EquipamentoFabricante),
                Linha("Modelo", p.EquipamentoModelo),
                Linha("Família", p.EquipamentoFamilia),
                Linha("SKU", p.EquipamentoSku),
                Linha("Número de série", p.EquipamentoNumeroSerie),
                Linha("UUID", p.EquipamentoUuid),
                Linha("Tipo de gabinete", p.Gabinete),
                Linha("Máquina virtual", p.MaquinaVirtual, SimNao),
            ]),
            new("Placa-mãe",
            [
                Linha("Fabricante", p.PlacaFabricante),
                Linha("Modelo", p.PlacaModelo),
                Linha("Versão", p.PlacaVersao),
                Linha("Número de série", p.PlacaNumeroSerie),
                Linha("Chipset", p.Chipset),
            ]),
            new("BIOS e firmware",
            [
                Linha("Fabricante da BIOS", p.BiosFabricante),
                Linha("Versão da BIOS", p.BiosVersao),
                Linha("Data da BIOS", p.BiosData, d => $"{Formatador.Data(d)} ({Formatador.IdadeEmAnos(d, hoje)})"),
                Linha("Modo", f.Modo),
                Linha("Secure Boot", f.SecureBoot, v => v ? "ligado" : "desligado"),
            ]),
            new("TPM",
            [
                Linha("Versão", f.TpmVersao),
                Linha("Fabricante", f.TpmFabricante),
            ]),
        ];
    }

    /// <summary>"8 núcleos, 16 threads" quando os dois foram lidos. Senão, o estado do que faltou.</summary>
    private static Campo<string> Juntar(Campo<int> nucleos, Campo<int> threads) =>
        nucleos.FoiLido && threads.FoiLido
            ? Campo<string>.Lido($"{Formatador.Plural(nucleos.Valor, "núcleo", "núcleos")}, {Formatador.Plural(threads.Valor, "thread", "threads")}", nucleos.Fonte)
            : nucleos.FoiLido ? threads.Mapear(_ => string.Empty) : nucleos.Mapear(_ => string.Empty);
}
