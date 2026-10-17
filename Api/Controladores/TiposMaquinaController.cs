using Api.DTOs;
using Api.DTOs.TiposMaquina;
using Api.Mapeadores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    [HttpGet]
    [ProducesResponseType<PaginaDto<TipoMaquinaDto>>(StatusCodes.Status200OK)]
    public ActionResult<PaginaDto<TipoMaquinaDto>> ObtenerTodos([FromQuery] FiltrosTiposMaquinaDto filtros)
    {
        if (!PaginaDto<TipoMaquinaDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<TipoMaquinaDto>.ErrorDePaginacion());
        }

        if (!ConsultasDeTexto.TryTraducirEstado(filtros.Estado, out var estado))
        {
            return Ok(PaginaDto<TipoMaquinaDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var filtro = new FiltroTiposMaquina(filtros.Buscar, estado);
        var (entidades, total) = _tipoMaquinaDao.ListarPagina(filtro, filtros.Pagina, filtros.TamanoPagina);
        var elementos = entidades.Select(TipoMaquinaMapeador.ADto).ToList();
        return Ok(PaginaDto<TipoMaquinaDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<TipoMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TipoMaquinaDto> ObtenerPorId(long id)
    {
        var tipo = _tipoMaquinaDao.SeleccionarTipoMaquina(id);
        return tipo is null ? NotFound() : Ok(TipoMaquinaMapeador.ADto(tipo));
    }

    [HttpPost]
    [ProducesResponseType<TipoMaquinaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<TipoMaquinaDto> Crear(TipoMaquinaCrearDto solicitud)
    {
        try
        {
            var tipoCreado = _tipoMaquinaDao.InsertarTipoMaquina(
                TipoMaquinaMapeador.AEntidad(solicitud));
            var respuesta = TipoMaquinaMapeador.ADto(tipoCreado);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = tipoCreado.IdTipoMaquina }, respuesta);
        }
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { codigo = "TIPO_MAQUINA_DUPLICADO", mensaje = "Ya existe un tipo de máquina con ese nombre." });
        }
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<TipoMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<TipoMaquinaDto> Actualizar(long id, TipoMaquinaActualizarDto solicitud)
    {
        try
        {
            var tipo = _tipoMaquinaDao.SeleccionarTipoMaquina(id);
            if (tipo is null) return NotFound();

            TipoMaquinaMapeador.AplicarCambios(tipo, solicitud);
            _tipoMaquinaDao.ActualizarTipoMaquina(tipo);

            return Ok(TipoMaquinaMapeador.ADto(tipo));
        }
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { codigo = "TIPO_MAQUINA_DUPLICADO", mensaje = "Ya existe un tipo de máquina con ese nombre." });
        }
    }

    [HttpPatch("{id:long}")]
    [ProducesResponseType<TipoMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TipoMaquinaDto> Desactivar(long id, TipoMaquinaEstadoDto solicitud)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { mensaje = "Este endpoint solo acepta el estado 'inactivo'." });

        if (!_tipoMaquinaDao.DarBajaLogicaTipoMaquina(id)) return NotFound();

        var tipo = _tipoMaquinaDao.SeleccionarTipoMaquina(id)!;
        return Ok(TipoMaquinaMapeador.ADto(tipo));
    }
}
