using Api.DTOs.Reportes;
using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

/// <summary>Distribución de la demanda correctiva por mes del año y tipo de máquina (A9).</summary>
[ApiController]
[Route("api/v1/reportes/estacionalidad")]
public sealed class EstacionalidadController(EstacionalidadDAO dao, CacheDeAnalitica cache) : ControllerBase
{
    /// <summary>Acumula las correctivas por mes UTC de creación. Sin fechas incluye todo el historial.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<EstacionalidadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EstacionalidadDto>> Obtener([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        if (filtros.FechaHasta == DateOnly.MaxValue ||
            (filtros.FechaDesde.HasValue && filtros.FechaHasta.HasValue && filtros.FechaDesde > filtros.FechaHasta))
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "El rango de fechas no es válido.");
        if (filtros.ClienteId is <= 0 || filtros.ActivoId is <= 0)
            return this.RespuestaDeError(400, "FILTROS_INVALIDOS", "Los identificadores deben ser mayores que cero.");

        var filtro = new FiltroAnalitica(filtros.FechaDesde, filtros.FechaHasta, filtros.ClienteId, filtros.ActivoId);
        return Ok(await cache.ObtenerAsync($"estacionalidad|{filtro}", async token =>
        {
            var filas = await dao.ObtenerAsync(filtro, token);
            var tipos = filas.GroupBy(fila => (fila.TipoMaquinaId, fila.TipoMaquina))
                .Select(grupo => new TipoEstacionalidadDto(grupo.Key.TipoMaquinaId, grupo.Key.TipoMaquina,
                    grupo.Sum(fila => fila.Correctivas), CompletarMeses(grupo))).ToList();
            return new EstacionalidadDto(filtro.Desde, filtro.Hasta, filas.Sum(fila => fila.Correctivas), CompletarMeses(filas), tipos);
        }, cancelacion));
    }

    private static List<MesEstacionalidadDto> CompletarMeses(IEnumerable<EstacionalidadFila> filas)
    {
        var cantidades = filas.GroupBy(fila => fila.Mes).ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(fila => fila.Correctivas));
        return Enumerable.Range(1, 12).Select(mes => new MesEstacionalidadDto(mes, cantidades.GetValueOrDefault(mes))).ToList();
    }
}
