using System.Runtime.Intrinsics.X86;

namespace MapHard.Nucleo.Cpuid;

/// <summary>Os quatro registradores que a instrução CPUID devolve.</summary>
public readonly record struct RegistrosCpuid(uint Eax, uint Ebx, uint Ecx, uint Edx);

/// <summary>De onde vêm as respostas do CPUID. Os testes trocam por respostas montadas à mão.</summary>
public interface IFonteCpuid
{
    /// <summary>False quando o processador ou o sistema não deixam executar a instrução.</summary>
    bool Disponivel { get; }

    RegistrosCpuid Ler(uint funcao, uint subfuncao = 0);
}

/// <summary>Executa a instrução CPUID pelo próprio .NET, sem driver e sem administrador.</summary>
public sealed class FonteCpuidReal : IFonteCpuid
{
    public bool Disponivel => X86Base.IsSupported;

    public RegistrosCpuid Ler(uint funcao, uint subfuncao = 0)
    {
        var (eax, ebx, ecx, edx) = X86Base.CpuId(unchecked((int)funcao), unchecked((int)subfuncao));
        return new RegistrosCpuid(unchecked((uint)eax), unchecked((uint)ebx), unchecked((uint)ecx), unchecked((uint)edx));
    }
}
