using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Smbios;
using MapHard.Nucleo.Tabelas;

namespace MapHard.Nucleo.Memoria;

/// <summary>Um slot de memória, com ou sem módulo (R11). Slot vazio fica na lista, com <see cref="Vazio"/> ligado.</summary>
public sealed record ModuloTela(
    Campo<string> Slot,
    Campo<string> Banco,
    bool Vazio,
    Campo<long> Tamanho,
    Campo<string> Tipo,
    Campo<string> Formato,
    Campo<int> VelocidadeNominal,
    Campo<int> VelocidadeConfigurada,
    Campo<string> Fabricante,
    Campo<string> PartNumber,
    Campo<string> NumeroSerie,
    Campo<int> Ranks,
    Campo<int> VoltagemMv);

/// <summary>Um alerta da seção Memória (R13): o código, para as regras de saúde, e a frase para a tela.</summary>
public sealed record AlertaMemoria(string Codigo, string Texto);

/// <summary>Seção Memória (R9 a R14).</summary>
public sealed record SecaoMemoria(
    Campo<long> Instalada,
    Campo<long> Utilizavel,
    Campo<long> Reservada,
    Campo<string> Tipo,
    Campo<int> SlotsTotal,
    Campo<int> SlotsOcupados,
    Campo<long> CapacidadeMaxima,
    Campo<string> Ecc,
    Campo<IReadOnlyList<ModuloTela>> Modulos,
    Campo<long> EmUso,
    Campo<long> Disponivel,
    Campo<int> Carga,
    Campo<long> Confirmada,
    Campo<long> LimiteConfirmada,
    Campo<long> Cache,
    IReadOnlyList<AlertaMemoria> Alertas,
    Campo<string> Ampliacao);

/// <summary>O que o coletor leu para a seção Memória: o valor de cada fonte ou o motivo da falha.</summary>
public sealed record LeiturasMemoria(
    TabelaSmbios? Smbios,
    string? FalhaSmbios,
    long? InstaladaKb,
    string? FalhaInstalada,
    EstadoMemoriaWindows? Estado,
    string? FalhaEstado);

/// <summary>Junta o SMBIOS (tipos 16 e 17) e o Windows na seção Memória. A fonte que falha deixa só os campos dela em erro.</summary>
public static class LeitorMemoria
{
    private const string SlotVazio = "slot vazio";

    public static SecaoMemoria Montar(LeiturasMemoria l, FabricantesMemoria fabricantes)
    {
        var conjuntos = l.Smbios is null ? [] : ConjuntoMemoriaSmbios.DoSistema(l.Smbios);
        var brutos = l.Smbios is null ? [] : ModuloMemoriaSmbios.Todos(l.Smbios);
        var instalados = brutos.Where(m => !m.Vazio).ToList();

        Campo<T> DoSmbios<T>(Func<Campo<T>> ler) => l.FalhaSmbios is { } falha
            ? Campo<T>.Erro(FonteDado.Smbios, falha)
            : l.Smbios is null ? Campo<T>.Erro(FonteDado.Smbios, "tabela SMBIOS indisponível") : ler();

        var modulos = DoSmbios(() => brutos.Count == 0
            ? Campo<IReadOnlyList<ModuloTela>>.NaoInformado(FonteDado.Smbios, "o firmware não lista os módulos")
            : Campo<IReadOnlyList<ModuloTela>>.Lido(brutos.Select(m => Modulo(m, fabricantes)).ToList(), FonteDado.Smbios));

        var somaModulos = instalados.Count > 0 && instalados.All(m => m.TamanhoBytes is not null) ? instalados.Sum(m => m.TamanhoBytes!.Value) : (long?)null;
        var instalada = Instalada(l, somaModulos);
        var utilizavel = l.Estado is { } e
            ? Campo<long>.Lido(e.TotalBytes, FonteDado.Windows)
            : Campo<long>.Erro(FonteDado.Windows, l.FalhaEstado ?? "GlobalMemoryStatusEx falhou");
        var reservada = instalada.FoiLido && utilizavel.FoiLido && instalada.Valor >= utilizavel.Valor
            ? Campo<long>.Lido(instalada.Valor - utilizavel.Valor, FonteDado.Windows)
            : Campo<long>.NaoInformado(FonteDado.Windows);

        var tipos = instalados.Select(m => m.NomeDoTipo).OfType<string>().Distinct().ToList();
        var tipo = DoSmbios(() => tipos.Count == 0
            ? Campo<string>.NaoInformado(FonteDado.Smbios)
            : Campo<string>.Lido(string.Join(" e ", tipos), FonteDado.Smbios, tipos.Count > 1 ? "módulos de tipos diferentes" : null));

        var slotsConjunto = conjuntos.Count > 0 && conjuntos.All(c => c.Slots is not null) ? conjuntos.Sum(c => c.Slots!.Value) : (int?)null;
        var slotsTotal = DoSmbios(() => slotsConjunto is { } s
            ? Campo<int>.Lido(s, FonteDado.Smbios)
            : brutos.Count > 0
                ? Campo<int>.Lido(brutos.Count, FonteDado.Smbios, "contagem dos registros de módulo")
                : Campo<int>.NaoInformado(FonteDado.Smbios));
        var slotsOcupados = DoSmbios(() => brutos.Count > 0 ? Campo<int>.Lido(instalados.Count, FonteDado.Smbios) : Campo<int>.NaoInformado(FonteDado.Smbios));

        var capacidade = DoSmbios(() => conjuntos.Count > 0 && conjuntos.All(c => c.CapacidadeMaximaBytes is not null)
            ? Campo<long>.Lido(conjuntos.Sum(c => c.CapacidadeMaximaBytes!.Value), FonteDado.Smbios, "informada pelo firmware; o manual da placa é a referência")
            : Campo<long>.NaoInformado(FonteDado.Smbios));
        var ecc = DoSmbios(() => Campo.Texto(conjuntos.Select(c => ConjuntoMemoriaSmbios.NomeCorrecaoErro(c.CorrecaoErro)).FirstOrDefault(n => n is not null), FonteDado.Smbios));

        Campo<T> DoWindows<T>(Func<EstadoMemoriaWindows, T?> ler)
            where T : struct => l.Estado is null
                ? Campo<T>.Erro(FonteDado.Windows, l.FalhaEstado ?? "GlobalMemoryStatusEx falhou")
                : ler(l.Estado) is { } valor ? Campo<T>.Lido(valor, FonteDado.Windows) : Campo<T>.NaoInformado(FonteDado.Windows, "GetPerformanceInfo falhou");

        return new SecaoMemoria(
            instalada,
            utilizavel,
            reservada,
            tipo,
            slotsTotal,
            slotsOcupados,
            capacidade,
            ecc,
            modulos,
            DoWindows<long>(x => x.TotalBytes - x.DisponivelBytes),
            DoWindows<long>(x => x.DisponivelBytes),
            DoWindows<int>(x => x.CargaPercentual),
            DoWindows(x => x.ConfirmadaBytes),
            DoWindows(x => x.LimiteConfirmadaBytes),
            DoWindows(x => x.CacheBytes),
            [],
            Campo<string>.NaoInformado(FonteDado.Smbios));
    }

