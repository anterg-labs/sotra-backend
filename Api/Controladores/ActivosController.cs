using Api.Infraestructura;
using Api.DTOs;
using Api.DTOs.Activos;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/activos")]
public class ActivosController : ControllerBase
{
    private readonly ActivoDAO _activoDao;

    public ActivosController(ActivoDAO activoDao)
    {
        _activoDao = activoDao;
    }

    [HttpGet]
    [Authorize(Policy = Politicas.ActivoConsultar)]
    [ProducesResponseType<PaginaDto<ActivoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<ActivoDto>>> ObtenerTodos([FromQuery] FiltrosActivosDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<ActivoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<ActivoDto>.ErrorDePaginacion());
        }

        if (!ActivoDAO.ColumnasOrdenables.Contiene(filtros.OrdenarPor))
        {
            return this.OrdenInvalido(ActivoDAO.ColumnasOrdenables.Claves);
        }

        if (!ConsultasDeTexto.TryTraducirEstado(filtros.Estado, out var estado))
        {
            return Ok(PaginaDto<ActivoDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var filtro = new FiltroActivos(filtros.Buscar, filtros.ClienteId, filtros.ModeloMaquinaId, estado, filtros.OrdenarPor, filtros.DireccionOrden == "desc");
        var (entidades, total) = await _activoDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var elementos = entidades.Select(ActivoMapeador.ADto).ToList();
        return Ok(PaginaDto<ActivoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = Politicas.ActivoConsultar)]
    [ProducesResponseType<ActivoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivoDto>> ObtenerPorId(long id, CancellationToken cancelacion)
    {
        var activo = await _activoDao.SeleccionarActivoAsync(id, cancelacion);
        return activo is null ? NotFound() : Ok(ActivoMapeador.ADto(activo));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.ActivoCrear)]
    [ProducesResponseType<ActivoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivoDto>> Crear(ActivoCrearDto solicitud, CancellationToken cancelacion)
    {
        var activoCreado = await _activoDao.InsertarActivoAsync(ActivoMapeador.AEntidad(solicitud), cancelacion);
        var respuesta = ActivoMapeador.ADto(activoCreado);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = activoCreado.IdActivo }, respuesta);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.ActivoEditar)]
    [ProducesResponseType<ActivoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivoDto>> Actualizar(long id, ActivoActualizarDto solicitud, CancellationToken cancelacion)
    {
        var activo = await _activoDao.SeleccionarActivoAsync(id, cancelacion);
        if (activo is null) return NotFound();

        ActivoMapeador.AplicarCambios(activo, solicitud);
        await _activoDao.ActualizarActivoAsync(activo, cancelacion);
        return Ok(ActivoMapeador.ADto(activo));
    }

    [HttpPatch("{id:long}")]
    [Authorize(Policy = Politicas.ActivoEditar)]
    [ProducesResponseType<ActivoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivoDto>> Desactivar(long id, ActivoEstadoDto solicitud, CancellationToken cancelacion)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
            return this.RespuestaDeError(400, "ESTADO_INVALIDO", "Este endpoint solo acepta el estado 'inactivo'.");

        if (!await _activoDao.DarBajaLogicaActivoAsync(id, cancelacion)) return NotFound();

        var activo = (await _activoDao.SeleccionarActivoAsync(id, cancelacion))!;
        return Ok(ActivoMapeador.ADto(activo));
    }
}
