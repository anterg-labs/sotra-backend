using Api.DTOs.Reportes;
using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

/// <summary>Correctivas de cada activo frente a su edad y su última lectura de uso conocida.</summary>
[ApiController]
[Route("api/v1/reportes/analisis/desgaste-edad-uso")]
public sealed class DesgasteEdadUsoController(DesgasteEdadUsoDAO dao, CacheDeAnalitica cache) : ControllerBase
{
    /// <summary>Una observación por activo con órdenes en el período, con edad, lectura de uso y correctivas.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<DesgasteEdadUsoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DesgasteEdadUsoDto>> Obtener([FromQuery] FiltrosAnaliticaDto filtros,
        CancellationToken cancelacion)
    {
        if (filtros.FechaHasta == DateOnly.MaxValue ||
            (filtros.FechaDesde.HasValue && filtros.FechaHasta.HasValue && filtros.FechaDesde > filtros.FechaHasta))
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "El rango de fechas no es válido.");
        if (filtros.ClienteId is <= 0 || filtros.ActivoId is <= 0)
            return this.RespuestaDeError(400, "FILTROS_INVALIDOS", "Los identificadores deben ser mayores que cero.");

        var filtro = new FiltroAnalitica(filtros.FechaDesde, filtros.FechaHasta, filtros.ClienteId, filtros.ActivoId);
        return Ok(await cache.ObtenerAsync($"desgaste-edad-uso|{filtro}", async token =>
        {
            var filas = await dao.ObtenerAsync(filtro, token);
            var activos = filas.Select(fila => new ActivoEdadUsoDto(fila.ActivoId, fila.NombreActivo,
                fila.Modelo, fila.EdadAnios, fila.LecturaUso, fila.UnidadUso, fila.FechaLectura,
                fila.Correctivas, fila.OrdenesObservadas)).ToList();
            return new DesgasteEdadUsoDto(filtro.Desde, filtro.Hasta, activos.Count,
                activos.Count(activo => activo.EdadAnios is not null),
                activos.Count(activo => activo.LecturaUso is not null), activos);
        }, cancelacion));
    }
}
