using System.Threading.RateLimiting;
using Api.Servicios;
using Microsoft.Extensions.Http.Resilience;

namespace Api.Infraestructura;

public static class ConfiguracionDeGeneradorDeTexto
{
    private const int SegundosMaximosParaConectar = 5;
    private const int SegundosMaximosPorIntento = 15;
    private const int SegundosMaximosEnTotal = 20;
    private const int ReintentosMaximos = 1;
    private const int SegundosDeCorteDeCircuito = 30;
    private const int SegundosDeMuestreoDelCircuito = 60;
    private const int SolicitudesMinimasParaCortar = 5;
    private const int LlamadasPorMinutoPorDefecto = 10;
    private const int SegmentosDeLaVentana = 6;

    /// <summary>
    /// Cliente del proveedor de IA con tiempos cortos: si no responde en <see cref="SegundosMaximosEnTotal"/>
    /// segundos se usa el texto del manual sin resumir. Reintenta una vez los fallos pasajeros (conexión, 408, 429
    /// y 5xx) y corta el circuito: con el proveedor caído, los pedidos siguientes no lo esperan.
    /// Además limita las llamadas al proveedor a IA_API_LLAMADAS_POR_MINUTO (10 por defecto) para toda la API, sin
    /// cola: la que se pasa del límite usa el texto del manual sin resumir. El reintento no cuenta como otra llamada.
    /// </summary>
    public static IServiceCollection AgregarGeneradorDeTexto(this IServiceCollection servicios,
        Action<HttpStandardResilienceOptions>? ajustar = null)
    {
        var limiteDeLlamadas = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = LeerLlamadasPorMinuto(),
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = SegmentosDeLaVentana,
            QueueLimit = 0,
        });

        servicios.AddHttpClient<IGeneradorDeTexto, GeneradorDeTextoPorApi>(cliente =>
            {
                var url = GeneradorDeTextoPorApi.LeerVariable("IA_API_URL", GeneradorDeTextoPorApi.UrlPorDefecto);
                cliente.BaseAddress = new Uri(url.TrimEnd('/') + "/");
                cliente.Timeout = TimeSpan.FromSeconds(SegundosMaximosEnTotal + 5);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(SegundosMaximosParaConectar)
            })
            .AddStandardResilienceHandler(opciones =>
            {
                opciones.RateLimiter.RateLimiter = argumentos =>
                    limiteDeLlamadas.AcquireAsync(1, argumentos.Context.CancellationToken);
                opciones.Retry.MaxRetryAttempts = ReintentosMaximos;
                opciones.AttemptTimeout.Timeout = TimeSpan.FromSeconds(SegundosMaximosPorIntento);
                opciones.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(SegundosMaximosEnTotal);
                opciones.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(SegundosDeMuestreoDelCircuito);
                opciones.CircuitBreaker.MinimumThroughput = SolicitudesMinimasParaCortar;
                opciones.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(SegundosDeCorteDeCircuito);
                ajustar?.Invoke(opciones);
            });

        return servicios;
    }

    public static int LeerLlamadasPorMinuto()
    {
        var valor = Environment.GetEnvironmentVariable("IA_API_LLAMADAS_POR_MINUTO");
        if (string.IsNullOrWhiteSpace(valor))
        {
            return LlamadasPorMinutoPorDefecto;
        }

        if (!int.TryParse(valor, out var llamadas) || llamadas < 1)
        {
            throw new InvalidOperationException("IA_API_LLAMADAS_POR_MINUTO debe ser un número entero mayor que 0.");
        }

        return llamadas;
    }
}
