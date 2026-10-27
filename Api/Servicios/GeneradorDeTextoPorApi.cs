using System.Net;
using System.Text;
using System.Text.Json;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace Api.Servicios;

public interface IGeneradorDeTexto
{
    Task<string> GenerarAsync(string instrucciones, string mensaje, CancellationToken cancellationToken);
}

/// <summary>El proveedor de IA no respondió, rechazó el pedido o no está configurado.</summary>
public class GeneradorDeTextoNoDisponibleException : Exception
{
    public HttpStatusCode? CodigoHttp { get; }

    public GeneradorDeTextoNoDisponibleException(string mensaje, HttpStatusCode? codigoHttp = null,
        Exception? causa = null) : base(mensaje, causa)
    {
        CodigoHttp = codigoHttp;
    }
}

/// <summary>
/// Redacta con un proveedor externo que acepta el formato de chat de OpenAI (Groq por defecto; también Gemini,
/// Mistral u otros). El proveedor se elige con IA_API_URL, IA_API_MODELO, IA_API_CLAVE e IA_API_ESFUERZO.
/// </summary>
public class GeneradorDeTextoPorApi : IGeneradorDeTexto
{
    public const string UrlPorDefecto = "https://api.groq.com/openai/v1/";
    public const string ModeloPorDefecto = "openai/gpt-oss-20b";

    // Holgado para modelos que razonan: el razonamiento también cuenta como salida.
    private const int TokensMaximosDeRespuesta = 1024;
    private const int LargoMaximoDelErrorRegistrado = 300;

    private readonly HttpClient _cliente;

    public GeneradorDeTextoPorApi(HttpClient cliente)
    {
        _cliente = cliente;
    }

    public async Task<string> GenerarAsync(string instrucciones, string mensaje, CancellationToken cancellationToken)
    {
        var clave = Environment.GetEnvironmentVariable("IA_API_CLAVE");
        if (string.IsNullOrWhiteSpace(clave))
        {
            throw new GeneradorDeTextoNoDisponibleException("Falta la variable IA_API_CLAVE.");
        }

        var cuerpo = new Dictionary<string, object>
        {
            ["model"] = LeerVariable("IA_API_MODELO", ModeloPorDefecto),
            ["temperature"] = 0,
            ["max_tokens"] = TokensMaximosDeRespuesta,
            ["messages"] = new[]
            {
                new { role = "system", content = instrucciones },
                new { role = "user", content = mensaje }
            }
        };

        var esfuerzo = Environment.GetEnvironmentVariable("IA_API_ESFUERZO");
        if (!string.IsNullOrWhiteSpace(esfuerzo))
        {
            cuerpo["reasoning_effort"] = esfuerzo;
        }

        using var pedido = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json")
        };
        pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", clave);

        using var respuesta = await EnviarAsync(pedido, cancellationToken);
        var contenido = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = contenido.Length > LargoMaximoDelErrorRegistrado
                ? contenido.Substring(0, LargoMaximoDelErrorRegistrado)
                : contenido;
            throw new GeneradorDeTextoNoDisponibleException(
                "El proveedor de IA respondió " + (int)respuesta.StatusCode + ": " + detalle, respuesta.StatusCode);
        }

        return LeerTexto(contenido);
    }

    public static string LeerVariable(string nombre, string valorPorDefecto)
    {
        var valor = Environment.GetEnvironmentVariable(nombre);
        return string.IsNullOrWhiteSpace(valor) ? valorPorDefecto : valor;
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage pedido, CancellationToken cancellationToken)
    {
        try
        {
            return await _cliente.SendAsync(pedido, cancellationToken);
        }
        catch (Exception excepcion) when (excepcion is BrokenCircuitException or TimeoutRejectedException
                                              or HttpRequestException)
        {
            throw new GeneradorDeTextoNoDisponibleException("El proveedor de IA no respondió.", causa: excepcion);
        }
        catch (RateLimiterRejectedException excepcion)
        {
            throw new GeneradorDeTextoNoDisponibleException(
                "Se alcanzó el límite de llamadas por minuto al proveedor de IA.", HttpStatusCode.TooManyRequests,
                excepcion);
        }
    }

    private static string LeerTexto(string contenido)
    {
        try
        {
            using var documento = JsonDocument.Parse(contenido);
            var texto = documento.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString();
            return (texto ?? "").Trim();
        }
        catch (Exception excepcion) when (excepcion is JsonException or KeyNotFoundException
                                              or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new GeneradorDeTextoNoDisponibleException("El proveedor de IA devolvió una respuesta inesperada.",
                causa: excepcion);
        }
    }
}
