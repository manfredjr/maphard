using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Processador;
using MapHard.Nucleo.Windows;

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
        var windows = new DadosWindows(Texto("Windows 11 Pro"), Texto("23H2"), Texto("22631.4169"));
        var firmware = new DadosFirmware(
            Texto("UEFI"),
            Campo<bool>.Lido(true, D),
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

        var identificacao = new Identificacao(
            Texto("ESTACAO-EXEMPLO"),
            Texto("Fabricante Exemplo"),
            Texto("Modelo Exemplo"),
            Texto("SERIE-TESTE-0001"),
            processador.Nome,
            memoria.Instalada,
            memoria.Tipo,
            memoria.Utilizavel,
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
            placa);
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
