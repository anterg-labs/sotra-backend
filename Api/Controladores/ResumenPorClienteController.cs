using Api.DTOs.Reportes;
using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

/// <summary>Concentración de la demanda de mantenimiento por empresa cliente (A13).</summary>
[ApiController]
[Route("api/v1/reportes/resumen-por-cliente")]
public sealed class ResumenPorClienteController(ResumenPorClienteDAO dao, CacheDeAnalitica cache,
    IOptions<OpcionesDeAlertasDeMaquinaria> opciones) : ControllerBase
{
    /// <summary>Órdenes, correctivas, horas de parada y reincidencias por cliente.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<ResumenPorClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ResumenPorClienteDto>> Obtener([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        if (filtros.FechaHasta == DateOnly.MaxValue ||
            (filtros.FechaDesde.HasValue && filtros.FechaHasta.HasValue && filtros.FechaDesde > filtros.FechaHasta))
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "El rango de fechas no es válido.");
        if (filtros.ClienteId is <= 0 || filtros.ActivoId is <= 0)
            return this.RespuestaDeError(400, "FILTROS_INVALIDOS", "Los identificadores deben ser mayores que cero.");
        var dias = filtros.Dias ?? opciones.Value.ReincidenciaDias;
        if (dias is < 1 or > 3650)
            return this.RespuestaDeError(400, "PARAMETRO_INVALIDO", "Los días deben estar entre 1 y 3650.");

        var filtro = new FiltroAnalitica(filtros.FechaDesde, filtros.FechaHasta, filtros.ClienteId, filtros.ActivoId);
        return Ok(await cache.ObtenerAsync($"resumen-por-cliente|{dias}|{filtro}", async token =>
        {
            var filas = await dao.ObtenerAsync(filtro, dias, token);
            return new ResumenPorClienteDto(filtro.Desde, filtro.Hasta, dias,
                filas.Select(fila => new ClienteDeReportesDto(fila.ClienteId, fila.Cliente,
                    fila.Ordenes, fila.Correctivas, fila.CorrectivasCerradas,
                    Math.Round(fila.HorasParada, 2), fila.Reincidencias)).ToList());
        }, cancelacion));
    }
}
