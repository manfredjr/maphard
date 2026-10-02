using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Smart;
using MapHard.Nucleo.Tabelas;

namespace MapHard.Nucleo.Saude;

/// <summary>Estado de saúde, na ordem de gravidade da seção 8 do desenho.</summary>
public enum EstadoSaude
{
    Bom,
    Atencao,
    Ruim,
    Desconhecido,
}

/// <summary>O estado e os motivos escritos, com os de Ruim primeiro.</summary>
public sealed record SaudeDisco(EstadoSaude Estado, IReadOnlyList<string> Motivos);

/// <summary>
/// Regras da seção 8 do desenho, com os valores iniciais que o Manfred pode ajustar. Sem SMART lido, o
/// estado é Desconhecido e nunca Bom (R26). Formato do bruto pelo drivedb.h (entrada DEFAULT): 05h e C4h
/// raw16, só os 16 bits baixos; C5h e C6h raw48; C2h e BEh tempminmax, temperatura no byte baixo.
/// </summary>
public static class RegrasDisco
{
    public const int LimiteTemperaturaHdd = 50;
    public const int LimiteTemperaturaSsd = 70;
    public const int VidaRestanteMinima = 10;
    public const int PercentualUsadoAtencao = 90;
    public const int MargemReserva = 10;

    private const byte Realocados = 0x05;
    private const byte EventosRealocacao = 0xC4;
    private const byte Pendentes = 0xC5;
    private const byte Incorrigiveis = 0xC6;
    private const byte Temperatura = 0xC2;
    private const byte TemperaturaAr = 0xBE;
    private const byte VidaRestanteSsd = 0xE7;
    private const byte DesgasteSsd = 0xE9;

    public static SaudeDisco Desconhecido(string motivo) => new(EstadoSaude.Desconhecido, [motivo]);

    public static SaudeDisco Ata(LeituraSmartAta leitura, TipoDisco tipo, AtributosSmart nomes)
    {
        if (leitura.Falha is { } falha)
        {
            return Desconhecido(falha);
        }

        var ruim = new List<string>();
        var atencao = new List<string>();

        if (leitura.FalhaPrevista == true)
        {
            ruim.Add("o próprio disco prevê falha");
        }

        foreach (var a in leitura.Atributos.Where(a => a.Limite is > 0 && a.Atual <= a.Limite))
        {
            ruim.Add($"{nomes.Nome(a.Id)} no limite do fabricante (valor {a.Atual}, limite {a.Limite})");
        }

        Contar(leitura, Realocados, 0xFFFF, "setor realocado", "setores realocados", atencao);
        Contar(leitura, EventosRealocacao, 0xFFFF, "evento de realocação", "eventos de realocação", atencao);
        Contar(leitura, Pendentes, 0xFFFF_FFFF_FFFF, "setor pendente", "setores pendentes", atencao);
        Contar(leitura, Incorrigiveis, 0xFFFF_FFFF_FFFF, "setor incorrigível", "setores incorrigíveis", atencao);

        var ssd = tipo == TipoDisco.SsdSata || (tipo != TipoDisco.Hdd && leitura.Ssd == true);
        if (ssd && (Buscar(leitura, VidaRestanteSsd) ?? Buscar(leitura, DesgasteSsd)) is { } vida && vida.Atual < VidaRestanteMinima)
        {
            atencao.Add($"vida restante de {vida.Atual}% informada pelo SSD");
        }

        if (TemperaturaAta(leitura) is { } graus && graus > (ssd ? LimiteTemperaturaSsd : LimiteTemperaturaHdd))
        {
            atencao.Add($"temperatura de {graus} °C");
        }

        return Montar(ruim, atencao);
    }

    public static SaudeDisco Nvme(SaudeNvme? saude, string? falha)
    {
        if (saude is null)
        {
            return Desconhecido(falha ?? FonteDiscosWindows.ControladoraSemSmart);
        }

        var ruim = saude.Alertas.Select(a => $"alerta crítico do disco: {a}").ToList();
        var atencao = new List<string>();
        if (saude.PercentualUsado >= PercentualUsadoAtencao)
        {
            atencao.Add($"{saude.PercentualUsado}% da vida útil usada");
        }

        if (saude.ErrosMidia > 0)
        {
            atencao.Add(Formatador.Plural((long)Math.Min(saude.ErrosMidia, long.MaxValue), "erro de mídia", "erros de mídia"));
        }

        if (saude.ReservaDisponivel < saude.LimiteReserva + MargemReserva)
        {
            atencao.Add($"reserva de {saude.ReservaDisponivel}%, perto do limite de {saude.LimiteReserva}%");
        }

        if (saude.TemperaturaC is { } t && saude.LimiteAvisoTemperaturaC is { } aviso && t > aviso)
        {
            atencao.Add($"temperatura de {t} °C, acima do aviso de {aviso} °C do próprio disco");
        }

        return Montar(ruim, atencao);
    }

    /// <summary>Temperatura do C2h, ou do BEh quando não houver C2h. Byte baixo do bruto; 0 é "sem leitura".</summary>
    public static int? TemperaturaAta(LeituraSmartAta leitura) =>
        (Buscar(leitura, Temperatura) ?? Buscar(leitura, TemperaturaAr)) is { } a && (a.Bruto & 0xFF) is > 0 and var t ? (int)t : null;

    private static SaudeDisco Montar(List<string> ruim, List<string> atencao) =>
        ruim.Count > 0
            ? new(EstadoSaude.Ruim, [.. ruim, .. atencao])
            : atencao.Count > 0 ? new(EstadoSaude.Atencao, atencao) : new(EstadoSaude.Bom, []);

    private static void Contar(LeituraSmartAta leitura, byte id, ulong mascara, string singular, string plural, List<string> motivos)
    {
        if (Buscar(leitura, id) is { } a && (a.Bruto & mascara) is > 0 and var n)
        {
            motivos.Add(Formatador.Plural((long)n, singular, plural));
        }
    }

    private static AtributoSmart? Buscar(LeituraSmartAta leitura, byte id) => leitura.Atributos.FirstOrDefault(a => a.Id == id);

    /// <summary>Saúde de todos os discos: o pior manda, e cada motivo leva o número do disco.</summary>
    public static SaudeArea Conjunto(SecaoDiscos discos)
    {
        if (!discos.Discos.FoiLido)
        {
            return new SaudeArea(EstadoSaude.Desconhecido, [discos.Discos.Motivo ?? "discos não lidos"]);
        }

        var lista = discos.Discos.Valor!;
        if (lista.Count == 0)
        {
            return new SaudeArea(EstadoSaude.Desconhecido, ["nenhum disco encontrado"]);
        }

        var pior = lista.Select(d => d.Saude.Estado).MaxBy(OrdemSaude.Peso);
        var motivos = lista
            .Where(d => d.Saude.Estado != EstadoSaude.Bom)
            .OrderByDescending(d => OrdemSaude.Peso(d.Saude.Estado))
            .SelectMany(d => d.Saude.Motivos.Select(m => $"Disco {d.Numero}: {m}"))
            .ToList();
        return new SaudeArea(pior, motivos);
    }
}
