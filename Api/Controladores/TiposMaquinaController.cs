using Api.DTOs;
using Api.DTOs.TiposMaquina;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
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

    [Authorize(Policy = Politicas.TipoMaquinaConsultar)]
    [HttpGet]
    [ProducesResponseType<PaginaDto<TipoMaquinaDto>>(StatusCodes.Status200OK)]
    public ActionResult<PaginaDto<TipoMaquinaDto>> ObtenerTodos([FromQuery] FiltrosTiposMaquinaDto filtros)
    {
        if (!PaginaDto<TipoMaquinaDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<TipoMaquinaDto>.ErrorDePaginacion());
        }

        var textoBuscado = "";
        if (!string.IsNullOrWhiteSpace(filtros.Buscar))
        {
            textoBuscado = filtros.Buscar.Trim().ToLower();
        }

        var respuesta = new List<TipoMaquinaDto>();
        foreach (var tipo in _tipoMaquinaDao.ListarTodo())
        {
            var tipoDto = TipoMaquinaMapeador.ADto(tipo);

            if (textoBuscado != "" && !PaginaDto<TipoMaquinaDto>.Contiene(tipoDto.Nombre, textoBuscado))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(filtros.Estado) && tipoDto.Estado != filtros.Estado)
            {
                continue;
            }

            respuesta.Add(tipoDto);
        }

        return Ok(PaginaDto<TipoMaquinaDto>.Crear(respuesta, filtros.Pagina, filtros.TamanoPagina));
    }

    [Authorize(Policy = Politicas.TipoMaquinaConsultar)]
    [HttpGet("{id:long}")]
    [ProducesResponseType<TipoMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TipoMaquinaDto> ObtenerPorId(long id)
    {
        var tipo = _tipoMaquinaDao.SeleccionarTipoMaquina(id);
        return tipo is null ? NotFound() : Ok(TipoMaquinaMapeador.ADto(tipo));
    }

    [Authorize(Policy = Politicas.TipoMaquinaGestionar)]
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
            return Conflict(new { mensaje = "Ya existe un tipo de máquina con ese nombre." });
        }
    }

    [Authorize(Policy = Politicas.TipoMaquinaGestionar)]
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
            return Conflict(new { mensaje = "Ya existe un tipo de máquina con ese nombre." });
        }
    }

    [Authorize(Policy = Politicas.TipoMaquinaGestionar)]
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
