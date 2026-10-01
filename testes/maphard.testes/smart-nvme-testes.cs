using System.Buffers.Binary;
using MapHard.Nucleo.Discos;
using MapHard.Nucleo.Smart;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class SmartNvmeTestes
{
    /// <summary>Log de saúde de 512 bytes montado à mão, com valores fictícios.</summary>
    internal static byte[] Log(
        byte alerta = 0,
        ushort kelvin = 315,
        byte reserva = 100,
        byte limiteReserva = 10,
        byte usado = 3,
        ulong lidas = 2_000_000,
        ulong gravadas = 74_609_375,
        ulong ciclos = 514,
        ulong horas = 12_345,
        ulong inseguros = 21,
        ulong errosMidia = 0,
        ulong entradas = 7)
    {
        var log = new byte[512];
        log[0] = alerta;
        BinaryPrimitives.WriteUInt16LittleEndian(log.AsSpan(1), kelvin);
        log[3] = reserva;
        log[4] = limiteReserva;
        log[5] = usado;
        BinaryPrimitives.WriteUInt64LittleEndian(log.AsSpan(32), lidas);
        BinaryPrimitives.WriteUInt64LittleEndian(log.AsSpan(48), gravadas);
        BinaryPrimitives.WriteUInt64LittleEndian(log.AsSpan(112), ciclos);
        BinaryPrimitives.WriteUInt64LittleEndian(log.AsSpan(128), horas);
        BinaryPrimitives.WriteUInt64LittleEndian(log.AsSpan(144), inseguros);
        BinaryPrimitives.WriteUInt64LittleEndian(log.AsSpan(160), errosMidia);
        BinaryPrimitives.WriteUInt64LittleEndian(log.AsSpan(176), entradas);
        return log;
    }

    internal static byte[] Identificacao(ushort wctempKelvin)
    {
        var id = new byte[4096];
        BinaryPrimitives.WriteUInt16LittleEndian(id.AsSpan(266), wctempKelvin);
        return id;
    }

    [Fact]
    public void Cada_campo_no_deslocamento_da_documentacao()
    {
        var s = LeitorSaudeNvme.Interpretar(Log())!;

        Assert.Equal(42, s.TemperaturaC);
        Assert.Equal(100, s.ReservaDisponivel);
        Assert.Equal(10, s.LimiteReserva);
        Assert.Equal(3, s.PercentualUsado);
        Assert.Equal(2_000_000m * 512_000m, s.DadosLidosBytes);
        Assert.Equal(74_609_375m * 512_000m, s.DadosGravadosBytes);
        Assert.Equal(514, s.CiclosEnergia);
        Assert.Equal(12_345, s.HorasLigado);
        Assert.Equal(21, s.DesligamentosInseguros);
        Assert.Equal(0, s.ErrosMidia);
        Assert.Equal(7, s.EntradasRegistroErros);
        Assert.Empty(s.Alertas);
        Assert.Null(s.LimiteAvisoTemperaturaC);
    }

    [Fact]
    public void Contador_de_16_bytes_maior_que_ulong_nao_estoura()
    {
        var log = Log();
        log[128 + 8] = 1;

        Assert.Equal((decimal)ulong.MaxValue + 1 + 12_345, LeitorSaudeNvme.Interpretar(log)!.HorasLigado);
    }

    [Fact]
    public void Contador_acima_do_decimal_satura()
    {
        var log = Log();
        log.AsSpan(128, 16).Fill(0xFF);

        Assert.Equal(decimal.MaxValue, LeitorSaudeNvme.Interpretar(log)!.HorasLigado);
    }

    [Fact]
    public void Temperatura_zero_vira_nula()
    {
        Assert.Null(LeitorSaudeNvme.Interpretar(Log(kelvin: 0))!.TemperaturaC);
    }

    [Fact]
    public void Buffer_curto_ou_nulo_devolve_nulo()
    {
        Assert.Null(LeitorSaudeNvme.Interpretar(new byte[100]));
        Assert.Null(LeitorSaudeNvme.Interpretar(null));
    }

    [Theory]
    [InlineData(0x01, "reserva abaixo do limite")]
    [InlineData(0x02, "temperatura fora da faixa")]
    [InlineData(0x04, "confiabilidade degradada")]
    [InlineData(0x08, "mídia só de leitura")]
    [InlineData(0x10, "falha na memória de reserva volátil")]
    public void Cada_bit_do_alerta_critico_vira_o_texto(byte bit, string texto)
    {
        Assert.Equal([texto], LeitorSaudeNvme.Interpretar(Log(alerta: bit))!.Alertas);
    }

    [Fact]
    public void Bits_reservados_do_alerta_nao_geram_texto()
    {
        Assert.Equal(["reserva abaixo do limite"], LeitorSaudeNvme.TextosAlerta(0xE1));
    }

    [Fact]
    public void Limite_de_aviso_de_temperatura_vem_da_identificacao()
    {
        Assert.Equal(75, LeitorSaudeNvme.Interpretar(Log(), Identificacao(348))!.LimiteAvisoTemperaturaC);
        Assert.Null(LeitorSaudeNvme.Interpretar(Log(), Identificacao(0))!.LimiteAvisoTemperaturaC);
        Assert.Null(LeitorSaudeNvme.Interpretar(Log(), new byte[100])!.LimiteAvisoTemperaturaC);
    }

    [FatoWindows]
    public void Nvme_real_tem_temperatura_e_percentual_usado_sem_administrador()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var fonte = new FonteDiscosWindows();
        var nvme = fonte.Discos().FirstOrDefault(d => DescritorArmazenamento.Interpretar(d.Descritor)?.Barramento == DescritorArmazenamento.BarramentoNvme);
        if (nvme is null)
        {
            return;
        }

        var s = LeitorSaudeNvme.Interpretar(fonte.LogSaudeNvme(nvme.Numero), fonte.IdentificacaoNvme(nvme.Numero));

        Assert.NotNull(s);
        Assert.InRange(s.TemperaturaC!.Value, 0, 100);
        Assert.InRange(s.PercentualUsado, 0, 255);
    }
}
