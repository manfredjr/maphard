using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Firmware;
using MapHard.Nucleo.Windows;
using MapHard.Testes.Apoio;

namespace MapHard.Testes;

public class FirmwareTestes
{
    internal sealed class RegistroSimulado : IFonteRegistro
    {
        private readonly Dictionary<(string, string), object> _valores = [];

        public RegistroSimulado Com(string chave, string valor, object dado)
        {
            _valores[(chave, valor)] = dado;
            return this;
        }

        public object? Ler(string chave, string valor) => _valores.GetValueOrDefault((chave, valor));
    }

    private sealed class FirmwareSimulado : IFonteFirmware
    {
        public int? Tipo { get; init; } = 2;

        public bool Virtualizacao { get; init; } = true;

        public RespostaTpm? RespostaTpm { get; init; } = new(0, 2);

        public int? TipoFirmware() => Tipo;

        public bool VirtualizacaoLigadaNoFirmware() => Virtualizacao;

        public RespostaTpm? Tpm() => RespostaTpm;
    }

    [Fact]
    public void Uefi_com_secure_boot_ligado_e_tpm_2()
    {
        var registro = new RegistroSimulado().Com(LeitorFirmware.ChaveSecureBoot, "UEFISecureBootEnabled", 1);

        var f = LeitorFirmware.Ler(new FirmwareSimulado(), registro, administrador: false);

        Assert.Equal("UEFI", f.Modo.Valor);
        Assert.True(f.SecureBoot.Valor);
        Assert.Equal("2.0", f.TpmVersao.Valor);
        Assert.Equal(EstadoCampo.RequerAdministrador, f.TpmFabricante.Estado);
        Assert.True(f.VirtualizacaoLigada.Valor);
    }

    [Fact]
    public void Uefi_com_secure_boot_desligado()
    {
        var registro = new RegistroSimulado().Com(LeitorFirmware.ChaveSecureBoot, "UEFISecureBootEnabled", 0);

        Assert.False(LeitorFirmware.Ler(new FirmwareSimulado(), registro, false).SecureBoot.Valor);
    }

    [Fact]
    public void Legado_nao_tem_secure_boot()
    {
        var f = LeitorFirmware.Ler(new FirmwareSimulado { Tipo = 1 }, new RegistroSimulado(), false);

        Assert.Equal("BIOS legado", f.Modo.Valor);
        Assert.Equal(EstadoCampo.NaoSuportado, f.SecureBoot.Estado);
    }

    [Fact]
    public void Uefi_sem_a_chave_do_secure_boot_fica_nao_informado()
    {
        Assert.Equal(EstadoCampo.NaoInformado, LeitorFirmware.Ler(new FirmwareSimulado(), new RegistroSimulado(), false).SecureBoot.Estado);
    }

    [Fact]
    public void Api_de_firmware_falhando_vira_erro()
    {
        Assert.Equal(EstadoCampo.ErroLeitura, LeitorFirmware.Ler(new FirmwareSimulado { Tipo = null }, new RegistroSimulado(), false).Modo.Estado);
    }

    [Fact]
    public void Tpm_ausente_e_nao_suportado_nos_dois_campos()
    {
        var f = LeitorFirmware.Ler(new FirmwareSimulado { RespostaTpm = new(LeitorFirmware.TpmNaoEncontrado, 0) }, new RegistroSimulado(), false);

        Assert.Equal(EstadoCampo.NaoSuportado, f.TpmVersao.Estado);
        Assert.Equal(EstadoCampo.NaoSuportado, f.TpmFabricante.Estado);
    }

    [Theory]
    [InlineData(1u, "1.2")]
    [InlineData(2u, "2.0")]
    public void Versao_do_tpm(uint versao, string esperado)
    {
        Assert.Equal(esperado, LeitorFirmware.Ler(new FirmwareSimulado { RespostaTpm = new(0, versao) }, new RegistroSimulado(), false).TpmVersao.Valor);
    }

    [Fact]
    public void Outro_codigo_do_tpm_vira_erro_com_o_codigo()
    {
        var f = LeitorFirmware.Ler(new FirmwareSimulado { RespostaTpm = new(0x80284002, 0) }, new RegistroSimulado(), false);

        Assert.Equal(EstadoCampo.ErroLeitura, f.TpmVersao.Estado);
        Assert.Contains("80284002", f.TpmVersao.Motivo);
    }

    [Fact]
    public void Microcodigo_pela_metade_alta_ou_baixa()
    {
        Assert.Equal("0xF4", LeitorFirmware.Microcodigo(new byte[] { 0, 0, 0, 0, 0xF4, 0, 0, 0 }).Valor);
        Assert.Equal("0xA20120E", LeitorFirmware.Microcodigo(new byte[] { 0x0E, 0x12, 0x20, 0x0A, 0, 0, 0, 0 }).Valor);
        Assert.Equal(EstadoCampo.NaoInformado, LeitorFirmware.Microcodigo(new byte[8]).Estado);
        Assert.Equal(EstadoCampo.NaoInformado, LeitorFirmware.Microcodigo(null).Estado);
    }

    [Fact]
    public void Microcodigo_de_4_bytes_e_lido_direto()
    {
        Assert.Equal("0x12C", LeitorFirmware.Microcodigo(new byte[] { 0x2C, 0x01, 0, 0 }).Valor);
        Assert.Equal(EstadoCampo.NaoInformado, LeitorFirmware.Microcodigo(new byte[4]).Estado);
        Assert.Equal(EstadoCampo.NaoInformado, LeitorFirmware.Microcodigo(new byte[] { 1, 2, 3 }).Estado);
    }

    [Theory]
    [InlineData("19045", "Professional", "Windows 10 Pro")]
    [InlineData("22631", "Core", "Windows 11 Home")]
    [InlineData("26100", "EdicaoNova", "Windows 11 EdicaoNova")]
    public void Nome_do_windows_pela_compilacao(string compilacao, string edicao, string esperado)
    {
        var registro = new RegistroSimulado().Com(LeitorWindows.Chave, "CurrentBuild", compilacao).Com(LeitorWindows.Chave, "EditionID", edicao);

        Assert.Equal(esperado, LeitorWindows.Ler(registro).Nome.Valor);
    }

    [Fact]
    public void Versao_e_compilacao_completa()
    {
        var registro = new RegistroSimulado()
            .Com(LeitorWindows.Chave, "CurrentBuild", "22631")
            .Com(LeitorWindows.Chave, "UBR", 4169)
            .Com(LeitorWindows.Chave, "DisplayVersion", "23H2");

        var w = LeitorWindows.Ler(registro);

        Assert.Equal("Windows 11", w.Nome.Valor);
        Assert.Equal("23H2", w.Versao.Valor);
        Assert.Equal("22631.4169", w.Compilacao.Valor);
    }

    [Fact]
    public void Sem_compilacao_tudo_fica_nao_informado()
    {
        var w = LeitorWindows.Ler(new RegistroSimulado());

        Assert.Equal(EstadoCampo.NaoInformado, w.Nome.Estado);
        Assert.Equal(EstadoCampo.NaoInformado, w.Compilacao.Estado);
    }

    [FatoWindows]
    public void Firmware_e_windows_reais()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var f = LeitorFirmware.Ler(new FonteFirmwareWindows(), new FonteRegistroWindows(), false);
        var w = LeitorWindows.Ler(new FonteRegistroWindows());

        Assert.NotEqual(EstadoCampo.ErroLeitura, f.Modo.Estado);
        Assert.True(w.Compilacao.FoiLido);
    }
}
