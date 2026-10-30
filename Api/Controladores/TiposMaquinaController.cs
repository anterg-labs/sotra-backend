using Api.Infraestructura;
using Api.DTOs;
using Api.DTOs.TiposMaquina;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/tipos-maquina")]
public class TiposMaquinaController : ControllerBase
{
    private readonly TipoMaquinaDAO _tipoMaquinaDao;

    public TiposMaquinaController(TipoMaquinaDAO tipoMaquinaDao)
    {
        _tipoMaquinaDao = tipoMaquinaDao;
    }

    /// <summary>Lista los tipos de máquina con filtros por texto y estado.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.TipoMaquinaConsultar)]
    [ProducesResponseType<PaginaDto<TipoMaquinaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<TipoMaquinaDto>>> ObtenerTodos([FromQuery] FiltrosTiposMaquinaDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<TipoMaquinaDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return this.PaginacionInvalida();
        }

        if (!TipoMaquinaDAO.ColumnasOrdenables.Contiene(filtros.OrdenarPor))
        {
            return this.OrdenInvalido(TipoMaquinaDAO.ColumnasOrdenables.Claves);
        }

        if (!ConsultasDeTexto.TryTraducirEstado(filtros.Estado, out var estado))
        {
            return Ok(PaginaDto<TipoMaquinaDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var filtro = new FiltroTiposMaquina(filtros.Buscar, estado, filtros.OrdenarPor, filtros.DireccionOrden == "desc");
        var (entidades, total) = await _tipoMaquinaDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var elementos = entidades.Select(TipoMaquinaMapeador.ADto).ToList();
        return Ok(PaginaDto<TipoMaquinaDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    /// <summary>Devuelve un tipo de máquina por su id.</summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = Politicas.TipoMaquinaConsultar)]
    [ProducesResponseType<TipoMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TipoMaquinaDto>> ObtenerPorId(long id, CancellationToken cancelacion)
    {
        var tipo = await _tipoMaquinaDao.SeleccionarTipoMaquinaAsync(id, cancelacion);
        return tipo is null ? NotFound() : Ok(TipoMaquinaMapeador.ADto(tipo));
    }

    /// <summary>Registra un tipo de máquina.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.TipoMaquinaGestionar)]
    [ProducesResponseType<TipoMaquinaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TipoMaquinaDto>> Crear(TipoMaquinaCrearDto solicitud, CancellationToken cancelacion)
    {
        var tipoCreado = await _tipoMaquinaDao.InsertarTipoMaquinaAsync(
            TipoMaquinaMapeador.AEntidad(solicitud), cancelacion);
        var respuesta = TipoMaquinaMapeador.ADto(tipoCreado);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = tipoCreado.IdTipoMaquina }, respuesta);
    }

    /// <summary>Modifica un tipo de máquina.</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.TipoMaquinaGestionar)]
    [ProducesResponseType<TipoMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TipoMaquinaDto>> Actualizar(long id, TipoMaquinaActualizarDto solicitud, CancellationToken cancelacion)
    {
        var tipo = await _tipoMaquinaDao.SeleccionarTipoMaquinaAsync(id, cancelacion);
        if (tipo is null) return NotFound();

        TipoMaquinaMapeador.AplicarCambios(tipo, solicitud);
        await _tipoMaquinaDao.ActualizarTipoMaquinaAsync(tipo, cancelacion);

        return Ok(TipoMaquinaMapeador.ADto(tipo));
    }

    /// <summary>Cambia el estado de un tipo (baja o reactivación lógica).</summary>
    [HttpPatch("{id:long}")]
    [Authorize(Policy = Politicas.TipoMaquinaGestionar)]
    [ProducesResponseType<TipoMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TipoMaquinaDto>> Desactivar(long id, TipoMaquinaEstadoDto solicitud, CancellationToken cancelacion)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
            return this.RespuestaDeError(400, "ESTADO_INVALIDO", "Este endpoint solo acepta el estado 'inactivo'.");

        if (!await _tipoMaquinaDao.DarBajaLogicaTipoMaquinaAsync(id, cancelacion)) return NotFound();

        var tipo = (await _tipoMaquinaDao.SeleccionarTipoMaquinaAsync(id, cancelacion))!;
        return Ok(TipoMaquinaMapeador.ADto(tipo));
    }
}
