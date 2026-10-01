using System.Reflection;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Cpuid;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Processador;
using MapHard.Nucleo.Smbios;
using MapHard.Nucleo.Tabelas;
using MapHard.Nucleo.Windows;

namespace MapHard.Nucleo.Coleta;

/// <summary>
/// Lê todas as fontes, cada uma com tempo limite, e monta a coleta. Fonte que falha ou demora
/// deixa só os campos dela em erro de leitura, com o motivo; o resto da coleta sai normal.
/// </summary>
public sealed class Coletor
{
    private readonly FontesColeta _fontes;
    private readonly TimeSpan _tempoLimite;
    private readonly TabelaProcessadores _tabela;
    private readonly FabricantesMemoria _fabricantesMemoria;
    private readonly Func<DateTimeOffset> _agora;

    public Coletor(
        FontesColeta fontes,
        TimeSpan tempoLimitePorFonte,
        TabelaProcessadores? tabela = null,
        Func<DateTimeOffset>? agora = null,
        FabricantesMemoria? fabricantesMemoria = null)
    {
        _fontes = fontes;
        _tempoLimite = tempoLimitePorFonte;
        _tabela = tabela ?? TabelaProcessadores.Embutida;
        _fabricantesMemoria = fabricantesMemoria ?? FabricantesMemoria.Embutida;
        _agora = agora ?? (() => DateTimeOffset.Now);
    }

    public static string VersaoPrograma =>
        typeof(Coletor).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    /// <summary>Resultado de uma fonte: o valor ou o motivo da falha.</summary>
    internal readonly record struct Leitura<T>(T? Valor, string? Falha)
    {
        public bool Falhou => Falha is not null;
    }

