using System.Runtime.InteropServices;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Windows;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class AtivacaoTestes
{
    [Theory]
    [InlineData(0, "ativado")]
    [InlineData(1, "licença inválida")]
    [InlineData(2, "licença adulterada")]
    [InlineData(3, "sem conexão para confirmar")]
    public void Estados_da_documentacao(int estado, string esperado)
    {
        Assert.Equal(esperado, LeitorAtivacao.Interpretar(estado).Valor);
    }

    [Fact]
    public void Estado_desconhecido_fica_nao_informado()
    {
        var c = LeitorAtivacao.Interpretar(4);

        Assert.Equal(EstadoCampo.NaoInformado, c.Estado);
        Assert.Equal("estado 4 fora da documentação", c.Motivo);
    }

    [Theory]
    [InlineData(Architecture.X64, "x64")]
    [InlineData(Architecture.Arm64, "ARM64")]
    public void Arquitetura_do_windows(Architecture arquitetura, string esperado)
    {
        Assert.Equal(esperado, LeitorAtivacao.Arquitetura(arquitetura).Valor);
    }

    [FatoWindows]
    public void Leitura_real_da_um_estado_conhecido()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.InRange(new FonteAtivacaoWindows().Estado(), 0, 4);
    }
}
