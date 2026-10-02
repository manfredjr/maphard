using MapHard.Nucleo.Sobre;

namespace MapHard.Testes;

public class RecursosTestes
{
    private static string Raiz => CaracteresProibidosTestes.RaizDoRepositorio();

    private static string App(string relativo) => Path.Combine(Raiz, "src", "maphard", relativo);

    [Fact]
    public void Fonte_vai_com_a_licenca()
    {
        foreach (var fonte in new[] { "montserrat-regular.ttf", "montserrat-semibold.ttf", "montserrat-extrabold.ttf", "ofl.txt" })
        {
            Assert.True(File.Exists(App(Path.Combine("recursos", "fontes", fonte))), fonte);
        }
    }

    [Fact]
    public void Tema_tem_as_cores_oficiais_da_mt()
    {
        var tema = File.ReadAllText(App(Path.Combine("tema", "tema-mt.xaml")));
        foreach (var cor in new[] { "#006B2D", "#0F8F2F", "#43A92C", "#9AD52B", "#202020", "#F4F4F4" })
        {
            Assert.Contains(cor, tema);
        }

        Assert.DoesNotContain("mapdisk", tema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mapnet", tema, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Janela_mostra_o_logo_da_mt_e_o_botao_sobre()
    {
        Assert.True(File.Exists(App(Path.Combine("recursos", "mt-logo.png"))));
        var janela = File.ReadAllText(App("janela-principal.xaml"));
        Assert.Contains("component/recursos/mt-logo.png", janela);
        Assert.Contains("Click=\"AoClicarLogo\"", janela);
        Assert.Contains("Click=\"AoAbrirSobre\"", janela);
        Assert.Contains("Click=\"AoSalvarRelatorio\"", janela);

        var sobre = File.ReadAllText(App("janela-sobre.xaml"));
        foreach (var campo in new[] { "TextosSobre.Copyright", "TextosSobre.SoftwareLivre", "TextosSobre.Versoes", "TextosSobre.Repositorio", "CampoLicenca" })
        {
            Assert.Contains(campo, sobre);
        }
    }

    [Fact]
    public void Tela_nao_corta_texto_com_reticencias_de_um_caractere()
    {
        foreach (var xaml in Directory.GetFiles(Path.Combine(Raiz, "src", "maphard"), "*.xaml", SearchOption.AllDirectories))
        {
            var texto = File.ReadAllText(xaml);
            Assert.DoesNotContain("CharacterEllipsis", texto);
            Assert.DoesNotContain("WordEllipsis", texto);
        }
    }

    [Fact]
    public void Manifesto_roda_sem_administrador()
    {
        Assert.Contains("level=\"asInvoker\"", File.ReadAllText(App("app.manifest")));
    }

    [Fact]
    public void Licenca_embutida_e_o_arquivo_license_do_repositorio()
    {
        var arquivo = File.ReadAllText(Path.Combine(Raiz, "LICENSE"));
        var embutida = TextosSobre.LerLicenca();

        Assert.Equal(arquivo.ReplaceLineEndings(), embutida.ReplaceLineEndings());
        Assert.Contains("GNU GENERAL PUBLIC LICENSE", embutida);
        Assert.Contains("Version 3, 29 June 2007", embutida);
    }

    [Fact]
    public void Copyright_e_o_mesmo_dos_metadados_do_exe()
    {
        var props = File.ReadAllText(Path.Combine(Raiz, "Directory.Build.props"));

        Assert.Contains($"<Copyright>{TextosSobre.Copyright}</Copyright>", props);
        Assert.Contains("<Authors>Manfred Heil Junior</Authors>", props);
        Assert.Contains("<Product>MapHard - MT</Product>", props);
    }

    [Fact]
    public void Enderecos_sao_do_repositorio_e_do_site_da_mt()
    {
        Assert.Equal("https://github.com/manfredjr/maphard", TextosSobre.Repositorio);
        Assert.Equal(TextosSobre.Repositorio + "/releases", TextosSobre.Versoes);
        Assert.Equal("https://www.manfred.com.br", TextosSobre.SiteMt);
    }
}
