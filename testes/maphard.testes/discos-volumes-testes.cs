using MapHard.Nucleo.Discos;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class DiscosVolumesTestes
{
    private static VolumeBruto Volume(string letra, int? disco, int? bitLocker = null, string? falha = Volumes.RequerAdministrador) =>
        new(letra, "Rotulo", "NTFS", 100L * 1024 * 1024 * 1024, 500L * 1024 * 1024 * 1024, disco, bitLocker, falha);

    [Fact]
    public void Volumes_ligados_ao_disco_pelo_numero_em_ordem_de_letra()
    {
        VolumeBruto[] volumes = [Volume("D:", 0), Volume("C:", 0), Volume("E:", 1)];

        Assert.Equal(["C:", "D:"], Volumes.DoDisco(volumes, 0).Select(v => v.Letra));
        Assert.Equal(["E:"], Volumes.DoDisco(volumes, 1).Select(v => v.Letra));
    }

    [Fact]
    public void Volume_sem_disco_ou_com_disco_desconhecido_fica_a_parte()
    {
        VolumeBruto[] volumes = [Volume("C:", 0), Volume("F:", null), Volume("G:", 7)];

        Assert.Equal(["F:", "G:"], Volumes.SemDisco(volumes, [0, 1]).Select(v => v.Letra));
    }

    [Theory]
    [InlineData(0, "desligado")]
    [InlineData(1, "ligado")]
    [InlineData(2, "não foi possível saber (volume bloqueado?)")]
    [InlineData(null, null)]
    public void Nome_do_estado_do_bitlocker(int? protecao, string? esperado)
    {
        Assert.Equal(esperado, Volumes.NomeBitLocker(protecao));
    }

    [FatoWindows]
    public void Unidade_do_windows_aparece_ligada_a_um_disco_e_sem_administrador_o_bitlocker_pede_elevacao()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var letra = Path.GetPathRoot(Environment.SystemDirectory)![..2];

        var sistema = Assert.Single(new FonteVolumesWindows().Ler(administrador: false), v => v.Letra.Equals(letra, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(sistema.Disco);
        Assert.True(sistema.TotalBytes > 0);
        Assert.Null(sistema.ProtecaoBitLocker);
        Assert.Equal(Volumes.RequerAdministrador, sistema.FalhaBitLocker);
    }
}