    /// <summary>Instalada: a do Windows. Sem ela, a soma dos módulos. Diferença entre as duas vai na observação.</summary>
    private static Campo<long> Instalada(LeiturasMemoria l, long? somaModulos)
    {
        if (l.InstaladaKb is { } kb)
        {
            var bytes = kb * 1024;
            return somaModulos is { } soma && soma != bytes
                ? Campo<long>.Lido(bytes, FonteDado.Windows, $"a soma dos módulos no SMBIOS dá {Formatador.Bytes(soma)}")
                : Campo<long>.Lido(bytes, FonteDado.Windows);
        }

        return somaModulos is { } s
            ? Campo<long>.Lido(s, FonteDado.Smbios, "soma dos módulos; o Windows não informou")
            : Campo<long>.Erro(FonteDado.Windows, l.FalhaInstalada ?? "GetPhysicallyInstalledSystemMemory falhou");
    }

    private static ModuloTela Modulo(ModuloMemoriaSmbios m, FabricantesMemoria fabricantes)
    {
        var slot = Campo.Texto(m.Slot, FonteDado.Smbios);
        var banco = Campo.Texto(m.Banco, FonteDado.Smbios);
        if (m.Vazio)
        {
            var vazioTexto = Campo<string>.NaoSuportado(FonteDado.Smbios, SlotVazio);
            var vazioNumero = Campo<int>.NaoSuportado(FonteDado.Smbios, SlotVazio);
            return new ModuloTela(slot, banco, true, Campo<long>.NaoSuportado(FonteDado.Smbios, SlotVazio), vazioTexto, vazioTexto,
                vazioNumero, vazioNumero, vazioTexto, vazioTexto, vazioTexto, vazioNumero, vazioNumero);
        }

        return new ModuloTela(
            slot,
            banco,
            false,
            Campo.DeOpcional(m.TamanhoBytes, FonteDado.Smbios),
            Campo.Texto(m.NomeDoTipo, FonteDado.Smbios),
            Campo.Texto(m.NomeDoFormato, FonteDado.Smbios),
            Campo.DeOpcional(m.VelocidadeNominal, FonteDado.Smbios),
            Campo.DeOpcional(m.VelocidadeConfigurada, FonteDado.Smbios),
            fabricantes.Traduzir(m.Fabricante, m.CodigoFabricante),
            Campo.Texto(m.PartNumber, FonteDado.Smbios),
            Campo.Texto(m.NumeroSerie, FonteDado.Smbios),
            Campo.DeOpcional(m.Ranks, FonteDado.Smbios),
            Campo.DeOpcional(m.VoltagemConfiguradaMv, FonteDado.Smbios));
    }
}
