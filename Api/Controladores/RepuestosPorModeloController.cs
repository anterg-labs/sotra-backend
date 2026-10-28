using Api.DTOs.Reportes;
using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

/// <summary>Repuestos asociados a órdenes correctivas de cada modelo de máquina (A12).</summary>
[ApiController]
[Route("api/v1/reportes/repuestos-por-modelo")]
public sealed class RepuestosPorModeloController(RepuestosPorModeloDAO dao, CacheDeAnalitica cache) : ControllerBase
{
    /// <summary>Cuenta órdenes por repuesto y modelo según la fecha UTC de creación.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<RepuestosPorModeloDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RepuestosPorModeloDto>> Obtener([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        if (filtros.FechaHasta == DateOnly.MaxValue ||
            (filtros.FechaDesde.HasValue && filtros.FechaHasta.HasValue && filtros.FechaDesde > filtros.FechaHasta))
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "El rango de fechas no es válido.");
        if (filtros.ClienteId is <= 0 || filtros.ActivoId is <= 0)
            return this.RespuestaDeError(400, "FILTROS_INVALIDOS", "Los identificadores deben ser mayores que cero.");

        var filtro = new FiltroAnalitica(filtros.FechaDesde, filtros.FechaHasta, filtros.ClienteId, filtros.ActivoId);
        return Ok(await cache.ObtenerAsync($"repuestos-por-modelo|{filtro}", async token =>
        {
            var filas = await dao.ObtenerAsync(filtro, token);
            var modelos = filas.GroupBy(fila => (fila.ModeloId, fila.Modelo, fila.TipoMaquinaId, fila.TipoMaquina))
                .Select(grupo => new ModeloConRepuestosDto(grupo.Key.ModeloId, grupo.Key.Modelo,
                    grupo.Key.TipoMaquinaId, grupo.Key.TipoMaquina,
                    grupo.Select(fila => new RepuestoDeModeloDto(fila.RepuestoId, fila.Codigo,
                        fila.Repuesto, fila.OrdenesAsociadas, fila.OrdenesSuministradas)).ToList())).ToList();
            return new RepuestosPorModeloDto(filtro.Desde, filtro.Hasta, modelos);
        }, cancelacion));
    }
}
