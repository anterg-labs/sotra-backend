using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace Api.Servicios;

/// <summary>
/// Redacta con Claude por medio del SDK oficial de Anthropic. Es el proveedor por defecto; la clave se lee de
/// ANTHROPIC_API_KEY y el modelo de IA_API_MODELO. Usa el mismo HttpClient que el otro proveedor, con sus tiempos,
/// su reintento y su límite de llamadas: por eso el SDK no reintenta por su cuenta.
/// </summary>
public class GeneradorDeTextoPorClaude : IGeneradorDeTexto
{
    public const string ModeloPorDefecto = "claude-haiku-5-5";

    private const int TokensMaximosDeRespuesta = 4096;

    private readonly HttpClient _cliente;

    public GeneradorDeTextoPorClaude(HttpClient cliente)
    {
        _cliente = cliente;
    }

    public async Task<string> GenerarAsync(string instrucciones, string mensaje, CancellationToken cancellationToken)
    {
        var clave = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(clave))
        {
            throw new GeneradorDeTextoNoDisponibleException("Falta la variable ANTHROPIC_API_KEY.");
        }

        var cliente = new AnthropicClient { ApiKey = clave, HttpClient = _cliente, MaxRetries = 0 };
        var parametros = new MessageCreateParams
        {
            Model = GeneradorDeTextoPorApi.LeerVariable("IA_API_MODELO", ModeloPorDefecto),
            MaxTokens = TokensMaximosDeRespuesta,
            System = instrucciones,
            // Sin razonamiento: con 78 consultas reales, la mediana bajó de 7,3 a 3,7 segundos con la misma calidad.
            Thinking = new ThinkingConfigDisabled(),
            OutputConfig = new OutputConfig { Effort = LeerEsfuerzo() },
            Messages = [new UserMessageParam { Content = mensaje }]
        };

        try
        {
            var respuesta = await cliente.Messages.Create(parametros, cancellationToken);
            if (respuesta.StopReason == StopReason.Refusal)
            {
                throw new GeneradorDeTextoNoDisponibleException("El proveedor de IA rechazó el pedido.");
            }

            return string.Concat(respuesta.Content.Select(bloque => bloque.Value).OfType<TextBlock>()
                .Select(bloque => bloque.Text)).Trim();
        }
        catch (AnthropicApiException excepcion)
        {
            throw new GeneradorDeTextoNoDisponibleException(
                "El proveedor de IA respondió " + (int)excepcion.StatusCode + ".", excepcion.StatusCode, excepcion);
        }
        catch (RateLimiterRejectedException excepcion)
        {
            throw new GeneradorDeTextoNoDisponibleException(
                "Se alcanzó el límite de llamadas por minuto al proveedor de IA.",
                System.Net.HttpStatusCode.TooManyRequests, excepcion);
        }
        catch (Exception excepcion) when (excepcion is BrokenCircuitException or TimeoutRejectedException
                                              or HttpRequestException or AnthropicIOException)
        {
            throw new GeneradorDeTextoNoDisponibleException("El proveedor de IA no respondió.", causa: excepcion);
        }
    }

    private static Effort LeerEsfuerzo()
    {
        return Environment.GetEnvironmentVariable("IA_API_ESFUERZO")?.ToLowerInvariant() switch
        {
            "medium" => Effort.Medium,
            "high" => Effort.High,
            _ => Effort.Low
        };
    }
}
