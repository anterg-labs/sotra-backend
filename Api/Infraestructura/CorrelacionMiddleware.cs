using System.Diagnostics;
using Serilog.Context;

namespace Api.Infraestructura;

/// <summary>Propaga un identificador de correlación por solicitud: se devuelve en la respuesta y queda en cada log.</summary>
public sealed class CorrelacionMiddleware(RequestDelegate siguiente)
{
    public const string Cabecera = "X-Correlation-Id";
    private const int LargoMaximo = 64;

    public async Task InvokeAsync(HttpContext contexto)
    {
        var identificador = ObtenerValido(contexto.Request.Headers[Cabecera]) ?? Guid.NewGuid().ToString("N");
        contexto.Response.Headers[Cabecera] = identificador;
        contexto.Items[Cabecera] = identificador;

        // El identificador de traza enlaza cada log con su traza distribuida (OpenTelemetry).
        using (LogContext.PushProperty("IdCorrelacion", identificador))
        using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString()))
        {
            await siguiente(contexto);
        }
    }

    private static string? ObtenerValido(string? entrante)
    {
        if (string.IsNullOrWhiteSpace(entrante) || entrante.Length > LargoMaximo)
        {
            return null;
        }

        return entrante.All(caracter => char.IsAsciiLetterOrDigit(caracter) || caracter is '-' or '_')
            ? entrante
            : null;
    }
}
