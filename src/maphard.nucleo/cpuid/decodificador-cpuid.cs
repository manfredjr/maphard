using System.Buffers.Binary;
using System.Text;

namespace MapHard.Nucleo.Cpuid;

/// <summary>O que o CPUID diz do processador.</summary>
public sealed record IdentidadeCpu(
    string Fabricante,
    int Familia,
    int Modelo,
    int Revisao,
    string? NomeComercial,
    IReadOnlyList<string> Instrucoes,
    int? NivelX8664,
    bool VirtualizacaoNoProcessador,
    bool HipervisorPresente,
    bool? Hibrido,
    int? ClockBaseMhz,
    int? ClockMaximoMhz)
{
    public bool EhIntel => Fabricante == "GenuineIntel";

    public bool EhAmd => Fabricante == "AuthenticAMD";

    /// <summary>Nome do fabricante para a tela.</summary>
    public string NomeFabricante => Fabricante switch
    {
        "GenuineIntel" => "Intel",
        "AuthenticAMD" => "AMD",
        _ => Fabricante,
    };

    /// <summary>Família, modelo e revisão no formato usual, em hexadecimal: "6, 9E, A".</summary>
    public string Assinatura => $"{Familia:X}, {Modelo:X}, {Revisao:X}";
}

/// <summary>
/// Decodifica o CPUID. Posições dos bits conferidas no arch/x86/include/asm/cpufeatures.h do
/// kernel Linux, que as agrupa por função e registrador:
/// função 1 EDX (palavra 0), função 0x80000001 EDX (palavra 1), função 1 ECX (palavra 4),
/// função 0x80000001 ECX (palavra 6), função 7 EBX (palavra 9) e função 7 EDX (palavra 18).
/// </summary>
public static class DecodificadorCpuid
{
    private const uint FuncaoEstendida = 0x80000000;

    private sealed record Bit(string Nome, uint Funcao, char Registrador, int Posicao);

    // Na ordem em que aparecem na tela.
    private static readonly Bit[] _instrucoes =
    [
        new("MMX", 1, 'd', 23),
        new("SSE", 1, 'd', 25),
        new("SSE2", 1, 'd', 26),
        new("SSE3", 1, 'c', 0),
        new("SSSE3", 1, 'c', 9),
        new("SSE4.1", 1, 'c', 19),
        new("SSE4.2", 1, 'c', 20),
        new("x86-64", 0x80000001, 'd', 29),
        new("AES", 1, 'c', 25),
        new("PCLMULQDQ", 1, 'c', 1),
        new("AVX", 1, 'c', 28),
        new("AVX2", 7, 'b', 5),
        new("FMA3", 1, 'c', 12),
        new("F16C", 1, 'c', 29),
        new("BMI1", 7, 'b', 3),
        new("BMI2", 7, 'b', 8),
        new("SHA", 7, 'b', 29),
        new("AVX-512F", 7, 'b', 16),
        new("AVX-512DQ", 7, 'b', 17),
        new("AVX-512CD", 7, 'b', 28),
        new("AVX-512BW", 7, 'b', 30),
        new("AVX-512VL", 7, 'b', 31),
        new("RDRAND", 1, 'c', 30),
        new("VT-x", 1, 'c', 5),
        new("AMD-V", 0x80000001, 'c', 2),
    ];

    // Níveis da ABI x86-64 (psABI). O nível 1 é o x86-64 básico.
    private static readonly string[] _nivel2 = ["CMPXCHG16B", "LAHF-SAHF", "POPCNT", "SSE3", "SSSE3", "SSE4.1", "SSE4.2"];
    private static readonly string[] _nivel3 = ["AVX", "AVX2", "BMI1", "BMI2", "F16C", "FMA3", "LZCNT", "MOVBE", "OSXSAVE"];
    private static readonly string[] _nivel4 = ["AVX-512F", "AVX-512BW", "AVX-512CD", "AVX-512DQ", "AVX-512VL"];

    // Bits usados só no cálculo do nível, fora da lista da tela.
    private static readonly Bit[] _auxiliares =
    [
        new("CMPXCHG16B", 1, 'c', 13),
        new("LAHF-SAHF", 0x80000001, 'c', 0),
        new("POPCNT", 1, 'c', 23),
        new("LZCNT", 0x80000001, 'c', 5),
        new("MOVBE", 1, 'c', 22),
        new("OSXSAVE", 1, 'c', 27),
    ];

