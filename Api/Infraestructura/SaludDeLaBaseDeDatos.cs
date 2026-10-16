using Microsoft.Extensions.Diagnostics.HealthChecks;
using SOTRA_BACKEND.Contexto;

namespace Api.Infraestructura;

/// <summary>Comprueba que la API puede hablar con PostgreSQL. No devuelve detalles de la falla.</summary>
public sealed class SaludDeLaBaseDeDatos(SotraContext contexto, ILogger<SaludDeLaBaseDeDatos> registro) : IHealthCheck
{
    public const string EtiquetaDisponibilidad = "disponibilidad";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext contextoDeSalud, CancellationToken cancelacion = default)
    {
        try
        {
            return await contexto.Database.CanConnectAsync(cancelacion)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            registro.LogError(excepcion, "La comprobación de disponibilidad de la base de datos falló");
            return HealthCheckResult.Unhealthy();
        }
    }
}
