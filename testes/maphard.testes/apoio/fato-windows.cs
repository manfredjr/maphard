namespace MapHard.Testes.Apoio;

/// <summary>
/// Teste que chama a API real do Windows. Fora do Windows (a máquina Linux da fase de nuvem),
/// o teste é pulado com o motivo. O CI em Windows roda todos.
/// </summary>
public sealed class FatoWindowsAttribute : FactAttribute
{
    public FatoWindowsAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "só roda no Windows";
        }
    }
}
