using Api.DTOs.Repuestos;
using Api.Mapeadores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/repuestos")]
public class RepuestosController : ControllerBase
{
    private readonly RepuestoDAO _repuestoDao;

    public RepuestosController(RepuestoDAO repuestoDao)
    {
        _repuestoDao = repuestoDao;
    }

    [HttpGet]
    public ActionResult<List<RepuestoDto>> ObtenerTodos()
    {
        var respuesta = new List<RepuestoDto>();
        foreach (var repuesto in _repuestoDao.ListarTodo())
        {
            respuesta.Add(RepuestoMapeador.ADto(repuesto));
        }

        return Ok(respuesta);
    }

    [HttpGet("{id:long}")]
    public ActionResult<RepuestoDto> ObtenerPorId(long id)
    {
        var repuesto = _repuestoDao.Seleccionar(id);
        if (repuesto is null)
        {
            return NotFound();
        }

        return Ok(RepuestoMapeador.ADto(repuesto));
    }

    [HttpPost]
    public ActionResult<RepuestoDto> Crear(RepuestoCrearDto solicitud)
    {
        try
        {
            var repuesto = _repuestoDao.Insertar(RepuestoMapeador.AEntidad(solicitud));
            var respuesta = RepuestoMapeador.ADto(repuesto);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = repuesto.IdRepuesto }, respuesta);
        }
        catch (DbUpdateException excepcion) when (excepcion.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { mensaje = "Ya existe un repuesto con ese código." });
        }
    }

    [HttpPut("{id:long}")]
    public ActionResult<RepuestoDto> Actualizar(long id, RepuestoActualizarDto solicitud)
    {
        try
        {
            var repuesto = _repuestoDao.Seleccionar(id);
            if (repuesto is null)
            {
                return NotFound();
            }

            RepuestoMapeador.AplicarCambios(repuesto, solicitud);
            _repuestoDao.Actualizar(repuesto);
            return Ok(RepuestoMapeador.ADto(repuesto));
        }
        catch (DbUpdateException excepcion) when (excepcion.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { mensaje = "Ya existe un repuesto con ese código." });
        }
    }

    [HttpPatch("{id:long}")]
    public ActionResult<RepuestoDto> Desactivar(long id, RepuestoEstadoDto solicitud)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { mensaje = "Este endpoint solo acepta el estado 'inactivo'." });
        }

        if (!_repuestoDao.Desactivar(id))
        {
            return NotFound();
        }

        var repuesto = _repuestoDao.Seleccionar(id)!;
        return Ok(RepuestoMapeador.ADto(repuesto));
    }
}
