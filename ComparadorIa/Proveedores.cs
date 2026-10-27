using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace ComparadorIa;

public record TextoGenerado(string Texto, long TokensEntrada, long TokensSalida);

public interface IProveedorDeTexto
{
    ConfiguracionDeProveedor Configuracion { get; }

    Task<TextoGenerado> GenerarAsync(string instrucciones, string mensaje, CancellationToken cancellationToken);
}

public class ErrorDelProveedorException : Exception
{
    public ErrorDelProveedorException(string mensaje) : base(mensaje)
    {
    }
}

public static class FabricaDeProveedores
{
    public static IProveedorDeTexto Crear(ConfiguracionDeProveedor configuracion, string? clave)
    {
        if (configuracion.Tipo == "anthropic")
        {
            return new ProveedorClaude(configuracion, clave);
        }

        if (configuracion.Tipo == "openai")
        {
            return new ProveedorCompatibleConOpenAi(configuracion, clave);
        }

        throw new InvalidDataException("Tipo de proveedor desconocido: " + configuracion.Tipo);
    }
}

/// <summary>Groq, Gemini, Mistral, DeepSeek y Ollama aceptan el formato de chat de OpenAI con otra URL base.</summary>
public class ProveedorCompatibleConOpenAi : IProveedorDeTexto
{
    private const int LargoMaximoDelErrorMostrado = 300;

    private readonly HttpClient _cliente;

    public ConfiguracionDeProveedor Configuracion { get; }

    public ProveedorCompatibleConOpenAi(ConfiguracionDeProveedor configuracion, string? clave)
    {
        Configuracion = configuracion;
        _cliente = new HttpClient
        {
            BaseAddress = new Uri(configuracion.UrlBase.TrimEnd('/') + "/"),
            Timeout = Timeout.InfiniteTimeSpan
        };

        if (!string.IsNullOrWhiteSpace(clave))
        {
            _cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clave);
        }
    }

    public async Task<TextoGenerado> GenerarAsync(string instrucciones, string mensaje,
        CancellationToken cancellationToken)
    {
        var cuerpo = new Dictionary<string, object?>
        {
            ["model"] = Configuracion.Modelo,
            ["temperature"] = 0,
            ["max_tokens"] = Configuracion.TokensMaximos,
            ["messages"] = new[]
            {
                new { role = "system", content = instrucciones },
                new { role = "user", content = mensaje }
            }
        };

        foreach (var parametro in Configuracion.ParametrosExtra)
        {
            cuerpo[parametro.Key] = parametro.Value;
        }

        // Se envía con largo conocido: algunos servidores compatibles no aceptan cuerpos por partes.
        using var pedido = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json");
        using var respuesta = await _cliente.PostAsync("chat/completions", pedido, cancellationToken);
        var contenido = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = contenido.Length > LargoMaximoDelErrorMostrado
                ? contenido.Substring(0, LargoMaximoDelErrorMostrado)
                : contenido;
            throw new ErrorDelProveedorException("HTTP " + (int)respuesta.StatusCode + ": " + detalle);
        }

        using var documento = JsonDocument.Parse(contenido);
        var raiz = documento.RootElement;
        var texto = raiz.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        long tokensEntrada = 0;
        long tokensSalida = 0;
        if (raiz.TryGetProperty("usage", out var uso))
        {
            if (uso.TryGetProperty("prompt_tokens", out var entrada))
            {
                tokensEntrada = entrada.GetInt64();
            }

            if (uso.TryGetProperty("completion_tokens", out var salida))
            {
                tokensSalida = salida.GetInt64();
            }
        }

        return new TextoGenerado((texto ?? "").Trim(), tokensEntrada, tokensSalida);
    }
}

/// <summary>Claude se llama con el SDK oficial de Anthropic, no con su capa de compatibilidad con OpenAI.</summary>
public class ProveedorClaude : IProveedorDeTexto
{
    private readonly AnthropicClient _cliente;

    public ConfiguracionDeProveedor Configuracion { get; }

    public ProveedorClaude(ConfiguracionDeProveedor configuracion, string? clave)
    {
        Configuracion = configuracion;
        _cliente = new AnthropicClient { ApiKey = clave, MaxRetries = 0 };
    }

    public async Task<TextoGenerado> GenerarAsync(string instrucciones, string mensaje,
        CancellationToken cancellationToken)
    {
        var respuesta = await _cliente.Messages.Create(new MessageCreateParams
        {
            Model = Configuracion.Modelo,
            MaxTokens = Configuracion.TokensMaximos,
            System = instrucciones,
            OutputConfig = new OutputConfig { Effort = LeerEsfuerzo(Configuracion.Esfuerzo) },
            Messages = [new UserMessageParam { Content = mensaje }]
        }, cancellationToken);

        var texto = "";
        foreach (var bloque in respuesta.Content)
        {
            if (bloque.TryPickText(out TextBlock? bloqueDeTexto))
            {
                texto += bloqueDeTexto.Text;
            }
        }

        return new TextoGenerado(texto.Trim(), respuesta.Usage.InputTokens, respuesta.Usage.OutputTokens);
    }

    private static Effort LeerEsfuerzo(string? esfuerzo)
    {
        if (esfuerzo == "medium")
        {
            return Effort.Medium;
        }

        if (esfuerzo == "high")
        {
            return Effort.High;
        }

        return Effort.Low;
    }
}
