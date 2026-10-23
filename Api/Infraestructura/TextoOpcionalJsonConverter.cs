using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Infraestructura;

/// <summary>Normaliza un texto opcional antes de validar su formato, conservando la protección contra U+0000.</summary>
public sealed class TextoOpcionalJsonConverter : JsonConverter<string>
{
    private static readonly TextoSinCaracteresNulosConverter TextoSeguro = new();

    public override string? Read(ref Utf8JsonReader lector, Type tipo, JsonSerializerOptions opciones)
    {
        var texto = TextoSeguro.Read(ref lector, tipo, opciones);
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    public override void Write(Utf8JsonWriter escritor, string valor, JsonSerializerOptions opciones) =>
        escritor.WriteStringValue(valor);
}
