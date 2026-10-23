using Api.Servicios;
using Microsoft.Extensions.Http.Resilience;

namespace Api.Infraestructura;

public static class ConfiguracionDeOllama
{
    private const int SegundosMaximosParaConectar = 5;
    private const int MinutosMaximosPorIntento = 5;
    private const int MinutosMaximosEnTotal = 10;
    private const int ReintentosMaximos = 2;
    private const int SegundosDeCorteDeCircuito = 30;
    private const int SolicitudesMinimasParaCortar = 5;

    /// <summary>
    /// Cliente de Ollama con reintentos con retroceso para fallos transitorios (conexión, 408, 429 y 5xx) y corte de
    /// circuito: con Ollama caído la API falla rápido en vez de esperar el tiempo máximo en cada solicitud.
    /// </summary>
    public static IServiceCollection AgregarClienteOllama(this IServiceCollection servicios,
        Action<HttpStandardResilienceOptions>? ajustar = null)
    {
        servicios.AddHttpClient<ServicioOllama>((proveedor, cliente) =>
            {
                var url = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL");
                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new InvalidOperationException("Falta la variable OLLAMA_BASE_URL.");
                }

                cliente.BaseAddress = new Uri(url);
                cliente.Timeout = TimeSpan.FromMinutes(MinutosMaximosEnTotal + 1);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(SegundosMaximosParaConectar)
            })
            .AddStandardResilienceHandler(opciones =>
            {
                opciones.Retry.MaxRetryAttempts = ReintentosMaximos;
                opciones.AttemptTimeout.Timeout = TimeSpan.FromMinutes(MinutosMaximosPorIntento);
                opciones.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(MinutosMaximosEnTotal);
                opciones.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(MinutosMaximosPorIntento * 2);
                opciones.CircuitBreaker.MinimumThroughput = SolicitudesMinimasParaCortar;
                opciones.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(SegundosDeCorteDeCircuito);
                ajustar?.Invoke(opciones);
            });

        return servicios;
    }
}
