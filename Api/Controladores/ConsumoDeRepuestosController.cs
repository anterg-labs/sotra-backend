using Api.DTOs.Reportes;
using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/reportes/consumo-repuestos")]
public sealed class ConsumoDeRepuestosController(ConsumoDeRepuestosDAO dao, CacheDeAnalitica cache) : ControllerBase
{
    /// <summary>Unidades descontadas del stock al cerrar órdenes, agrupadas por repuesto y fecha de egreso UTC.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<ConsumoDeRepuestosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ConsumoDeRepuestosDto>> Obtener([FromQuery] FiltrosAnaliticaDto filtros,
        CancellationToken cancelacion)
    {
        if (filtros.FechaHasta == DateOnly.MaxValue ||
            (filtros.FechaDesde.HasValue && filtros.FechaHasta.HasValue && filtros.FechaDesde > filtros.FechaHasta))
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "El rango de fechas no es válido.");
        if (filtros.ClienteId is <= 0 || filtros.ActivoId is <= 0)
            return this.RespuestaDeError(400, "FILTROS_INVALIDOS", "Los identificadores deben ser mayores que cero.");

        var filtro = new FiltroAnalitica(filtros.FechaDesde, filtros.FechaHasta, filtros.ClienteId, filtros.ActivoId);
        return Ok(await cache.ObtenerAsync($"consumo-repuestos|{filtro}", async token =>
        {
            var filas = await dao.ObtenerAsync(filtro, token);
            return new ConsumoDeRepuestosDto(filtro.Desde, filtro.Hasta,
                filas.Sum(fila => fila.Unidades),
                filas.Select(fila => new RepuestoConsumidoDto(fila.RepuestoId, fila.Codigo,
                    fila.Repuesto, fila.Unidades, fila.Ordenes)).ToList());
        }, cancelacion));
    }
}
