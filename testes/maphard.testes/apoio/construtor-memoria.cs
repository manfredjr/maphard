using System.Buffers.Binary;

namespace MapHard.Testes.Apoio;

/// <summary>
/// Monta os corpos dos tipos 16 e 17 do SMBIOS para o <see cref="ConstrutorSmbios"/>, por deslocamento
/// da especificação. Todos os valores são fictícios.
/// </summary>
internal static class ConstrutorMemoria
{
    /// <summary>Tamanho do tipo 17 na versão 3.2 da especificação, com o código JEDEC e sem os campos estendidos de velocidade.</summary>
    public const int Tipo17Versao32 = 0x54;

    /// <summary>Tamanho do tipo 17 a partir da versão 3.3, com as velocidades estendidas.</summary>
    public const int Tipo17Versao33 = 0x5C;

    /// <summary>Tipo 16 com capacidade em KB, ECC e número de slots.</summary>
    public static byte[] Conjunto(uint capacidadeKb, ushort slots, byte uso = 0x03, byte ecc = 0x03, int tamanho = 0x17, ulong capacidadeEstendida = 0)
    {
        var c = new Corpo(tamanho);
        c.U8(0x04, 0x03).U8(0x05, uso).U8(0x06, ecc).U32(0x07, capacidadeKb).U16(0x0B, 0xFFFE).U16(0x0D, slots);
        if (tamanho >= 0x17)
        {
            c.U64(0x0F, capacidadeEstendida);
        }

        return c.Bytes;
    }

    /// <summary>
    /// Tipo 17 com os textos na ordem: slot (1), banco (2), fabricante (3), série (4), patrimônio (5) e
    /// part number (6). Os campos que não cabem no <paramref name="tamanho"/> ficam de fora.
    /// </summary>
    public static byte[] Modulo(
        ushort tamanhoBruto,
        byte tipo = 0x1A,
        byte formato = 0x0D,
        ushort velocidade = 3200,
        ushort configurada = 3200,
        byte ranks = 1,
        ushort voltagemMv = 1200,
        ushort codigoFabricante = 0,
        uint tamanhoEstendidoMb = 0,
        uint velocidadeEstendida = 0,
        uint configuradaEstendida = 0,
        int tamanho = Tipo17Versao32)
    {
        var c = new Corpo(tamanho);
        c.U16(0x04, 0x1000).U16(0x06, 0xFFFE).U16(0x08, 64).U16(0x0A, 64).U16(0x0C, tamanhoBruto)
         .U8(0x0E, formato).U8(0x10, 1).U8(0x11, 2).U8(0x12, tipo).U16(0x13, 0x0080)
         .U16(0x15, velocidade).U8(0x17, 3).U8(0x18, 4).U8(0x19, 5).U8(0x1A, 6).U8(0x1B, ranks)
         .U32(0x1C, tamanhoEstendidoMb).U16(0x20, configurada)
         .U16(0x22, voltagemMv).U16(0x24, voltagemMv).U16(0x26, voltagemMv)
         .U16(0x2C, codigoFabricante).U32(0x54, velocidadeEstendida).U32(0x58, configuradaEstendida);
        return c.Bytes;
    }

    public static readonly string[] TextosModulo = ["ChannelA-DIMM0", "BANK 0", "Fabricante Memoria", "SERIE-MEM-0001", "PATRIMONIO-0001", "PN-TESTE-3200"];

    /// <summary>Corpo sem o cabeçalho de 4 bytes. Valor que não cabe no tamanho é ignorado.</summary>
    private sealed class Corpo(int tamanhoFormatado)
    {
        public byte[] Bytes { get; } = new byte[tamanhoFormatado - 4];

        public Corpo U8(int deslocamento, byte valor) => Gravar(deslocamento, 1, b => b[0] = valor);

        public Corpo U16(int deslocamento, ushort valor) => Gravar(deslocamento, 2, b => BinaryPrimitives.WriteUInt16LittleEndian(b, valor));

        public Corpo U32(int deslocamento, uint valor) => Gravar(deslocamento, 4, b => BinaryPrimitives.WriteUInt32LittleEndian(b, valor));

        public Corpo U64(int deslocamento, ulong valor) => Gravar(deslocamento, 8, b => BinaryPrimitives.WriteUInt64LittleEndian(b, valor));

        private Corpo Gravar(int deslocamento, int quantidade, SpanAction gravar)
        {
            var inicio = deslocamento - 4;
            if (inicio + quantidade <= Bytes.Length)
            {
                gravar(Bytes.AsSpan(inicio, quantidade));
            }

            return this;
        }

        private delegate void SpanAction(Span<byte> destino);
    }
}
