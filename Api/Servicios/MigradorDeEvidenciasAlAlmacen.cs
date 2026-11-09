using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;

namespace Api.Servicios;

/// <summary>
/// Al arrancar la API con R2 configurado, pasa a R2 los archivos de evidencias que todavía están en la base
/// (columna archivo) y los borra de la base una vez subidos. Avanza de a pocas filas para no cargar la memoria; si R2
/// falla se detiene y lo vuelve a intentar en el próximo arranque. Es seguro correrlo varias veces.
/// </summary>
public sealed class MigradorDeEvidenciasAlAlmacen : BackgroundService
{
    private const int FilasPorTanda = 20;

    private readonly IServiceScopeFactory _fabricaDeAmbitos;
    private readonly ILogger<MigradorDeEvidenciasAlAlmacen> _registro;

    public MigradorDeEvidenciasAlAlmacen(IServiceScopeFactory fabricaDeAmbitos,
        ILogger<MigradorDeEvidenciasAlAlmacen> registro)
    {
        _fabricaDeAmbitos = fabricaDeAmbitos;
        _registro = registro;
    }

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        await Task.Yield();

        var migradas = 0;
        try
        {
            while (!cancelacion.IsCancellationRequested)
            {
                var migradasEnLaTanda = await MigrarTandaAsync(cancelacion);
                if (migradasEnLaTanda == 0)
                {
                    break;
                }

                migradas += migradasEnLaTanda;
            }
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
            return;
        }
        catch (Exception error)
        {
            _registro.LogWarning(error,
                "La migración de evidencias a R2 se detuvo después de {Migradas} archivos; se reintenta en el próximo arranque.",
                migradas);
            return;
        }

        if (migradas > 0)
        {
            _registro.LogInformation("Se pasaron {Migradas} archivos de evidencias de la base a R2.", migradas);
        }
    }

    private async Task<int> MigrarTandaAsync(CancellationToken cancelacion)
    {
        using var ambito = _fabricaDeAmbitos.CreateScope();
        var almacen = ambito.ServiceProvider.GetService<IAlmacenDeArchivos>();
        if (almacen is null)
        {
            return 0;
        }

        var contexto = ambito.ServiceProvider.GetRequiredService<SotraContext>();
        var evidencias = await contexto.EvidenciasOt
            .Where(evidencia => evidencia.ClaveArchivo == null && evidencia.Archivo != null)
            .OrderBy(evidencia => evidencia.IdEvidencia)
            .Take(FilasPorTanda)
            .ToListAsync(cancelacion);

        foreach (var evidencia in evidencias)
        {
            var tipoContenido = evidencia.TipoContenido ?? "application/octet-stream";
            var clave = ClaveDeEvidencia.Crear(evidencia.IdOrden, evidencia.TipoContenido);
            await almacen.GuardarAsync(clave, evidencia.Archivo!, tipoContenido, cancelacion);
            evidencia.ClaveArchivo = clave;
            evidencia.Archivo = null;
            await contexto.SaveChangesAsync(cancelacion);
        }

        return evidencias.Count;
    }
}
