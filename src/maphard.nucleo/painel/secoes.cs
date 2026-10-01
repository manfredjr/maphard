using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;
using MapHard.Nucleo.Formatacao;
using MapHard.Nucleo.Processador;

namespace MapHard.Nucleo.Painel;

/// <summary>Uma linha da tela: rótulo, valor em texto, estado (para a cor) e a dica com a fonte e o motivo.</summary>
public sealed record LinhaTela(string Rotulo, string Texto, EstadoCampo Estado, string Dica)
{
    public bool Lido => Estado == EstadoCampo.Lido;
}

/// <summary>Um cartão da tela, com título e linhas.</summary>
public sealed record CartaoTela(string Titulo, IReadOnlyList<LinhaTela> Linhas);

/// <summary>Uma seção da navegação à esquerda.</summary>
public sealed record SecaoTela(string Id, string Titulo, IReadOnlyList<CartaoTela> Cartoes);

/// <summary>Transforma a coleta nas seções da tela. Toda linha passa pelo texto de estado: nada fica vazio.</summary>
public static class MontadorSecoes
{
    public const string Resumo = "resumo";
    public const string Processador = "processador";
    public const string Placa = "placa";

    public static IReadOnlyList<SecaoTela> Montar(ColetaMaquina c, DateOnly hoje) =>
    [
        new(Resumo, "Resumo", MontarResumo(c)),
        new(Processador, "Processador", MontarProcessador(c.Processador)),
        new(Placa, "Placa-mãe e firmware", MontarPlaca(c.Placa, hoje)),
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
                Linha("Memória utilizável", id.MemoriaUtilizavel, Formatador.Bytes),
            ]),
            new("Windows",
            [
                Linha("Sistema", id.Windows.Nome),
                Linha("Versão", id.Windows.Versao),
                Linha("Compilação", id.Windows.Compilacao),
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
