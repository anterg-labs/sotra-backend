using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Infraestructura;

/// <summary>
/// PostgreSQL no admite el carácter nulo (U+0000) en un texto: llegaba a la base y respondía 500.
/// Se rechaza en el borde con un 400 y un <c>codigo</c> estable, tanto en la dirección como en el JSON y los formularios.
/// </summary>
public sealed class EntradaSinCaracteresNulosMiddleware(RequestDelegate siguiente)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        if (ContieneNulo(contexto.Request.Path.Value) || ContieneNulo(contexto.Request.QueryString.Value)
            || await FormularioContieneNuloAsync(contexto.Request))
        {
            MetricasDeNegocio.EntradasRechazadas.Add(1);
            var servicio = contexto.RequestServices.GetRequiredService<IProblemDetailsService>();
            contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
            await servicio.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = contexto,
                ProblemDetails =
                {
                    Status = StatusCodes.Status400BadRequest,
                    Extensions =
                    {
                        ["codigo"] = "ENTRADA_INVALIDA",
                        ["mensaje"] = "La solicitud contiene caracteres no permitidos.",
                    },
                },
            });
            return;
        }

        await siguiente(contexto);
    }

    private static bool ContieneNulo(string? texto) =>
        texto is not null && (texto.Contains('\0') || texto.Contains("%00", StringComparison.Ordinal));

    private static async Task<bool> FormularioContieneNuloAsync(HttpRequest solicitud)
    {
        if (!solicitud.HasFormContentType)
        {
            return false;
        }

        IFormCollection formulario;
        try
        {
            formulario = await solicitud.ReadFormAsync(solicitud.HttpContext.RequestAborted);
        }
        catch (InvalidDataException)
        {
            // El lector de formularios de ASP.NET Core rechaza por sí mismo los valores con %00 y respondería 500.
            return true;
        }

        return formulario.Any(campo => ContieneNulo(campo.Key) || campo.Value.Any(ContieneNulo))
            || formulario.Files.Any(archivo => ContieneNulo(archivo.FileName));
    }
}

public sealed class TextoSinCaracteresNulosConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader lector, Type tipo, JsonSerializerOptions opciones)
    {
        var texto = lector.GetString();
        if (texto is not null && texto.Contains('\0'))
        {
            throw new JsonException("El texto contiene caracteres no permitidos.");
        }

        return texto;
    }

    public override void Write(Utf8JsonWriter escritor, string valor, JsonSerializerOptions opciones) =>
        escritor.WriteStringValue(valor);
}
