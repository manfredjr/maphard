using MapHard.Nucleo.Coleta;

namespace MapHard.Nucleo.Sobre;

/// <summary>
/// Textos e endereços da janela Sobre. O aviso de software livre segue a GPL-3.0 (seção 0, "Appropriate
/// Legal Notices"), no mesmo texto do MapDisk. O texto "Licença e garantias" espera a verificação
/// jurídica do MapHard (pendência "Verificação jurídica"), porque texto jurídico não sai de memória.
/// </summary>
public static class TextosSobre
{
    public const string SiteMt = "https://www.manfred.com.br";

    public const string Repositorio = "https://github.com/manfredjr/maphard";

    public const string Versoes = "https://github.com/manfredjr/maphard/releases";

    public const string Descricao = "O hardware deste computador: processador, memória, placa-mãe, discos e o que exige atenção.";

    public const string Autoria = "Desenvolvido por Manfred Heil Junior, da MT - Manfred Tecnologia.";

    public const string Copyright = "Copyright (c) 2026 MANFRED TECNOLOGIA LTDA";

    public const string SoftwareLivre =
        "Este programa é software livre: você pode redistribuí-lo e modificá-lo nos termos da GNU General Public " +
        "License, versão 3 (GPL-3.0), publicada pela Free Software Foundation. O botão \"Ver a licença\" mostra o texto completo.";

    public const string Fonte = "A fonte Montserrat vai embutida no programa sob a SIL Open Font License 1.1.";

    public static string Titulo => $"MapHard - MT, versão {Coletor.VersaoPrograma}";

    /// <summary>O texto completo da GPL-3.0, embutido no programa: abre sem internet.</summary>
    public static string LerLicenca()
    {
        using var fluxo = typeof(TextosSobre).Assembly.GetManifestResourceStream("licenca.txt")
            ?? throw new InvalidOperationException("A licença não foi embutida no programa.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }
}
