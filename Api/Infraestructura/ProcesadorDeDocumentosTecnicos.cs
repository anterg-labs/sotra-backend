using Api.Servicios;

namespace Api.Infraestructura;

/// <summary>
/// Procesa en segundo plano los documentos técnicos que se usan para las sugerencias de IA: extrae los fragmentos
/// del PDF y calcula sus embeddings, de a un documento por vez. Al arrancar retoma los que quedaron pendientes o a
/// medio procesar (por ejemplo, si la API se reinició durante un procesamiento).
/// </summary>
public sealed class ProcesadorDeDocumentosTecnicos : BackgroundService
{
    private readonly ColaDeDocumentosTecnicos _cola;
    private readonly IServiceScopeFactory _alcances;
    private readonly ILogger<ProcesadorDeDocumentosTecnicos> _registro;

    public ProcesadorDeDocumentosTecnicos(ColaDeDocumentosTecnicos cola, IServiceScopeFactory alcances,
        ILogger<ProcesadorDeDocumentosTecnicos> registro)
    {
        _cola = cola;
        _alcances = alcances;
        _registro = registro;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Deja terminar el arranque de la API antes de consultar la base.
        await Task.Yield();
        await RetomarPendientesAsync(stoppingToken);

        await foreach (var documentoId in _cola.LeerAsync(stoppingToken))
        {
            try
            {
                await using var alcance = _alcances.CreateAsyncScope();
                var servicio = alcance.ServiceProvider.GetRequiredService<ServicioDocumentosTecnicos>();
                // Los embeddings ocupan la CPU de forma sincrónica: se calculan fuera del hilo del bucle.
                await Task.Run(() => servicio.ProcesarAsync(documentoId, stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // El documento queda "procesando" y se retoma en el próximo arranque.
                return;
            }
            catch (Exception excepcion)
            {
                _registro.LogError(excepcion, "Falló el procesamiento en segundo plano del documento técnico {DocumentoId}",
                    documentoId);
            }
        }
    }

    private async Task RetomarPendientesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var alcance = _alcances.CreateAsyncScope();
            var servicio = alcance.ServiceProvider.GetRequiredService<ServicioDocumentosTecnicos>();
            var pendientes = await servicio.ListarPendientesDeProcesarAsync(cancellationToken);
            foreach (var documentoId in pendientes)
            {
                _cola.Encolar(documentoId);
            }

            if (pendientes.Count > 0)
            {
                _registro.LogInformation("Se retoman {Cantidad} documentos técnicos pendientes de procesar", pendientes.Count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception excepcion)
        {
            _registro.LogError(excepcion, "No se pudieron leer los documentos técnicos pendientes de procesar");
        }
    }
}
