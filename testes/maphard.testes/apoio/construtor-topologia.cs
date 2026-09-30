using System.Buffers.Binary;

namespace MapHard.Testes.Apoio;

/// <summary>Monta o buffer do GetLogicalProcessorInformationEx com núcleos, caches e pacotes fictícios.</summary>
internal sealed class ConstrutorTopologia
{
    private readonly List<byte> _buffer = [];
    private int _proximoProcessador;

    /// <summary>Um núcleo com <paramref name="threads"/> processadores lógicos seguidos.</summary>
    public ConstrutorTopologia Nucleo(int threads, byte eficiencia = 0)
    {
        var registro = new byte[8 + 24 + 16];
        BinaryPrimitives.WriteInt32LittleEndian(registro, 0);
        BinaryPrimitives.WriteInt32LittleEndian(registro.AsSpan(4), registro.Length);
        registro[8] = (byte)(threads > 1 ? 1 : 0);
        registro[8 + 1] = eficiencia;
        BinaryPrimitives.WriteUInt16LittleEndian(registro.AsSpan(8 + 22), 1);
        var mascara = ((1UL << threads) - 1) << _proximoProcessador;
        BinaryPrimitives.WriteUInt64LittleEndian(registro.AsSpan(8 + 24), mascara);
        _proximoProcessador += threads;
        _buffer.AddRange(registro);
        return this;
    }

    public ConstrutorTopologia Cache(byte nivel, int tipo, uint tamanho, byte associatividade = 8, ushort linha = 64)
    {
        var registro = new byte[8 + 32 + 16];
        BinaryPrimitives.WriteInt32LittleEndian(registro, 2);
        BinaryPrimitives.WriteInt32LittleEndian(registro.AsSpan(4), registro.Length);
        registro[8] = nivel;
        registro[8 + 1] = associatividade;
        BinaryPrimitives.WriteUInt16LittleEndian(registro.AsSpan(8 + 2), linha);
        BinaryPrimitives.WriteUInt32LittleEndian(registro.AsSpan(8 + 4), tamanho);
        BinaryPrimitives.WriteInt32LittleEndian(registro.AsSpan(8 + 8), tipo);
        _buffer.AddRange(registro);
        return this;
    }

    public ConstrutorTopologia Pacote()
    {
        var registro = new byte[8 + 24 + 16];
        BinaryPrimitives.WriteInt32LittleEndian(registro, 3);
        BinaryPrimitives.WriteInt32LittleEndian(registro.AsSpan(4), registro.Length);
        _buffer.AddRange(registro);
        return this;
    }

    /// <summary>Registro de um tipo que o leitor não usa, com tamanho próprio.</summary>
    public ConstrutorTopologia Desconhecido(int tipo, int tamanho)
    {
        var registro = new byte[tamanho];
        BinaryPrimitives.WriteInt32LittleEndian(registro, tipo);
        BinaryPrimitives.WriteInt32LittleEndian(registro.AsSpan(4), tamanho);
        _buffer.AddRange(registro);
        return this;
    }

    public byte[] Montar() => [.. _buffer];
}
