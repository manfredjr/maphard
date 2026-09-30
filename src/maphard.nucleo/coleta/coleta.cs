using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Cpuid;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Processador;
using MapHard.Nucleo.Smbios;
using MapHard.Nucleo.Windows;

namespace MapHard.Nucleo.Coleta;

/// <summary>Resumo da máquina, no topo da tela e do relatório (R2, com o que a fatia 1 já lê).</summary>
public sealed record Identificacao(
    Campo<string> Computador,
    Campo<string> Fabricante,
    Campo<string> Modelo,
    Campo<string> NumeroSerie,
    Campo<string> Processador,
    Campo<long> MemoriaUtilizavel,
    DadosWindows Windows);

/// <summary>Seção Processador (R3 a R8).</summary>
public sealed record SecaoProcessador(
    Campo<string> Nome,
    Campo<string> Fabricante,
    Campo<string> Assinatura,
    Campo<string> Codinome,
    Campo<string> Litografia,
    Campo<string> Soquete,
    Campo<string> Microcodigo,
    Campo<int> Nucleos,
    Campo<int> Threads,
    Campo<int> NucleosDesempenho,
    Campo<int> NucleosEficiencia,
    Campo<IReadOnlyList<CacheCpu>> Caches,
    Campo<IReadOnlyList<string>> Instrucoes,
    Campo<int> NivelX8664,
    ClocksCpu Clocks,
    Campo<bool> VirtualizacaoNoProcessador,
    Campo<bool> VirtualizacaoLigada,
    Campo<bool> Hipervisor);

/// <summary>Seção Placa-mãe e firmware (R16 sem o chipset, R17 a R19).</summary>
public sealed record SecaoPlaca(
    Campo<string> PlacaFabricante,
    Campo<string> PlacaModelo,
    Campo<string> PlacaVersao,
    Campo<string> PlacaNumeroSerie,
    Campo<string> EquipamentoFabricante,
    Campo<string> EquipamentoModelo,
    Campo<string> EquipamentoFamilia,
    Campo<string> EquipamentoSku,
    Campo<string> EquipamentoNumeroSerie,
    Campo<string> EquipamentoUuid,
    Campo<string> Gabinete,
    Campo<bool> MaquinaVirtual,
    Campo<string> BiosFabricante,
    Campo<string> BiosVersao,
    Campo<DateOnly> BiosData,
    DadosFirmware Firmware);

/// <summary>Uma coleta inteira. É o que a tela mostra e o que o JSON grava.</summary>
public sealed record ColetaMaquina(
    string Formato,
    int VersaoFormato,
    string VersaoPrograma,
    DateTimeOffset ColetadoEm,
    bool Administrador,
    Identificacao Identificacao,
    SecaoProcessador Processador,
    SecaoPlaca Placa)
{
    public const string NomeFormato = "maphard-coleta";
    public const int VersaoAtual = 1;
}
