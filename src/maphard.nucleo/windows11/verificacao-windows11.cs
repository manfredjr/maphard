using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Formatacao;

namespace MapHard.Nucleo.Windows11;

/// <summary>Como a máquina fica diante de um requisito. "Configuração" é o que se resolve no firmware, sem trocar peça.</summary>
public enum EstadoRequisito
{
    Atende,
    Configuracao,
    NaoAtende,
    Desconhecido,
}

public sealed record ItemWindows11(string Item, EstadoRequisito Estado, string Detalhe);

/// <summary>A verificação do Windows 11, item por item (R35), com a versão da lista de processadores usada.</summary>
public sealed record VerificacaoWindows11(IReadOnlyList<ItemWindows11> Itens, string VersaoLista);

/// <summary>Os seis itens do quadro da tarefa 6 do plano da fatia 5.</summary>
public static class VerificadorWindows11
{
    public const long MemoriaMinima = 4L << 30;
    public const long ArmazenamentoMinimo = 64_000_000_000;

    public static VerificacaoWindows11 Verificar(Campo<string> processador, DadosFirmware firmware, Campo<long> memoria, Campo<long> discoWindows, TabelaWindows11 tabela) =>
        new([
            Processador(processador, tabela),
            Tpm(firmware.TpmVersao),
            Firmware(firmware.Modo),
            SecureBoot(firmware.SecureBoot, firmware.Modo),
            Minimo("Memória", memoria, MemoriaMinima, Formatador.Bytes, "instalados"),
            Minimo("Armazenamento", discoWindows, ArmazenamentoMinimo, b => Formatador.BytesDecimais(b), "no disco do Windows"),
        ], tabela.Versao);

    /// <summary>O tamanho do disco que tem a letra do Windows ("C:"). Sem o disco, não informado.</summary>
    public static Campo<long> DiscoDoWindows(SecaoDiscos discos, string letraWindows)
    {
        var disco = discos.Discos.Valor?.FirstOrDefault(d => d.Volumes.Any(v => string.Equals(v.Letra.Valor, letraWindows, StringComparison.OrdinalIgnoreCase)));
        return disco?.Tamanho ?? Campo<long>.NaoInformado(FonteDado.Armazenamento, $"disco do {letraWindows} não achado");
    }

    private static ItemWindows11 Processador(Campo<string> processador, TabelaWindows11 tabela)
    {
        if (!processador.FoiLido)
        {
            return new("Processador", EstadoRequisito.Desconhecido, "processador não lido");
        }

        var p = tabela.Verificar(processador.Valor);
        return p.Situacao switch
        {
            SituacaoProcessador.NaLista => new("Processador", EstadoRequisito.Atende, $"consta na lista do Windows 11 {p.Versao}: {p.Serie}"),
            SituacaoProcessador.GeracaoAnterior => new("Processador", EstadoRequisito.NaoAtende, $"geração anterior à primeira da lista do Windows 11 {p.Versao}"),
            _ => new("Processador", EstadoRequisito.Desconhecido, $"não consta na lista do MapHard (Windows 11 {p.Versao}); a Microsoft também aceita processadores lançados depois da lista"),
        };
    }

    private static ItemWindows11 Tpm(Campo<string> versao) => versao switch
    {
        { FoiLido: true, Valor: "2.0" } => new("TPM", EstadoRequisito.Atende, "TPM 2.0"),
        { FoiLido: true, Valor: "1.2" } => new("TPM", EstadoRequisito.NaoAtende, "TPM 1.2; o Windows 11 pede a versão 2.0"),
        { Estado: EstadoCampo.NaoSuportado } => new("TPM", EstadoRequisito.Configuracao, "TPM não encontrado: pode estar desligado no firmware"),
        _ => new("TPM", EstadoRequisito.Desconhecido, "TPM não lido"),
    };

    private static ItemWindows11 Firmware(Campo<string> modo) => modo switch
    {
        { FoiLido: true, Valor: "UEFI" } => new("Firmware", EstadoRequisito.Atende, "UEFI"),
        { FoiLido: true, Valor: "BIOS legado" } => new("Firmware", EstadoRequisito.Configuracao, "BIOS legado: mudar para UEFI no firmware, com conversão do disco ou reinstalação"),
        _ => new("Firmware", EstadoRequisito.Desconhecido, "modo do firmware não lido"),
    };

    private static ItemWindows11 SecureBoot(Campo<bool> secureBoot, Campo<string> modo) => secureBoot switch
    {
        { FoiLido: true, Valor: true } => new("Secure Boot", EstadoRequisito.Atende, "ligado"),
        { FoiLido: true, Valor: false } => new("Secure Boot", EstadoRequisito.Configuracao, "Secure Boot desligado: ligar no firmware"),
        _ when modo is { FoiLido: true, Valor: "BIOS legado" } => new("Secure Boot", EstadoRequisito.Configuracao, "só existe em UEFI: ligar depois de mudar o firmware para UEFI"),
        _ => new("Secure Boot", EstadoRequisito.Desconhecido, "Secure Boot não lido"),
    };

    private static ItemWindows11 Minimo(string item, Campo<long> valor, long minimo, Func<long, string> formatar, string onde) =>
        !valor.FoiLido ? new(item, EstadoRequisito.Desconhecido, $"{item.ToLowerInvariant()} não lida")
        : valor.Valor >= minimo ? new(item, EstadoRequisito.Atende, $"{formatar(valor.Valor)} {onde}")
        : new(item, EstadoRequisito.NaoAtende, $"{formatar(valor.Valor)} {onde}; o mínimo é {formatar(minimo)}");
}
