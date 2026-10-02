using MapHard.Nucleo.Baterias;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Cpuid;
using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Dispositivos;
using MapHard.Nucleo.Eventos;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Memoria;
using MapHard.Nucleo.Processador;
using MapHard.Nucleo.Rede;
using MapHard.Nucleo.Smbios;
using MapHard.Nucleo.Video;
using MapHard.Nucleo.Windows;
using MapHard.Nucleo.Windows11;

namespace MapHard.Nucleo.Coleta;

/// <summary>Resumo da máquina, no topo da tela e do relatório (R2, com o que as fatias 1 e 2 já leem).</summary>
public sealed record Identificacao(
    Campo<string> Computador,
    Campo<string> Fabricante,
    Campo<string> Modelo,
    Campo<string> NumeroSerie,
    Campo<string> Processador,
    Campo<long> MemoriaInstalada,
    Campo<string> MemoriaTipo,
    Campo<long> MemoriaUtilizavel,
    Campo<string> Discos,
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
    Campo<bool> Hipervisor,
    Campo<bool> HyperVPedido);

/// <summary>Seção Placa-mãe e firmware (R16 a R19).</summary>
public sealed record SecaoPlaca(
    Campo<string> PlacaFabricante,
    Campo<string> PlacaModelo,
    Campo<string> PlacaVersao,
    Campo<string> PlacaNumeroSerie,
    Campo<string> Chipset,
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
    SecaoMemoria Memoria,
    SecaoDiscos Discos,
    SecaoPlaca Placa,
    SecaoEstabilidade Estabilidade,
    SecaoDispositivos Dispositivos,
    Campo<IReadOnlyList<PlacaVideo>> Video,
    Campo<IReadOnlyList<MonitorVideo>> Monitores,
    Campo<IReadOnlyList<Bateria>> Bateria,
    Campo<IReadOnlyList<PlacaRede>> Rede,
    VerificacaoWindows11 Windows11)
{
    public const string NomeFormato = "maphard-coleta";

    /// <summary>6 a partir da fatia 6, que acrescenta ao processador se o Hyper-V ou o WSL está instalado.</summary>
    public const int VersaoAtual = 6;
}
