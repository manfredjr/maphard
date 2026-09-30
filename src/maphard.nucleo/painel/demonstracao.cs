using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Firmware;
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

        var identificacao = new Identificacao(
            Texto("ESTACAO-EXEMPLO"),
            Texto("Fabricante Exemplo"),
            Texto("Modelo Exemplo"),
            Texto("SERIE-TESTE-0001"),
            processador.Nome,
            Campo<long>.Lido(16L * 1024 * 1024 * 1024, D),
            windows);

        return new ColetaMaquina(
            ColetaMaquina.NomeFormato,
            ColetaMaquina.VersaoAtual,
            Coletor.VersaoPrograma,
            new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.FromHours(-3)),
            false,
            identificacao,
            processador,
            placa);
    }

    private static Campo<string> Texto(string valor) => Campo<string>.Lido(valor, D);
}
