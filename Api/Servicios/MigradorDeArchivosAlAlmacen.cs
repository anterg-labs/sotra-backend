using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;

namespace Api.Servicios;

/// <summary>
/// Al arrancar la API con R2 configurado, pasa a R2 los archivos que todavía están en la base: las evidencias de las
/// órdenes (operativo.evidencia_ot.archivo, a evidencias/) y los PDF de los documentos técnicos
/// (vectorial.documento_tecnico.archivo, a documentacion/). Cada archivo se borra de la base recién cuando quedó
/// subido. Avanza de a pocos archivos para no cargar la memoria; si R2 falla se detiene y lo vuelve a intentar en el
/// próximo arranque. Es seguro correrlo varias veces.
/// </summary>
public sealed class MigradorDeArchivosAlAlmacen : BackgroundService
{
    private const int EvidenciasPorTanda = 20;

    private readonly IServiceScopeFactory _fabricaDeAmbitos;
    private readonly ILogger<MigradorDeArchivosAlAlmacen> _registro;

    public MigradorDeArchivosAlAlmacen(IServiceScopeFactory fabricaDeAmbitos,
        ILogger<MigradorDeArchivosAlAlmacen> registro)
    {
        _fabricaDeAmbitos = fabricaDeAmbitos;
        _registro = registro;
    }

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        await Task.Yield();
        await MigrarAsync("evidencias", MigrarTandaDeEvidenciasAsync, cancelacion);
        await MigrarAsync("documentos técnicos", MigrarUnDocumentoAsync, cancelacion);
    }

    private async Task MigrarAsync(string que, Func<CancellationToken, Task<int>> migrarTanda, CancellationToken cancelacion)
    {
        var migrados = 0;
        try
        {
            while (!cancelacion.IsCancellationRequested)
            {
                var migradosEnLaTanda = await migrarTanda(cancelacion);
                if (migradosEnLaTanda == 0)
                {
                    break;
                }

                migrados += migradosEnLaTanda;
            }
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
            return;
        }
        catch (Exception error)
        {
            _registro.LogWarning(error,
                "El traspaso de {Que} a R2 se detuvo después de {Migrados} archivos; se reintenta en el próximo arranque.",
                que, migrados);
            return;
        }

        if (migrados > 0)
        {
            _registro.LogInformation("Se pasaron {Migrados} archivos de {Que} de la base a R2.", migrados, que);
        }
    }

    private async Task<int> MigrarTandaDeEvidenciasAsync(CancellationToken cancelacion)
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
            .Take(EvidenciasPorTanda)
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

    /// <summary>Un PDF por vez: pueden pesar hasta 50 MB.</summary>
    private async Task<int> MigrarUnDocumentoAsync(CancellationToken cancelacion)
    {
        using var ambito = _fabricaDeAmbitos.CreateScope();
        var almacen = ambito.ServiceProvider.GetService<IAlmacenDeArchivos>();
        if (almacen is null)
        {
            return 0;
        }

        await using var baseDatos = BaseVectorial.CrearConexion();
        await BaseVectorial.PrepararTablasAsync(baseDatos, cancelacion);

        long id;
        byte[] archivo;
        await using (var leer = baseDatos.CreateCommand("""
            SELECT id, archivo FROM vectorial.documento_tecnico
            WHERE clave_archivo IS NULL AND archivo IS NOT NULL AND length(archivo) > 0
            ORDER BY id
            LIMIT 1
            """))
        {
            await using var lector = await leer.ExecuteReaderAsync(cancelacion);
            if (!await lector.ReadAsync(cancelacion))
            {
                return 0;
            }

            id = lector.GetInt64(0);
            archivo = (byte[])lector[1];
        }

        var clave = ClaveDeDocumentoTecnico.Crear();
        await almacen.GuardarAsync(clave, archivo, "application/pdf", cancelacion);

        await using var actualizar = baseDatos.CreateCommand("""
            UPDATE vectorial.documento_tecnico
            SET clave_archivo = $2, archivo = NULL
            WHERE id = $1 AND clave_archivo IS NULL
            """);
        actualizar.Parameters.AddWithValue(id);
        actualizar.Parameters.AddWithValue(clave);
        if (await actualizar.ExecuteNonQueryAsync(cancelacion) == 0)
        {
            // El documento se borró mientras se subía: no queda nada que apunte al PDF recién subido.
            await almacen.BorrarAsync(clave, cancelacion);
        }

        return 1;
    }
}
