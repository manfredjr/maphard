using System.Globalization;

namespace MapHard.Nucleo.Formatacao;

/// <summary>
/// Números, tamanhos, clocks e datas no formato brasileiro. A cultura é fixa em pt-BR,
/// para o texto sair igual em qualquer Windows, de qualquer idioma.
/// </summary>
public static class Formatador
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly string[] _unidades = ["bytes", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>Tamanho em potências de 1024, com até duas casas e sem zeros à direita: "48 KB", "1,25 MB", "32 GB".</summary>
    public static string Bytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes == 1 ? "1 byte" : $"{bytes} bytes";
        }

        double valor = bytes;
        var unidade = 0;
        while (valor >= 1024 && unidade < _unidades.Length - 1)
        {
            valor /= 1024;
            unidade++;
        }

        return $"{valor.ToString("0.##", PtBr)} {_unidades[unidade]}";
    }

    private static readonly string[] _unidadesDecimais = ["bytes", "KB", "MB", "GB", "TB", "PB", "EB"];

    /// <summary>
    /// Tamanho em potências de 1000, como o fabricante de disco usa na etiqueta e o SMART conta os dados
    /// gravados: "1,02 TB", "38,2 TB". A memória e os volumes continuam em <see cref="Bytes"/>, como o Windows mostra.
    /// </summary>
    public static string BytesDecimais(decimal bytes)
    {
        if (bytes < 1000)
        {
            return bytes == 1 ? "1 byte" : $"{bytes.ToString("0", PtBr)} bytes";
        }

        var valor = bytes;
        var unidade = 0;
        while (valor >= 1000 && unidade < _unidadesDecimais.Length - 1)
        {
            valor /= 1000;
            unidade++;
        }

        return $"{valor.ToString("0.##", PtBr)} {_unidadesDecimais[unidade]}";
    }

    /// <summary>"12.345 horas, 514 dias". Abaixo de 24 horas, só as horas.</summary>
    public static string Horas(long horas) =>
        horas < 24 ? Plural(horas, "hora", "horas") : $"{Plural(horas, "hora", "horas")}, {Plural(horas / 24, "dia", "dias")}";

    /// <summary>"3 dias e 4 horas", "5 horas e 12 minutos", "8 minutos": as duas maiores unidades.</summary>
    public static string Duracao(TimeSpan duracao)
    {
        var dias = (long)duracao.TotalDays;
        if (dias > 0)
        {
            return duracao.Hours > 0 ? $"{Plural(dias, "dia", "dias")} e {Plural(duracao.Hours, "hora", "horas")}" : Plural(dias, "dia", "dias");
        }

        if (duracao.Hours > 0)
        {
            return duracao.Minutes > 0 ? $"{Plural(duracao.Hours, "hora", "horas")} e {Plural(duracao.Minutes, "minuto", "minutos")}" : Plural(duracao.Hours, "hora", "horas");
        }

        return Plural(duracao.Minutes, "minuto", "minutos");
    }

    /// <summary>"38,2 TB gravados".</summary>
    public static string Gravados(decimal bytes) => $"{BytesDecimais(bytes)} gravados";

    public static string Temperatura(int graus) => $"{graus} °C";

    /// <summary>Clock: abaixo de 1000 MHz em MHz, a partir daí em GHz com duas casas: "800 MHz", "3,60 GHz".</summary>
    public static string Mhz(double mhz)
    {
        if (mhz < 1000)
        {
            return $"{Math.Round(mhz).ToString("0", PtBr)} MHz";
        }

        return $"{(mhz / 1000).ToString("0.00", PtBr)} GHz";
    }

    public static string Numero(long valor) => valor.ToString("#,0", PtBr);

    public static string Porcentagem(double valor) => $"{valor.ToString("0", PtBr)}%";

    public static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", PtBr);

    public static string DataHora(DateTimeOffset momento) => momento.ToString("dd/MM/yyyy HH:mm", PtBr);

    /// <summary>Idade em anos completos: "menos de 1 ano", "1 ano", "5 anos".</summary>
    public static string IdadeEmAnos(DateOnly desde, DateOnly hoje)
    {
        var anos = hoje.Year - desde.Year;
        if (hoje < desde.AddYears(anos))
        {
            anos--;
        }

        return anos switch
        {
            < 1 => "menos de 1 ano",
            1 => "1 ano",
            _ => $"{anos} anos",
        };
    }

    /// <summary>Plural simples: "1 núcleo", "8 núcleos".</summary>
    public static string Plural(long quantidade, string singular, string plural) =>
        $"{Numero(quantidade)} {(quantidade == 1 ? singular : plural)}";
}
