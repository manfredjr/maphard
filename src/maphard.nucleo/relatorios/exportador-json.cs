using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapHard.Nucleo.Campos;
using MapHard.Nucleo.Coleta;

namespace MapHard.Nucleo.Relatorios;

/// <summary>
/// Grava e lê a coleta em JSON. Cada campo sai como objeto com valor, estado, fonte e motivo,
/// para quem ler o arquivo saber o que foi lido e o que não foi. Nomes em camelCase, sem acento.
/// </summary>
public static class ExportadorJson
{
    private static readonly char[] _proibidosEmArquivo = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    public static readonly JsonSerializerOptions Opcoes = CriarOpcoes();

    public static string Serializar(ColetaMaquina coleta) => JsonSerializer.Serialize(coleta, Opcoes);

    public static ColetaMaquina? Desserializar(string json) => JsonSerializer.Deserialize<ColetaMaquina>(json, Opcoes);

    /// <summary>UTF-8 sem BOM.</summary>
    public static void Gravar(ColetaMaquina coleta, string caminho) =>
        File.WriteAllText(caminho, Serializar(coleta), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    /// <summary>Nome padrão: maphard-COMPUTADOR-aaaa-mm-dd-hhmm.json, com o que o Windows não aceita em nome de arquivo trocado por hífen.</summary>
    public static string NomePadrao(string computador, DateTimeOffset momento)
    {
        var limpo = new string(computador.Select(c => char.IsControl(c) || _proibidosEmArquivo.Contains(c) ? '-' : c).ToArray()).Trim();
        if (limpo.Length == 0)
        {
            limpo = "computador";
        }

        return $"maphard-{limpo}-{momento:yyyy-MM-dd-HHmm}.json";
    }

    private static JsonSerializerOptions CriarOpcoes()
    {
        var opcoes = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        opcoes.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        opcoes.Converters.Add(new ConversorCampo());
        return opcoes;
    }

    /// <summary>Converte qualquer <see cref="Campo{T}"/>.</summary>
    private sealed class ConversorCampo : JsonConverterFactory
    {
        public override bool CanConvert(Type tipo) => tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(Campo<>);

        public override JsonConverter CreateConverter(Type tipo, JsonSerializerOptions opcoes) =>
            (JsonConverter)Activator.CreateInstance(typeof(ConversorCampo<>).MakeGenericType(tipo.GetGenericArguments()[0]))!;
    }

    private sealed class ConversorCampo<T> : JsonConverter<Campo<T>>
    {
        public override void Write(Utf8JsonWriter escritor, Campo<T> campo, JsonSerializerOptions opcoes)
        {
            escritor.WriteStartObject();
            escritor.WritePropertyName("valor");
            if (campo.FoiLido)
            {
                JsonSerializer.Serialize(escritor, campo.Valor, opcoes);
            }
            else
            {
                escritor.WriteNullValue();
            }

            escritor.WritePropertyName("estado");
            JsonSerializer.Serialize(escritor, campo.Estado, opcoes);
            escritor.WritePropertyName("fonte");
            JsonSerializer.Serialize(escritor, campo.Fonte, opcoes);
            escritor.WriteString("motivo", campo.Motivo);
            escritor.WriteEndObject();
        }

        public override Campo<T> Read(ref Utf8JsonReader leitor, Type tipo, JsonSerializerOptions opcoes)
        {
            using var documento = JsonDocument.ParseValue(ref leitor);
            var raiz = documento.RootElement;
            var estado = raiz.GetProperty("estado").Deserialize<EstadoCampo>(opcoes);
            var fonte = raiz.GetProperty("fonte").Deserialize<FonteDado>(opcoes);
            var motivo = raiz.TryGetProperty("motivo", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;

            return estado switch
            {
                EstadoCampo.Lido => Campo<T>.Lido(raiz.GetProperty("valor").Deserialize<T>(opcoes)!, fonte, motivo),
                EstadoCampo.NaoInformado => Campo<T>.NaoInformado(fonte, motivo),
                EstadoCampo.RequerAdministrador => Campo<T>.RequerAdministrador(fonte),
                EstadoCampo.NaoSuportado => Campo<T>.NaoSuportado(fonte, motivo),
                _ => Campo<T>.Erro(fonte, motivo ?? "erro de leitura"),
            };
        }
    }
}
