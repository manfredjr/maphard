using MapHard.Nucleo.Baterias;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Dispositivos;
using MapHard.Nucleo.Eventos;
using MapHard.Nucleo.Saude;
using MapHard.Nucleo.Smart;
using MapHard.Nucleo.Tabelas;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Processador;
using MapHard.Nucleo.Rede;
using MapHard.Nucleo.Video;
using MapHard.Nucleo.Windows;
using MapHard.Nucleo.Windows11;

namespace MapHard.Nucleo.Painel;

/// <summary>
/// Máquina fictícia do modo <c>--demonstracao</c>, para imagem de tela e página. Nenhum dado vem do
/// computador onde o programa roda. Os estados sem leitura também aparecem, para a imagem mostrar como ficam.
/// </summary>
public static class DadosDemonstracao
{
    private const FonteDado D = FonteDado.Demonstracao;

    public static ColetaMaquina Coleta()
    {
        var windows = new DadosWindows(Texto("Windows 11 Pro"), Texto("23H2"), Texto("22631.4169"), Texto("x64"), Texto("ativado"));
        var firmware = new DadosFirmware(
            Texto("UEFI"),
            Campo<bool>.Lido(false, D),
            Texto("2.0"),
            Campo<string>.RequerAdministrador(D),
            Texto("0xF4"),
            Campo<bool>.Lido(true, D));

        var processador = new SecaoProcessador(
            Texto("Processador Exemplo 3.60GHz"),
            Texto("Fabricante Exemplo"),
            Texto("6, 9E, A"),
            Texto("Codinome Exemplo"),
            Campo<string>.NaoInformado(D),
            Texto("SOQUETE 1"),
            Texto("0xF4"),
            Campo<int>.Lido(8, D),
            Campo<int>.Lido(16, D),
            Campo<int>.NaoSuportado(D, "processador sem núcleos de eficiência"),
            Campo<int>.NaoSuportado(D, "processador sem núcleos de eficiência"),
            Campo<IReadOnlyList<CacheCpu>>.Lido(
            [
                new CacheCpu(1, TipoCache.Dados, 32 * 1024, 8, 8, 64),
                new CacheCpu(1, TipoCache.Instrucoes, 32 * 1024, 8, 8, 64),
                new CacheCpu(2, TipoCache.Unificado, 256 * 1024, 8, 4, 64),
                new CacheCpu(3, TipoCache.Unificado, 12 * 1024 * 1024, 1, 16, 64),
            ], D),
            Campo<IReadOnlyList<string>>.Lido(["MMX", "SSE", "SSE2", "SSE3", "SSSE3", "SSE4.1", "SSE4.2", "x86-64", "AES", "AVX", "AVX2", "FMA3", "VT-x"], D),
            Campo<int>.Lido(3, D),
            new ClocksCpu(
                Campo<int>.Lido(3600, D),
                Campo<int>.Lido(4700, D),
                Campo<int>.Lido(4300, D),
                Campo<double>.Lido(12, D),
                Campo<IReadOnlyList<double>>.Lido([10, 14, 8, 16, 12, 10, 14, 12, 10, 14, 8, 16, 12, 10, 14, 12], D)),
            Campo<bool>.Lido(true, D),
            Campo<bool>.Lido(true, D),
            Campo<bool>.Lido(false, D));

        var placa = new SecaoPlaca(
            Texto("Fabricante Placa Exemplo"),
            Texto("PLACA-EXEMPLO"),
            Texto("1.0"),
            Texto("SERIE-TESTE-0002"),
            Campo<string>.Lido("Chipset Exemplo", D, "dispositivo PCI 8086:0000"),
            Texto("Fabricante Exemplo"),
            Texto("Modelo Exemplo"),
            Texto("Linha Exemplo"),
            Campo<string>.NaoInformado(D),
            Texto("SERIE-TESTE-0001"),
            Texto("00000000-0000-0000-0000-000000000001"),
            Texto("desktop"),
            Campo<bool>.Lido(false, D),
            Texto("Fabricante BIOS Exemplo"),
            Texto("1.0"),
            Campo<DateOnly>.Lido(new DateOnly(2021, 3, 15), D),
            firmware);

        var memoria = Memoria();
        var discos = Discos();

        var identificacao = new Identificacao(
            Texto("ESTACAO-EXEMPLO"),
            Texto("Fabricante Exemplo"),
            Texto("Modelo Exemplo"),
            Texto("SERIE-TESTE-0001"),
            processador.Nome,
            memoria.Instalada,
            memoria.Tipo,
            memoria.Utilizavel,
            Coletor.ResumoDiscos(discos),
            windows);

        return new ColetaMaquina(
            ColetaMaquina.NomeFormato,
            ColetaMaquina.VersaoAtual,
            Coletor.VersaoPrograma,
            new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.FromHours(-3)),
            false,
            identificacao,
            processador,
            memoria,
            discos,
            placa,
            Estabilidade(),
            Dispositivos(),
            Campo<IReadOnlyList<PlacaVideo>>.Lido(
                [new PlacaVideo(Texto("Placa de Vídeo Exemplo"), Texto("Fabricante de Vídeo Exemplo"), Campo<long>.Lido(4L << 30, D), Campo<long>.Lido(8L << 30, D), Texto("32.0.15.1234"), Campo<DateOnly>.Lido(new DateOnly(2026, 3, 30), D))], D),
            Campo<IReadOnlyList<MonitorVideo>>.Lido(
                [new MonitorVideo(Texto("MON"), Texto("Monitor Exemplo 24"), Texto("SERIE-MONITOR-0001"), Campo<int>.Lido(2023, D), Campo<double>.Lido(23.8, D, "53 x 30 cm"), Texto("1920 x 1080"))], D),
            Campo<IReadOnlyList<Bateria>>.Lido(
                [new Bateria(Texto("Bateria Exemplo"), Texto("Fabricante Exemplo"), Campo<string>.Lido("íon de lítio", D, "LION"), Campo<long>.Lido(56_000, D), Campo<long>.Lido(49_280, D), Campo<double>.Lido(12, D), Campo<int>.Lido(214, D))], D),
            Campo<IReadOnlyList<PlacaRede>>.Lido(
            [
                new PlacaRede(Texto("Ethernet"), Texto("Placa de Rede Exemplo"), Texto("02-00-5E-10-20-AB"), Texto("Ethernet"), Campo<long>.NaoInformado(D, "desconectada"), Campo<bool>.Lido(false, D)),
                new PlacaRede(Texto("Wi-Fi"), Texto("Placa Sem Fio Exemplo"), Texto("02-00-5E-10-20-AC"), Texto("Wi-Fi"), Campo<long>.Lido(721_000_000, D), Campo<bool>.Lido(true, D)),
            ], D),
            Windows11());
    }

    /// <summary>
    /// Estabilidade fictícia: uma tela azul e um desligamento inesperado no período, o resto zerado, para a
    /// imagem mostrar o cartão em Atenção.
    /// </summary>
    private static SecaoEstabilidade Estabilidade()
    {
        var momento = new DateTimeOffset(2026, 9, 21, 14, 30, 0, TimeSpan.FromHours(-3));
        GrupoEventos Grupo(string codigo, string titulo, params string[] detalhes) =>
            new(codigo, titulo, detalhes.Length, detalhes.Length > 0 ? momento : null, detalhes);

        IReadOnlyList<GrupoEventos> grupos =
        [
            Grupo(Eventos.Estabilidade.TelasAzuis, "Telas azuis", "21/09/2026 14:30: código 0x9F"),
            Grupo(Eventos.Estabilidade.Desligamentos, "Desligamentos inesperados", "21/09/2026 14:30: o botão de energia foi segurado"),
            Grupo(Eventos.Estabilidade.Disco, "Erros de disco"),
            Grupo(Eventos.Estabilidade.SistemaArquivos, "Erros do sistema de arquivos"),
            Grupo(Eventos.Estabilidade.WheaCorrigido, "Erros de hardware corrigidos"),
            Grupo(Eventos.Estabilidade.WheaNaoCorrigido, "Erros de hardware não corrigidos"),
        ];

        return new SecaoEstabilidade(
            30,
            Campo<IReadOnlyList<GrupoEventos>>.Lido(grupos, D),
            Texto("sem erros, em 02/09/2026 09:10"),
            false,
            Campo<double>.Lido(6.2, D),
            Campo<TimeSpan>.Lido(TimeSpan.FromHours(26.5), D),
            Campo<DateTimeOffset>.Lido(new DateTimeOffset(2026, 9, 29, 7, 30, 0, TimeSpan.FromHours(-3)), D),
            Campo<DateTimeOffset>.Lido(new DateTimeOffset(2024, 3, 2, 10, 0, 0, TimeSpan.FromHours(-3)), D));
    }

    /// <summary>Windows 11 com o Secure Boot desligado, para a imagem mostrar o cartão em Atenção.</summary>
    private static VerificacaoWindows11 Windows11() => new(
    [
        new("Processador", EstadoRequisito.Atende, "consta na lista do Windows 11 25H2: Série Exemplo"),
        new("TPM", EstadoRequisito.Atende, "TPM 2.0"),
        new("Firmware", EstadoRequisito.Atende, "UEFI"),
        new("Secure Boot", EstadoRequisito.Configuracao, "Secure Boot desligado: ligar no firmware"),
        new("Memória", EstadoRequisito.Atende, "16 GB instalados"),
        new("Armazenamento", EstadoRequisito.Atende, "1 TB no disco do Windows"),
    ], "25H2");

    /// <summary>Um dispositivo fictício sem driver, para a imagem mostrar o cartão Dispositivos.</summary>
    private static SecaoDispositivos Dispositivos() => new(
        Campo<IReadOnlyList<DispositivoProblema>>.Lido(
            [new DispositivoProblema(Texto("Leitor de Cartão Exemplo"), Texto("Unknown"), 28, ProblemasDispositivo.Embutida.Texto(28))], D),
        Campo<IReadOnlyList<DispositivoProblema>>.Lido([], D),
        Campo<int>.Lido(148, D));

    /// <summary>
    /// Dois discos fictícios: um SSD NVMe Bom e um HDD em Atenção, com setores realocados, para a imagem
    /// mostrar os dois estados. A saúde sai das mesmas regras da coleta real.
    /// </summary>
    private static SecaoDiscos Discos()
    {
        var nvme = new SaudeNvme(0, 38, 100, 10, 4, 21_500_000_000_000m, 12_800_000_000_000m, 412, 3_210, 9, 0, 0, 75);
        var ata = new LeituraSmartAta(
        [
            new AtributoSmart(0x05, 0x33, 99, 99, 36, 3),
            new AtributoSmart(0x09, 0x32, 81, 81, 0, 16_802),
            new AtributoSmart(0x0C, 0x32, 99, 99, 0, 1_204),
            new AtributoSmart(0xC2, 0x22, 39, 52, 0, 39),
            new AtributoSmart(0xC5, 0x12, 100, 100, 0, 0),
        ], false, 7200, false, "SATA 6 Gb/s", null, null);

        VolumeTela Volume(string letra, string rotulo, long livreGb, long totalGb) => new(
            Texto(letra), Texto(rotulo), Texto("NTFS"),
            Campo<long>.Lido(livreGb * 1024 * 1024 * 1024, D), Campo<long>.Lido(totalGb * 1024 * 1024 * 1024, D),
            Campo<string>.RequerAdministrador(D));

        var nomes = AtributosSmart.Embutida;
        var ssd = new DiscoTela(
            0, Texto("SSD NVMe Exemplo 1TB"), Texto("1.0"), Texto("SERIE-DISCO-0001"),
            Campo<long>.Lido(1_000_204_886_016, D), Texto("SSD NVMe"), Texto("NVMe, PCIe 4.0 x4"),
            Campo<int>.NaoSuportado(D, "SSD não tem rotação"), Campo<bool>.Lido(true, D), Texto("GPT"),
            RegrasDisco.Nvme(nvme, null),
            Campo<int>.Lido(38, D), Campo<long>.Lido(3_210, D), Campo<long>.Lido(412, D),
            Campo<decimal>.Lido(nvme.DadosGravadosBytes, D), Campo<int>.Lido(4, D),
            Campo<IReadOnlyList<LinhaSmart>>.Lido([new LinhaSmart("Percentage Used", "Vida usada (%)", "Percentage Used", null, null, null, 4)], D),
            [Volume("C:", "Sistema", 412, 930)]);

        var hdd = new DiscoTela(
            1, Texto("HDD Exemplo 2TB"), Texto("CC43"), Texto("SERIE-DISCO-0002"),
            Campo<long>.Lido(2_000_398_934_016, D), Texto("HDD"), Texto("SATA 6 Gb/s"),
            Campo<int>.Lido(7200, D), Campo<bool>.Lido(false, D), Texto("GPT"),
            RegrasDisco.Ata(ata, TipoDisco.Hdd, nomes),
            Campo<int>.Lido(39, D), Campo<long>.Lido(16_802, D), Campo<long>.Lido(1_204, D),
            Campo<decimal>.NaoInformado(D, "o atributo de dados gravados muda de unidade conforme o fabricante; veja a tabela SMART"),
            Campo<int>.NaoInformado(D, "em disco SATA, a vida usada depende do fabricante; veja a tabela SMART"),
            Campo<IReadOnlyList<LinhaSmart>>.Lido(
                ata.Atributos.Select(a => new LinhaSmart($"{a.Id:X2}h", nomes.Nome(a.Id), nomes.Buscar(a.Id)?.NomeOriginal ?? string.Empty, a.Atual, a.Pior, a.Limite, a.Bruto)).ToList(), D),
            [Volume("D:", "Arquivos", 1_120, 1_862)]);

        return new SecaoDiscos(Campo<IReadOnlyList<DiscoTela>>.Lido([ssd, hdd], D), []);
    }

    /// <summary>
    /// Quatro slots, dois módulos fictícios de 8 GB DDR4 rodando a 2666 com nominal 3200, para a imagem
    /// mostrar o cartão Atenção. Os alertas e a ampliação saem das mesmas regras da coleta real.
    /// </summary>
    private static SecaoMemoria Memoria()
    {
        const long gb = 1024L * 1024 * 1024;
        ModuloTela Modulo(string slot, string serie) => new(
            Texto(slot), Texto("BANK 0"), false,
            Campo<long>.Lido(8 * gb, D), Texto("DDR4"), Texto("DIMM"),
            Campo<int>.Lido(3200, D), Campo<int>.Lido(2666, D),
            Texto("Fabricante Memoria Exemplo"), Texto("PN-TESTE-3200"), Texto(serie),
            Campo<int>.Lido(1, D), Campo<int>.Lido(1200, D));
        ModuloTela Vazio(string slot)
        {
            var texto = Campo<string>.NaoSuportado(D, "slot vazio");
            var numero = Campo<int>.NaoSuportado(D, "slot vazio");
            return new(Texto(slot), Texto("BANK 1"), true, Campo<long>.NaoSuportado(D, "slot vazio"), texto, texto, numero, numero, texto, texto, texto, numero, numero);
        }

        var secao = new SecaoMemoria(
            Campo<long>.Lido(16 * gb, D),
            Campo<long>.Lido((16 * gb) - (256 * 1024 * 1024), D),
            Campo<long>.Lido(256 * 1024 * 1024, D),
            Texto("DDR4"),
            Campo<int>.Lido(4, D),
            Campo<int>.Lido(2, D),
            Campo<long>.Lido(64 * gb, D),
            Texto("nenhuma"),
            Campo<IReadOnlyList<ModuloTela>>.Lido([Modulo("ChannelA-DIMM0", "SERIE-MEM-0001"), Vazio("ChannelA-DIMM1"), Modulo("ChannelB-DIMM0", "SERIE-MEM-0002"), Vazio("ChannelB-DIMM1")], D),
            Campo<long>.Lido(7 * gb, D),
            Campo<long>.Lido(9 * gb, D),
            Campo<int>.Lido(44, D),
            Campo<long>.Lido(10 * gb, D),
            Campo<long>.Lido(24 * gb, D),
            Campo<long>.Lido(4 * gb, D),
            [],
            Campo<string>.NaoInformado(D));

        var ampliacao = AlertasMemoria.Ampliacao(secao);
        return secao with
        {
            Alertas = AlertasMemoria.Calcular(secao),
            Ampliacao = ampliacao.FoiLido ? Texto(ampliacao.Valor!) : ampliacao,
        };
    }

    private static Campo<string> Texto(string valor) => Campo<string>.Lido(valor, D);
}
