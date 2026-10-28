using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Infraestructura;

public sealed class OpcionesDeLimiteDeSolicitudes
{
    public const string Seccion = "LimiteSolicitudes";

    public bool Habilitado { get; init; } = true;

    /// <summary>Solicitudes por minuto y por dirección IP en toda la API.</summary>
    [Range(1, 100_000)]
    public int PorMinutoGeneral { get; init; } = 1200;

    /// <summary>Solicitudes por minuto para las sugerencias de IA, que son costosas.</summary>
    [Range(1, 100_000)]
    public int PorMinutoSugerencias { get; init; } = 20;

    /// <summary>Solicitudes por minuto para las subidas de archivos.</summary>
    [Range(1, 100_000)]
    public int PorMinutoSubidas { get; init; } = 30;

    /// <summary>Solicitudes por minuto para las altas y modificaciones.</summary>
    [Range(1, 100_000)]
    public int PorMinutoEscrituras { get; init; } = 300;
}

public static class PoliticasDeLimite
{
    public const string Sugerencias = "sugerencias";
    public const string Subidas = "subidas";
    public const string Errores = "errores";
}

public static class ConfiguracionDeLimiteDeSolicitudes
{
    private const int SegundosDeLaVentana = 60;

    public static IServiceCollection AgregarLimiteDeSolicitudes(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddOptions<OpcionesDeLimiteDeSolicitudes>()
            .Bind(configuracion.GetSection(OpcionesDeLimiteDeSolicitudes.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var opciones = configuracion.GetSection(OpcionesDeLimiteDeSolicitudes.Seccion)
            .Get<OpcionesDeLimiteDeSolicitudes>() ?? new OpcionesDeLimiteDeSolicitudes();

        servicios.AddRateLimiter(limitador =>
        {
            limitador.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limitador.OnRejected = EscribirRechazo;

            if (!opciones.Habilitado)
            {
                limitador.AddPolicy(PoliticasDeLimite.Sugerencias, _ => RateLimitPartition.GetNoLimiter("libre"));
                limitador.AddPolicy(PoliticasDeLimite.Subidas, _ => RateLimitPartition.GetNoLimiter("libre"));
                limitador.AddPolicy(PoliticasDeLimite.Errores, _ => RateLimitPartition.GetNoLimiter("libre"));
                return;
            }

            var general = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
                Particion(contexto, "general", opciones.PorMinutoGeneral));
            var escrituras = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
                EsLectura(contexto.Request.Method)
                    ? RateLimitPartition.GetNoLimiter("lectura")
                    : Particion(contexto, "escrituras", opciones.PorMinutoEscrituras));
            limitador.GlobalLimiter = PartitionedRateLimiter.CreateChained(general, escrituras);

            AgregarPolitica(limitador, PoliticasDeLimite.Sugerencias, opciones.PorMinutoSugerencias);
            AgregarPolitica(limitador, PoliticasDeLimite.Subidas, opciones.PorMinutoSubidas);
            AgregarPolitica(limitador, PoliticasDeLimite.Errores, 20);
        });

        return servicios;
    }

    private static void AgregarPolitica(RateLimiterOptions limitador, string nombre, int porMinuto)
    {
        limitador.AddPolicy(nombre, contexto => Particion(contexto, nombre, porMinuto));
    }

    private static bool EsLectura(string metodo)
    {
        return HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || HttpMethods.IsOptions(metodo);
    }

    private static RateLimitPartition<string> Particion(HttpContext contexto, string politica, int porMinuto)
    {
        var direccion = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        return RateLimitPartition.GetSlidingWindowLimiter(
            $"{politica}:{direccion}",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = porMinuto,
                Window = TimeSpan.FromSeconds(SegundosDeLaVentana),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
            });
    }

    private static async ValueTask EscribirRechazo(OnRejectedContext contexto, CancellationToken cancelacion)
    {
        var respuesta = contexto.HttpContext.Response;
        // Un limitador encadenado no siempre expone la espera exacta: se informa la ventana completa.
        var segundosDeEspera = SegundosDeLaVentana;
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
        {
            segundosDeEspera = (int)Math.Ceiling(espera.TotalSeconds);
        }

        respuesta.Headers.RetryAfter = segundosDeEspera.ToString();

        var servicio = contexto.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await servicio.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto.HttpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Extensions =
                {
                    ["codigo"] = "DEMASIADAS_SOLICITUDES",
                    ["mensaje"] = "Hiciste demasiadas solicitudes en poco tiempo. Esperá un momento e intentá de nuevo.",
                },
            },
        });
    }
}
