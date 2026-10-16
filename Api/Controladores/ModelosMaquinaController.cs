using Api.Infraestructura.Seguridad;
using Api.DTOs;
using Api.DTOs.ModelosMaquina;
using Api.Mapeadores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/modelos-maquina")]
public class ModelosMaquinaController : ControllerBase
{
    private readonly ModeloMaquinaDAO _modeloMaquinaDao;

    public ModelosMaquinaController(ModeloMaquinaDAO modeloMaquinaDao)
    {
        _modeloMaquinaDao = modeloMaquinaDao;
    }

    [HttpGet]
    [Permiso("modelo.consultar")]
    [ProducesResponseType<PaginaDto<ModeloMaquinaDto>>(StatusCodes.Status200OK)]
    public ActionResult<PaginaDto<ModeloMaquinaDto>> ObtenerTodos([FromQuery] FiltrosModelosMaquinaDto filtros)
    {
        if (!PaginaDto<ModeloMaquinaDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<ModeloMaquinaDto>.ErrorDePaginacion());
        }

        if (!ConsultasDeTexto.TryTraducirEstado(filtros.Estado, out var estado))
        {
            return Ok(PaginaDto<ModeloMaquinaDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var filtro = new FiltroModelosMaquina(filtros.Buscar, filtros.TipoMaquinaId, estado);
        var (entidades, total) = _modeloMaquinaDao.ListarPagina(filtro, filtros.Pagina, filtros.TamanoPagina);
        var elementos = entidades.Select(ModeloMaquinaMapeador.ADto).ToList();
        return Ok(PaginaDto<ModeloMaquinaDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("{id:long}")]
    [Permiso("modelo.consultar")]
    [ProducesResponseType<ModeloMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ModeloMaquinaDto> ObtenerPorId(long id)
    {
        var modelo = _modeloMaquinaDao.SeleccionarModeloMaquina(id);
        return modelo is null ? NotFound() : Ok(ModeloMaquinaMapeador.ADto(modelo));
    }

    [HttpPost]
    [Permiso("modelo.crear")]
    [ProducesResponseType<ModeloMaquinaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<ModeloMaquinaDto> Crear(ModeloMaquinaCrearDto solicitud)
    {
        try
        {
            var modeloCreado = _modeloMaquinaDao.InsertarModeloMaquina(
                ModeloMaquinaMapeador.AEntidad(solicitud));
            var respuesta = ModeloMaquinaMapeador.ADto(modeloCreado);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = modeloCreado.IdModeloMaquina }, respuesta);
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
        catch (ConflictoDeDatosException excepcion)
        {
            return Conflict(new { codigo = excepcion.Codigo, mensaje = excepcion.Message });
        }
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new
            {
                codigo = "MODELO_DUPLICADO",
                mensaje = "Ya existe un modelo de máquina con ese nombre para el tipo seleccionado."
            });
        }
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<ModeloMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<ModeloMaquinaDto> Actualizar(long id, ModeloMaquinaActualizarDto solicitud)
    {
        try
        {
            var modelo = _modeloMaquinaDao.SeleccionarModeloMaquina(id);
            if (modelo is null) return NotFound();

            ModeloMaquinaMapeador.AplicarCambios(modelo, solicitud);
            _modeloMaquinaDao.ActualizarModeloMaquina(modelo);
            return Ok(ModeloMaquinaMapeador.ADto(modelo));
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
        catch (ConflictoDeDatosException excepcion)
        {
            return Conflict(new { codigo = excepcion.Codigo, mensaje = excepcion.Message });
        }
        catch (DbUpdateException excepcion)
            when (excepcion.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new
            {
                codigo = "MODELO_DUPLICADO",
                mensaje = "Ya existe un modelo de máquina con ese nombre para el tipo seleccionado."
            });
        }
    }

    [HttpPatch("{id:long}")]
    [ProducesResponseType<ModeloMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ModeloMaquinaDto> Desactivar(long id, ModeloMaquinaEstadoDto solicitud)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { mensaje = "Este endpoint solo acepta el estado 'inactivo'." });

        if (!_modeloMaquinaDao.DarBajaLogicaModeloMaquina(id)) return NotFound();

        var modelo = _modeloMaquinaDao.SeleccionarModeloMaquina(id)!;
        return Ok(ModeloMaquinaMapeador.ADto(modelo));
    }
}
