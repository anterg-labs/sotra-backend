using Api.DTOs;
using Api.DTOs.Activos;
using Api.Mapeadores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    [ProducesResponseType<PaginaDto<ActivoDto>>(StatusCodes.Status200OK)]
    public ActionResult<PaginaDto<ActivoDto>> ObtenerTodos([FromQuery] FiltrosActivosDto filtros)
    {
        if (!PaginaDto<ActivoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<ActivoDto>.ErrorDePaginacion());
        }

        var textoBuscado = "";
        if (!string.IsNullOrWhiteSpace(filtros.Buscar))
        {
            textoBuscado = filtros.Buscar.Trim().ToLower();
        }

        var respuesta = new List<ActivoDto>();
        foreach (var activo in _activoDao.ListarTodo())
        {
            var activoDto = ActivoMapeador.ADto(activo);

            if (textoBuscado != "")
            {
                var coincide = PaginaDto<ActivoDto>.Contiene(activoDto.Nombre, textoBuscado)
                               || PaginaDto<ActivoDto>.Contiene(activoDto.NumSerie, textoBuscado)
                               || PaginaDto<ActivoDto>.Contiene(activoDto.Ubicacion, textoBuscado)
                               || PaginaDto<ActivoDto>.Contiene(activoDto.Descripcion, textoBuscado);
                if (!coincide)
                {
                    continue;
                }
            }

            if (filtros.ClienteId is not null && activoDto.ClienteId != filtros.ClienteId)
            {
                continue;
            }

            if (filtros.ModeloMaquinaId is not null && activoDto.ModeloMaquinaId != filtros.ModeloMaquinaId)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(filtros.Estado) && activoDto.Estado != filtros.Estado)
            {
                continue;
            }

            respuesta.Add(activoDto);
        }

        return Ok(PaginaDto<ActivoDto>.Crear(respuesta, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<ActivoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ActivoDto> ObtenerPorId(long id)
    {
        var activo = _activoDao.SeleccionarActivo(id);
        return activo is null ? NotFound() : Ok(ActivoMapeador.ADto(activo));
    }

    [HttpPost]
    [ProducesResponseType<ActivoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<ActivoDto> Crear(ActivoCrearDto solicitud)
    {
        try
        {
            var activoCreado = _activoDao.InsertarActivo(ActivoMapeador.AEntidad(solicitud));
            var respuesta = ActivoMapeador.ADto(activoCreado);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = activoCreado.IdActivo }, respuesta);
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { codigo = "NUMERO_SERIE_DUPLICADO", mensaje = "Ya existe un activo con ese número de serie." });
        }
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<ActivoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<ActivoDto> Actualizar(long id, ActivoActualizarDto solicitud)
    {
        try
        {
            var activo = _activoDao.SeleccionarActivo(id);
            if (activo is null) return NotFound();

            ActivoMapeador.AplicarCambios(activo, solicitud);
            _activoDao.ActualizarActivo(activo);
            return Ok(ActivoMapeador.ADto(activo));
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { codigo = "NUMERO_SERIE_DUPLICADO", mensaje = "Ya existe un activo con ese número de serie." });
        }
    }

    [HttpPatch("{id:long}")]
    [ProducesResponseType<ActivoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ActivoDto> Desactivar(long id, ActivoEstadoDto solicitud)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { mensaje = "Este endpoint solo acepta el estado 'inactivo'." });

        if (!_activoDao.DarBajaLogicaActivo(id)) return NotFound();

        var activo = _activoDao.SeleccionarActivo(id)!;
        return Ok(ActivoMapeador.ADto(activo));
    }
}