    public async Task<ColetaMaquina> ColetarAsync(IProgress<string>? andamento = null, CancellationToken cancelar = default)
    {
        andamento?.Report("lendo firmware, processador e memória...");

        // As leituras rápidas correm juntas. A amostra dos clocks leva 1 segundo.
        var smbios = Ler(() => LeitorTabelaSmbios.Interpretar(_fontes.Smbios.LerTabelaBruta()), cancelar);
        var cpuid = Ler(() => DecodificadorCpuid.Decodificar(_fontes.Cpuid), cancelar);
        var topologia = Ler(() => LeitorTopologia.Interpretar(_fontes.Topologia.LerBruta()), cancelar);
        var firmware = Ler(() => LeitorFirmware.Ler(_fontes.Firmware, _fontes.Registro, _fontes.Administrador()), cancelar);
        var windows = Ler(() => LeitorWindows.Ler(_fontes.Registro), cancelar);
        var computador = Ler(_fontes.NomeComputador, cancelar);
        var instalada = Ler(_fontes.Memoria.InstaladaKb, cancelar);
        var estadoMemoria = Ler(_fontes.Memoria.Estado, cancelar);
        var administrador = Ler(_fontes.Administrador, cancelar);
        await Task.WhenAll(smbios, cpuid, topologia, firmware, windows, computador, instalada, estadoMemoria, administrador).ConfigureAwait(false);

        andamento?.Report("medindo clock e uso...");
        var tabelaSmbios = smbios.Result;
        var id = cpuid.Result;
        var cpuSmbios = tabelaSmbios.Valor is null ? null : ProcessadorSmbios.Todos(tabelaSmbios.Valor).FirstOrDefault();
        var clocks = await Ler(
            () => CalculoClocks.Montar(id.Valor?.ClockBaseMhz, id.Valor?.ClockMaximoMhz, cpuSmbios?.ClockMaximoMhz, _fontes.Clocks),
            cancelar,
            tempoLimite: _tempoLimite + TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        var dadosFirmware = OcultoPeloHipervisor(firmware.Result.Valor ?? FirmwareEmErro(firmware.Result.Falha!), id.Valor);
        var dadosWindows = windows.Result.Valor ?? WindowsEmErro(windows.Result.Falha!);

        var processador = MontarProcessador(tabelaSmbios, id, topologia.Result, clocks, dadosFirmware);
        var memoria = LeitorMemoria.Montar(
            new LeiturasMemoria(
                tabelaSmbios.Valor,
                tabelaSmbios.Falha,
                instalada.Result.Valor,
                instalada.Result.Falha,
                estadoMemoria.Result.Valor,
                estadoMemoria.Result.Falha),
            _fabricantesMemoria);
        var placa = MontarPlaca(tabelaSmbios, dadosFirmware, id.Valor);
        var identificacao = MontarIdentificacao(computador.Result, memoria, placa, processador, dadosWindows);

        andamento?.Report("coleta concluída");
        return new ColetaMaquina(
            ColetaMaquina.NomeFormato,
            ColetaMaquina.VersaoAtual,
            VersaoPrograma,
            _agora(),
            administrador.Result.Valor,
            identificacao,
            processador,
            memoria,
            placa);
    }

    private async Task<Leitura<T>> Ler<T>(Func<T> ler, CancellationToken cancelar, TimeSpan? tempoLimite = null)
    {
        try
        {
            var valor = await Task.Run(ler, cancelar).WaitAsync(tempoLimite ?? _tempoLimite, cancelar).ConfigureAwait(false);
            return new Leitura<T>(valor, null);
        }
        catch (TimeoutException)
        {
            return new Leitura<T>(default, "tempo esgotado");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            return new Leitura<T>(default, erro.Message);
        }
    }

    private SecaoProcessador MontarProcessador(
        Leitura<TabelaSmbios?> smbios,
        Leitura<IdentidadeCpu?> cpuid,
        Leitura<TopologiaCpu?> topologia,
        Leitura<ClocksCpu> clocks,
        DadosFirmware firmware)
    {
        var id = cpuid.Valor;
        var cpuSmbios = smbios.Valor is null ? null : ProcessadorSmbios.Todos(smbios.Valor).FirstOrDefault();
        var t = topologia.Valor;

        Campo<T> DoCpuid<T>(Func<IdentidadeCpu, T?> ler)
            where T : class => cpuid.Falhou
                ? Campo<T>.Erro(FonteDado.Cpuid, cpuid.Falha!)
                : id is null
                    ? Campo<T>.NaoSuportado(FonteDado.Cpuid, "instrução CPUID indisponível")
                    : ler(id) is { } valor ? Campo<T>.Lido(valor, FonteDado.Cpuid) : Campo<T>.NaoSuportado(FonteDado.Cpuid);

        Campo<int> DaTopologia(Func<TopologiaCpu, int> ler) => topologia.Falhou
            ? Campo<int>.Erro(FonteDado.Topologia, topologia.Falha!)
            : t is null ? Campo<int>.NaoInformado(FonteDado.Topologia) : Campo<int>.Lido(ler(t), FonteDado.Topologia);

        // Nome: o do CPUID. Sem ele, a versão do SMBIOS.
        var nome = DoCpuid(i => i.NomeComercial);
        if (!nome.FoiLido && cpuSmbios?.Versao is { } versaoSmbios)
        {
            nome = Campo.Texto(versaoSmbios, FonteDado.Smbios);
        }

        var linha = id is null ? null : _tabela.Buscar(id.Fabricante, id.Familia, id.Modelo, id.Revisao);
        var codinome = id is null
            ? DoCpuid<string>(_ => null)
            : linha is null
                ? Campo<string>.NaoInformado(FonteDado.Tabela, "não consta na tabela do MapHard")
                : Campo<string>.Lido(linha.Nome, FonteDado.Tabela, linha.Fonte);
        var litografia = linha?.Litografia is { } nm
            ? Campo<string>.Lido(nm, FonteDado.Tabela, linha.Fonte)
            : Campo<string>.NaoInformado(FonteDado.Tabela);

        // Núcleos e threads: vale a topologia do Windows. Divergência com o SMBIOS vai no motivo.
        var nucleos = DaTopologia(x => x.Nucleos);
        var threads = DaTopologia(x => x.Threads);
        if (!nucleos.FoiLido && cpuSmbios?.Nucleos is { } nucleosSmbios)
        {
            nucleos = Campo<int>.Lido(nucleosSmbios, FonteDado.Smbios);
            threads = cpuSmbios.Threads is { } threadsSmbios ? Campo<int>.Lido(threadsSmbios, FonteDado.Smbios) : threads;
        }
        else if (nucleos.FoiLido && cpuSmbios?.Nucleos is { } outro && outro != nucleos.Valor && (smbios.Valor is null || ProcessadorSmbios.Todos(smbios.Valor).Count == 1))
        {
            nucleos = Campo<int>.Lido(nucleos.Valor, FonteDado.Topologia, $"o SMBIOS informa {outro}");
        }

        var hibrido = t?.Hibrido ?? id?.Hibrido ?? false;
        var desempenho = hibrido ? DaTopologia(x => x.NucleosDesempenho) : Campo<int>.NaoSuportado(FonteDado.Topologia, "processador sem núcleos de eficiência");
        var eficiencia = hibrido ? DaTopologia(x => x.NucleosEficiencia) : Campo<int>.NaoSuportado(FonteDado.Topologia, "processador sem núcleos de eficiência");

        var caches = topologia.Falhou
            ? Campo<IReadOnlyList<CacheCpu>>.Erro(FonteDado.Topologia, topologia.Falha!)
            : t is { Caches.Count: > 0 } ? Campo<IReadOnlyList<CacheCpu>>.Lido(t.Caches, FonteDado.Topologia) : Campo<IReadOnlyList<CacheCpu>>.NaoInformado(FonteDado.Topologia);

        var nivel = id?.NivelX8664 is { } n ? Campo<int>.Lido(n, FonteDado.Cpuid) : Campo<int>.NaoInformado(FonteDado.Cpuid);

        return new SecaoProcessador(
            nome,
            DoCpuid(i => i.NomeFabricante),
            DoCpuid(i => i.Assinatura),
            codinome,
            litografia,
            TextoSmbios(smbios, _ => cpuSmbios?.Soquete),
            firmware.Microcodigo,
            nucleos,
            threads,
            desempenho,
            eficiencia,
            caches,
            DoCpuid<IReadOnlyList<string>>(i => i.Instrucoes),
            nivel,
            clocks.Valor ?? ClocksEmErro(clocks.Falha!),
            id is null
                ? DoCpuid<string>(_ => null).Mapear(_ => false)
                : id.HipervisorPresente && !id.VirtualizacaoNoProcessador
                    ? Campo<bool>.NaoInformado(FonteDado.Cpuid, MotivoHipervisor)
                    : Campo<bool>.Lido(id.VirtualizacaoNoProcessador, FonteDado.Cpuid),
            firmware.VirtualizacaoLigada,
            id is null ? DoCpuid<string>(_ => null).Mapear(_ => false) : Campo<bool>.Lido(id.HipervisorPresente, FonteDado.Cpuid));
    }

    private static SecaoPlaca MontarPlaca(Leitura<TabelaSmbios?> smbios, DadosFirmware firmware, IdentidadeCpu? id)
    {
        var tabela = smbios.Valor;
        var placa = tabela is null ? null : PlacaSmbios.De(tabela);
        var sistema = tabela is null ? null : SistemaSmbios.De(tabela);
        var gabinete = tabela is null ? null : GabineteSmbios.De(tabela);
        var bios = tabela is null ? null : BiosSmbios.De(tabela);

        var biosData = smbios.Falhou
            ? Campo<DateOnly>.Erro(FonteDado.Smbios, smbios.Falha!)
            : bios?.Data is { } data ? Campo<DateOnly>.Lido(data, FonteDado.Smbios) : Campo<DateOnly>.NaoInformado(FonteDado.Smbios);

        // Máquina virtual: o bit da BIOS, ou o hipervisor do CPUID com o fabricante do SMBIOS de hipervisor.
        var maquinaVirtual = bios?.MaquinaVirtual is { } vm
            ? Campo<bool>.Lido(vm || EhHipervisorConhecido(sistema, id), FonteDado.Smbios)
            : id is null ? Campo<bool>.NaoInformado(FonteDado.Cpuid) : Campo<bool>.Lido(EhHipervisorConhecido(sistema, id), FonteDado.Cpuid);

        return new SecaoPlaca(
            TextoSmbios(smbios, _ => placa?.Fabricante),
            TextoSmbios(smbios, _ => placa?.Produto),
            TextoSmbios(smbios, _ => placa?.Versao),
            TextoSmbios(smbios, _ => placa?.NumeroSerie),
            TextoSmbios(smbios, _ => sistema?.Fabricante),
            TextoSmbios(smbios, _ => sistema?.Produto),
            TextoSmbios(smbios, _ => sistema?.Familia),
            TextoSmbios(smbios, _ => sistema?.Sku),
            TextoSmbios(smbios, _ => sistema?.NumeroSerie),
            TextoSmbios(smbios, _ => sistema?.Uuid),
            TextoSmbios(smbios, _ => gabinete?.Tipo),
            maquinaVirtual,
            TextoSmbios(smbios, _ => bios?.Fabricante),
            TextoSmbios(smbios, _ => bios?.Versao),
            biosData,
            firmware);
    }

    private static Identificacao MontarIdentificacao(Leitura<string> computador, SecaoMemoria memoria, SecaoPlaca placa, SecaoProcessador processador, DadosWindows windows)
    {
        // Fabricante e modelo do equipamento. Texto de fábrica no tipo 1 cai para a placa-mãe (tipo 2), dito na observação.
        var fabricante = placa.EquipamentoFabricante.FoiLido ? placa.EquipamentoFabricante : DaPlaca(placa.PlacaFabricante, placa.EquipamentoFabricante);
        var modelo = placa.EquipamentoModelo.FoiLido ? placa.EquipamentoModelo : DaPlaca(placa.PlacaModelo, placa.EquipamentoModelo);
        var serie = placa.EquipamentoNumeroSerie.FoiLido ? placa.EquipamentoNumeroSerie : DaPlaca(placa.PlacaNumeroSerie, placa.EquipamentoNumeroSerie);

        return new Identificacao(
            computador.Falhou || string.IsNullOrWhiteSpace(computador.Valor)
                ? Campo<string>.Erro(FonteDado.Windows, computador.Falha ?? "nome vazio")
                : Campo<string>.Lido(computador.Valor, FonteDado.Windows),
            fabricante,
            modelo,
            serie,
            processador.Nome,
            memoria.Instalada,
            memoria.Tipo,
            memoria.Utilizavel,
            windows);
    }

    private static Campo<string> DaPlaca(Campo<string> daPlaca, Campo<string> original) =>
        daPlaca.FoiLido ? Campo<string>.Lido(daPlaca.Valor!, daPlaca.Fonte, "da placa-mãe") : original;

    private static Campo<string> TextoSmbios(Leitura<TabelaSmbios?> smbios, Func<TabelaSmbios, string?> ler) =>
        smbios.Falhou
            ? Campo<string>.Erro(FonteDado.Smbios, smbios.Falha!)
            : smbios.Valor is null
                ? Campo<string>.Erro(FonteDado.Smbios, "tabela SMBIOS indisponível")
                : Campo.Texto(ler(smbios.Valor), FonteDado.Smbios);

    // O bit de hipervisor sozinho não basta: com a segurança baseada em virtualização do Windows 11
    // ligada, ele aparece também em máquina física. Por isso a regra exige o fabricante ou o modelo
    // do SMBIOS de um hipervisor conhecido.
    private static readonly string[] _fabricantesHipervisor = ["VMware", "QEMU", "innotek", "VirtualBox", "Xen", "Parallels"];

    private static bool EhHipervisorConhecido(SistemaSmbios? sistema, IdentidadeCpu? id) =>
        id?.HipervisorPresente == true && sistema is not null
        && ((sistema.Fabricante is { } f && _fabricantesHipervisor.Any(h => f.Contains(h, StringComparison.OrdinalIgnoreCase)))
            || sistema.Produto == "Virtual Machine");

    // Com o hipervisor ativo (Hyper-V ou a segurança baseada em virtualização do Windows), o processador
    // esconde a virtualização do próprio Windows: o CPUID e o IsProcessorFeaturePresent dizem "não"
    // mesmo com ela suportada e ligada. Esse "não" vira "não informado", com o motivo.
    internal const string MotivoHipervisor = "o hipervisor ativo esconde esta informação do Windows";

    private static DadosFirmware OcultoPeloHipervisor(DadosFirmware firmware, IdentidadeCpu? id) =>
        id?.HipervisorPresente == true && firmware.VirtualizacaoLigada is { FoiLido: true, Valor: false }
            ? firmware with { VirtualizacaoLigada = Campo<bool>.NaoInformado(FonteDado.Windows, MotivoHipervisor) }
            : firmware;

    private static DadosFirmware FirmwareEmErro(string motivo)
    {
        var texto = Campo<string>.Erro(FonteDado.Firmware, motivo);
        var sim = Campo<bool>.Erro(FonteDado.Firmware, motivo);
        return new DadosFirmware(texto, sim, texto, texto, texto, sim);
    }

    private static DadosWindows WindowsEmErro(string motivo)
    {
        var texto = Campo<string>.Erro(FonteDado.Registro, motivo);
        return new DadosWindows(texto, texto, texto);
    }

    private static ClocksCpu ClocksEmErro(string motivo) => new(
        Campo<int>.Erro(FonteDado.Contador, motivo),
        Campo<int>.Erro(FonteDado.Contador, motivo),
        Campo<int>.Erro(FonteDado.Contador, motivo),
        Campo<double>.Erro(FonteDado.Contador, motivo),
        Campo<IReadOnlyList<double>>.Erro(FonteDado.Contador, motivo));
}
