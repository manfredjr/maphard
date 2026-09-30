using System.Buffers.Binary;
using System.Text;

namespace MapHard.Nucleo.Smbios;

/// <summary>
/// Uma estrutura da tabela SMBIOS: a parte formatada (com o cabeçalho de 4 bytes) e as cadeias de texto.
/// Toda leitura passa pelos métodos abaixo, que devolvem null fora dos limites, para uma tabela
/// malformada nunca derrubar o programa.
/// </summary>
public sealed record EstruturaSmbios(byte Tipo, ushort Identificador, byte[] Formatada, IReadOnlyList<string> Textos)
{
    /// <summary>Tamanho declarado da parte formatada, com o cabeçalho.</summary>
    public int Tamanho => Formatada.Length;

    public byte? Byte(int deslocamento) => deslocamento >= 0 && deslocamento < Formatada.Length ? Formatada[deslocamento] : null;

    public ushort? Palavra(int deslocamento) =>
        deslocamento >= 0 && deslocamento + 2 <= Formatada.Length ? BinaryPrimitives.ReadUInt16LittleEndian(Formatada.AsSpan(deslocamento)) : null;

    public ulong? PalavraQuadrupla(int deslocamento) =>
        deslocamento >= 0 && deslocamento + 8 <= Formatada.Length ? BinaryPrimitives.ReadUInt64LittleEndian(Formatada.AsSpan(deslocamento)) : null;

    public byte[]? Bytes(int deslocamento, int quantidade) =>
        deslocamento >= 0 && deslocamento + quantidade <= Formatada.Length ? Formatada.AsSpan(deslocamento, quantidade).ToArray() : null;

    /// <summary>
    /// Texto referenciado pelo byte no deslocamento. O byte guarda o número da cadeia, começando em 1.
    /// Zero é "sem texto". Número maior que a quantidade de cadeias também devolve null.
    /// </summary>
    public string? Texto(int deslocamento)
    {
        var indice = Byte(deslocamento);
        if (indice is null or 0 || indice.Value > Textos.Count)
        {
            return null;
        }

        return Textos[indice.Value - 1];
    }
}

/// <summary>A tabela SMBIOS inteira, com a versão da especificação que o firmware declara.</summary>
public sealed record TabelaSmbios(Version Versao, IReadOnlyList<EstruturaSmbios> Estruturas)
{
    public IEnumerable<EstruturaSmbios> DoTipo(byte tipo) => Estruturas.Where(e => e.Tipo == tipo);

    public EstruturaSmbios? PrimeiraDoTipo(byte tipo) => Estruturas.FirstOrDefault(e => e.Tipo == tipo);

    /// <summary>A versão como número único, no formato do dmidecode: 2.6 vira 0x0206.</summary>
    public int VersaoNumerica => (Versao.Major << 8) | Versao.Minor;
}

/// <summary>
/// Interpreta o que o <c>GetSystemFirmwareTable('RSMB')</c> devolve: um cabeçalho de 8 bytes
/// (RawSMBIOSData: método de chamada, versão maior, versão menor, revisão DMI, tamanho de 4 bytes)
/// seguido das estruturas. Cada estrutura tem tipo, tamanho da parte formatada e identificador,
/// a parte formatada e as cadeias de texto terminadas em zero, com um zero a mais no fim.
/// O tipo 127 marca o fim da tabela.
/// Fontes: documentação do GetSystemFirmwareTable (MicrosoftDocs/sdk-api, sysinfoapi) e a leitura
/// das mesmas estruturas no dmidecode (dmidecode.c, funções dmi_decode e _dmi_string).
/// </summary>
public static class LeitorTabelaSmbios
{
    private const int TamanhoCabecalhoBruto = 8;
    private const int TamanhoCabecalhoEstrutura = 4;
    private const byte TipoFim = 127;

    public static TabelaSmbios? Interpretar(byte[]? bruta)
    {
        if (bruta is null || bruta.Length < TamanhoCabecalhoBruto)
        {
            return null;
        }

        var versao = new Version(bruta[1], bruta[2]);
        var declarado = BinaryPrimitives.ReadUInt32LittleEndian(bruta.AsSpan(4));
        var fim = (int)Math.Min((long)bruta.Length, TamanhoCabecalhoBruto + (long)declarado);

        var estruturas = new List<EstruturaSmbios>();
        var posicao = TamanhoCabecalhoBruto;
        while (posicao + TamanhoCabecalhoEstrutura <= fim)
        {
            var tipo = bruta[posicao];
            var tamanho = bruta[posicao + 1];
            if (tamanho < TamanhoCabecalhoEstrutura || posicao + tamanho > fim)
            {
                break;
            }

            var identificador = BinaryPrimitives.ReadUInt16LittleEndian(bruta.AsSpan(posicao + 2));
            var formatada = bruta.AsSpan(posicao, tamanho).ToArray();

            var (textos, proxima) = LerTextos(bruta, posicao + tamanho, fim);
            if (proxima < 0)
            {
                break;
            }

            estruturas.Add(new EstruturaSmbios(tipo, identificador, formatada, textos));
            if (tipo == TipoFim)
            {
                break;
            }

            posicao = proxima;
        }

        return new TabelaSmbios(versao, estruturas);
    }

    /// <summary>Lê as cadeias até o zero duplo. Devolve -1 na posição quando a tabela acaba antes do fim das cadeias.</summary>
    private static (IReadOnlyList<string> Textos, int Proxima) LerTextos(byte[] bruta, int inicio, int fim)
    {
        var textos = new List<string>();
        var posicao = inicio;

        // Estrutura sem textos: dois zeros logo após a parte formatada.
        if (posicao + 1 < fim && bruta[posicao] == 0 && bruta[posicao + 1] == 0)
        {
            return (textos, posicao + 2);
        }

        while (posicao < fim)
        {
            var zero = Array.IndexOf(bruta, (byte)0, posicao, fim - posicao);
            if (zero < 0)
            {
                return (textos, -1);
            }

            textos.Add(Decodificar(bruta.AsSpan(posicao, zero - posicao)));
            posicao = zero + 1;
            if (posicao < fim && bruta[posicao] == 0)
            {
                return (textos, posicao + 1);
            }
        }

        return (textos, -1);
    }

    /// <summary>A especificação diz ASCII. Byte fora da faixa imprimível vira ponto, como no dmidecode.</summary>
    private static string Decodificar(ReadOnlySpan<byte> bytes)
    {
        var texto = new StringBuilder(bytes.Length);
        foreach (var b in bytes)
        {
            texto.Append(b is >= 32 and < 127 ? (char)b : '.');
        }

        return texto.ToString().Trim();
    }
}
