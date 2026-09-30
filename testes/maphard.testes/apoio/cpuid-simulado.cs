using System.Buffers.Binary;
using System.Text;
using MapHard.Nucleo.Cpuid;

namespace MapHard.Testes.Apoio;

/// <summary>Responde ao CPUID a partir de um dicionário montado no teste. Função não cadastrada devolve zeros.</summary>
internal sealed class CpuidSimulado : IFonteCpuid
{
    private readonly Dictionary<uint, RegistrosCpuid> _respostas = [];

    public bool Disponivel { get; init; } = true;

    public RegistrosCpuid Ler(uint funcao, uint subfuncao = 0) => _respostas.GetValueOrDefault(funcao);

    public CpuidSimulado Com(uint funcao, uint eax = 0, uint ebx = 0, uint ecx = 0, uint edx = 0)
    {
        _respostas[funcao] = new RegistrosCpuid(eax, ebx, ecx, edx);
        return this;
    }

    /// <summary>Liga um bit na resposta já cadastrada.</summary>
    public CpuidSimulado Bit(uint funcao, char registrador, int posicao)
    {
        var r = _respostas.GetValueOrDefault(funcao);
        var mascara = 1u << posicao;
        _respostas[funcao] = registrador switch
        {
            'a' => r with { Eax = r.Eax | mascara },
            'b' => r with { Ebx = r.Ebx | mascara },
            'c' => r with { Ecx = r.Ecx | mascara },
            _ => r with { Edx = r.Edx | mascara },
        };
        return this;
    }

    /// <summary>Processador base: fabricante, maior função básica 7, maior estendida 0x80000004 e a assinatura.</summary>
    public static CpuidSimulado Base(string fabricante = "GenuineIntel", uint assinatura = 0x000906EA, uint maiorBasica = 7, uint maiorEstendida = 0x80000004)
    {
        var f = Registradores(fabricante, 12);
        return new CpuidSimulado()
            .Com(0, maiorBasica, f[0], f[2], f[1])
            .Com(1, assinatura)
            .Com(7)
            .Com(0x80000000, maiorEstendida)
            .Com(0x80000001);
    }

    /// <summary>Empacota o nome comercial nas funções 0x80000002 a 0x80000004.</summary>
    public CpuidSimulado NomeComercial(string nome)
    {
        var r = Registradores(nome, 48);
        Com(0x80000002, r[0], r[1], r[2], r[3]);
        Com(0x80000003, r[4], r[5], r[6], r[7]);
        Com(0x80000004, r[8], r[9], r[10], r[11]);
        return this;
    }

    private static uint[] Registradores(string texto, int tamanho)
    {
        var bytes = new byte[tamanho];
        Encoding.ASCII.GetBytes(texto).CopyTo(bytes, 0);
        return Enumerable.Range(0, tamanho / 4).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4))).ToArray();
    }
}
