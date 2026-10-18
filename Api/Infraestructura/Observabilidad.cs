using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api.Infraestructura;

/// <summary>Métricas propias de SOTRA (medidor <c>Sotra.Api</c>). Sin exportador configurado cuestan prácticamente nada.</summary>
public static class MetricasDeNegocio
{
    public const string NombreDelMedidor = "Sotra.Api";
    public const string NombreDeLaFuente = "Sotra.Api";

    private static readonly Meter Medidor = new(NombreDelMedidor);

    public static readonly ActivitySource Fuente = new(NombreDeLaFuente);

    /// <summary>Acciones que cambian datos, por recurso, acción y clase de resultado (2xx, 4xx, 5xx).</summary>
    public static readonly Counter<long> Acciones =
        Medidor.CreateCounter<long>("sotra.acciones", description: "Acciones que cambian datos");

    public static readonly Counter<long> EntradasRechazadas =
        Medidor.CreateCounter<long>("sotra.entradas_rechazadas", description: "Solicitudes rechazadas por caracteres no permitidos");

    public static readonly Counter<long> ReportesGenerados =
        Medidor.CreateCounter<long>("sotra.reportes_generados", description: "Reportes descargados, por tipo y formato");

    public static readonly Histogram<double> DuracionDeSugerencias =
        Medidor.CreateHistogram<double>("sotra.sugerencias_ia.duracion", unit: "s",
            description: "Tiempo de generación de una sugerencia técnica, por resultado");

    public static string ClaseDeResultado(int codigoHttp) => $"{codigoHttp / 100}xx";
}

public static class ConfiguracionDeObservabilidad
{
    /// <summary>
    /// Trazas y métricas con OpenTelemetry. Se activan al definir <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (o
    /// <c>Observabilidad__Habilitada=true</c>); el destino, los encabezados y el muestreo usan las variables
    /// estándar <c>OTEL_*</c>. Sin ellas no se exporta nada y el comportamiento no cambia.
    /// </summary>
    public static IServiceCollection AgregarObservabilidad(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var hayDestino = !string.IsNullOrWhiteSpace(configuracion["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        var habilitada = configuracion.GetValue("Observabilidad:Habilitada", hayDestino);
        if (!habilitada)
        {
            return servicios;
        }

        var nombre = configuracion["OTEL_SERVICE_NAME"] ?? "sotra-api";
        var entorno = configuracion["ASPNETCORE_ENVIRONMENT"] ?? "Production";

        servicios.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso
                .AddService(nombre, serviceVersion: typeof(MetricasDeNegocio).Assembly.GetName().Version?.ToString())
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", entorno)]))
            .WithTracing(trazas => trazas
                .AddAspNetCoreInstrumentation(opciones => opciones.Filter = contexto =>
                    !contexto.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql")
                .AddSource(MetricasDeNegocio.NombreDeLaFuente))
            .WithMetrics(metricas => metricas
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Npgsql", "Microsoft.EntityFrameworkCore", MetricasDeNegocio.NombreDelMedidor))
            .UseOtlpExporter();

        return servicios;
    }
}
