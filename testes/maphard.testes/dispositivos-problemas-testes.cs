using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Dispositivos;
using MapHard.Nucleo.Tabelas;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class DispositivosProblemasTestes
{
    private static readonly ProblemasDispositivo Textos = ProblemasDispositivo.Ler(
        "codigo;constante;texto;fonte\n22;CM_PROB_DISABLED;O dispositivo está desativado.;f\n28;CM_PROB_FAILED_INSTALL;O driver do dispositivo não está instalado.;f\n");

    private static DispositivoBruto D(string? nome, uint? codigo, string classe = "Exemplo") => new(nome, classe, [], codigo, null);

    [Fact]
    public void So_entra_dispositivo_com_codigo_diferente_de_zero()
    {
        var s = Problemas.Montar([D("Teclado Exemplo", 0), D("Leitor Exemplo", 28), D("Mouse Exemplo", null)], null, Textos);

        var p = Assert.Single(s.ComProblema.Valor!);
        Assert.Equal("Leitor Exemplo", p.Nome.Valor);
        Assert.Equal(28, p.Codigo);
        Assert.Equal("O driver do dispositivo não está instalado.", p.Texto);
        Assert.Equal(3, s.Total.Valor);
    }

    [Fact]
    public void Desativado_fica_separado()
    {
        var s = Problemas.Montar([D("Placa Exemplo", 22), D("Leitor Exemplo", 28)], null, Textos);

        Assert.Equal(["Leitor Exemplo"], s.ComProblema.Valor!.Select(d => d.Nome.Valor));
        Assert.Equal(["Placa Exemplo"], s.Desativados.Valor!.Select(d => d.Nome.Valor));
    }

    [Fact]
    public void Sem_nome_fica_nao_informado_e_codigo_desconhecido_tem_texto_generico()
    {
        var p = Assert.Single(Problemas.Montar([D(null, 99)], null, Textos).ComProblema.Valor!);

        Assert.Equal(EstadoCampo.NaoInformado, p.Nome.Estado);
        Assert.Equal("problema código 99", p.Texto);
    }

    [Fact]
    public void Lista_sem_problema_e_lida_e_vazia()
    {
        var s = Problemas.Montar([D("Teclado Exemplo", 0)], null, Textos);

        Assert.Equal(EstadoCampo.Lido, s.ComProblema.Estado);
        Assert.Empty(s.ComProblema.Valor!);
    }

    [Fact]
    public void Leitura_que_falhou_vira_erro_com_o_motivo()
    {
        var s = Problemas.Montar(null, "falha simulada", Textos);

        Assert.Equal(EstadoCampo.ErroLeitura, s.ComProblema.Estado);
        Assert.Equal("falha simulada", s.Total.Motivo);
    }

    [FatoWindows]
    public void Maquina_real_tem_dispositivos_e_a_leitura_nao_falha()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var dispositivos = new FonteDispositivosWindows().Ler();

        Assert.NotEmpty(dispositivos);
        Assert.Contains(dispositivos, d => d.Nome is not null && d.IdsHardware.Count > 0);
        Assert.All(dispositivos, d => Assert.NotNull(d.CodigoProblema));
    }
}
