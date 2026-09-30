using System.Buffers.Binary;
using System.Text;

namespace MapHard.Testes.Apoio;

/// <summary>
/// Monta uma tabela SMBIOS em bytes, no formato que o GetSystemFirmwareTable('RSMB') devolve,
/// a partir de estruturas descritas no teste. Todos os valores são fictícios.
/// </summary>
internal sealed class ConstrutorSmbios
{
    private readonly List<byte> _corpo = [];
    private ushort _proximoIdentificador;

    public byte VersaoMaior { get; init; } = 3;

    public byte VersaoMenor { get; init; } = 4;

    /// <summary>
    /// Acrescenta uma estrutura. <paramref name="corpo"/> é a parte formatada sem o cabeçalho de 4 bytes;
    /// o deslocamento 0x04 da especificação é o índice 0 de <paramref name="corpo"/>.
    /// </summary>
    public ConstrutorSmbios Estrutura(byte tipo, byte[] corpo, params string[] textos)
    {
        _corpo.Add(tipo);
        _corpo.Add((byte)(4 + corpo.Length));
        _corpo.Add((byte)(_proximoIdentificador & 0xFF));
        _corpo.Add((byte)(_proximoIdentificador >> 8));
        _proximoIdentificador++;
        _corpo.AddRange(corpo);

        if (textos.Length == 0)
        {
            _corpo.Add(0);
            _corpo.Add(0);
            return this;
        }

        foreach (var texto in textos)
        {
            _corpo.AddRange(Encoding.ASCII.GetBytes(texto));
            _corpo.Add(0);
        }

        _corpo.Add(0);
        return this;
    }

    public ConstrutorSmbios Fim() => Estrutura(127, []);

    public byte[] Montar()
    {
        var bruta = new byte[8 + _corpo.Count];
        bruta[1] = VersaoMaior;
        bruta[2] = VersaoMenor;
        BinaryPrimitives.WriteUInt32LittleEndian(bruta.AsSpan(4), (uint)_corpo.Count);
        _corpo.CopyTo(bruta, 8);
        return bruta;
    }

    /// <summary>Corpo com <paramref name="tamanho"/> bytes zerados, para preencher por deslocamento da especificação.</summary>
    public static byte[] Corpo(int tamanhoFormatado, params (int Deslocamento, byte Valor)[] valores)
    {
        var corpo = new byte[tamanhoFormatado - 4];
        foreach (var (deslocamento, valor) in valores)
        {
            corpo[deslocamento - 4] = valor;
        }

        return corpo;
    }
}