    public static IdentidadeCpu? Decodificar(IFonteCpuid fonte)
    {
        if (!fonte.Disponivel)
        {
            return null;
        }

        var f0 = fonte.Ler(0);
        var maiorBasica = f0.Eax;
        var fabricante = Texto(f0.Ebx, f0.Edx, f0.Ecx);
        var maiorEstendida = fonte.Ler(FuncaoEstendida).Eax;

        RegistrosCpuid? Ler(uint funcao)
        {
            var disponivel = funcao >= FuncaoEstendida ? maiorEstendida >= funcao : maiorBasica >= funcao;
            return disponivel ? fonte.Ler(funcao) : null;
        }

        var f1 = Ler(1);
        var f7 = Ler(7);
        var e1 = Ler(0x80000001);
        var respostas = new Dictionary<uint, RegistrosCpuid?> { [1] = f1, [7] = f7, [0x80000001] = e1 };

        bool Tem(Bit bit) => respostas[bit.Funcao] is { } r && ((Registrador(r, bit.Registrador) >> bit.Posicao) & 1) == 1;

        var (familia, modelo, revisao) = Assinatura(f1?.Eax ?? 0);
        var presentes = _instrucoes.Where(Tem).Select(b => b.Nome).ToList();
        var todas = presentes.Concat(_auxiliares.Where(Tem).Select(b => b.Nome)).ToHashSet();

        var f16 = Ler(0x16);
        return new IdentidadeCpu(
            fabricante,
            familia,
            modelo,
            revisao,
            NomeComercial(Ler(0x80000002), Ler(0x80000003), Ler(0x80000004)),
            presentes,
            NivelX8664(todas),
            todas.Contains("VT-x") || todas.Contains("AMD-V"),
            f1 is { } um && ((um.Ecx >> 31) & 1) == 1,
            f7 is { } sete ? ((sete.Edx >> 15) & 1) == 1 : null,
            f16 is { Eax: > 0 } base16 ? (int)(base16.Eax & 0xFFFF) : null,
            f16 is { Ebx: > 0 } max16 ? (int)(max16.Ebx & 0xFFFF) : null);
    }

    /// <summary>
    /// Função 1, EAX: revisão nos bits 0 a 3, modelo 4 a 7, família 8 a 11, modelo estendido 16 a 19,
    /// família estendida 20 a 27. A família estendida soma quando a família é 0xF; o modelo estendido
    /// entra quando a família é 6 ou 0xF.
    /// </summary>
    public static (int Familia, int Modelo, int Revisao) Assinatura(uint eax)
    {
        var revisao = (int)(eax & 0xF);
        var modelo = (int)((eax >> 4) & 0xF);
        var familia = (int)((eax >> 8) & 0xF);
        var modeloEstendido = (int)((eax >> 16) & 0xF);
        var familiaEstendida = (int)((eax >> 20) & 0xFF);

        var familiaExibida = familia == 0xF ? familia + familiaEstendida : familia;
        var modeloExibido = familia is 0x6 or 0xF ? (modeloEstendido << 4) + modelo : modelo;
        return (familiaExibida, modeloExibido, revisao);
    }

    internal static int? NivelX8664(IReadOnlySet<string> instrucoes)
    {
        if (!instrucoes.Contains("x86-64"))
        {
            return null;
        }

        if (!_nivel2.All(instrucoes.Contains))
        {
            return 1;
        }

        if (!_nivel3.All(instrucoes.Contains))
        {
            return 2;
        }

        return _nivel4.All(instrucoes.Contains) ? 4 : 3;
    }

    private static string? NomeComercial(RegistrosCpuid? a, RegistrosCpuid? b, RegistrosCpuid? c)
    {
        if (a is null || b is null || c is null)
        {
            return null;
        }

        var texto = string.Concat(new[] { a.Value, b.Value, c.Value }.Select(r => Texto(r.Eax, r.Ebx, r.Ecx, r.Edx)));
        var limpo = string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return limpo.Length == 0 ? null : limpo;
    }

    /// <summary>Registradores em little-endian, quatro caracteres cada, até o primeiro zero.</summary>
    private static string Texto(params uint[] registradores)
    {
        var bytes = new byte[registradores.Length * 4];
        for (var i = 0; i < registradores.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), registradores[i]);
        }

        var fim = Array.IndexOf(bytes, (byte)0);
        bytes = fim < 0 ? bytes : bytes[..fim];
        return Encoding.ASCII.GetString(bytes);
    }

    private static uint Registrador(RegistrosCpuid r, char nome) => nome switch
    {
        'a' => r.Eax,
        'b' => r.Ebx,
        'c' => r.Ecx,
        _ => r.Edx,
    };
}
