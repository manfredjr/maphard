using MapHard.Nucleo.Memoria;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class MemoriaFonteTestes
{
    [FatoWindows]
    public void Instalada_real_e_maior_ou_igual_a_utilizavel()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var fonte = new FonteMemoriaWindows();
        var instalada = fonte.InstaladaKb();
        var estado = fonte.Estado();

        Assert.NotNull(estado);
        Assert.True(estado.TotalBytes > 0);
        Assert.InRange(estado.DisponivelBytes, 0, estado.TotalBytes);
        Assert.InRange(estado.CargaPercentual, 0, 100);
        if (instalada is { } kb)
        {
            Assert.True(kb * 1024 >= estado.TotalBytes);
        }
    }

    [FatoWindows]
    public void Desempenho_real_traz_o_limite_de_memoria_confirmada()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var estado = new FonteMemoriaWindows().Estado()!;

        Assert.NotNull(estado.LimiteConfirmadaBytes);
        Assert.True(estado.LimiteConfirmadaBytes >= estado.ConfirmadaBytes);
        Assert.True(estado.CacheBytes > 0);
    }
}
