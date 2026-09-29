using Api.DTOs.OrdenesTrabajo;
using Api.Mapeadores;
using Api.Servicios;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/ordenes-trabajo")]
public class OrdenesTrabajoController : ControllerBase
{
    private readonly OrdenTrabajoDAO _ordenTrabajoDao;
    private readonly ServicioSugerencias _servicioSugerencias;

    public OrdenesTrabajoController(OrdenTrabajoDAO ordenTrabajoDao,
        ServicioSugerencias servicioSugerencias)
    {
        _ordenTrabajoDao = ordenTrabajoDao;
        _servicioSugerencias = servicioSugerencias;
    }

    [HttpGet]
    public ActionResult<List<OrdenTrabajoDto>> ObtenerTodos()
    {
        var respuesta = new List<OrdenTrabajoDto>();
        foreach (var orden in _ordenTrabajoDao.ListarTodo())
        {
            respuesta.Add(OrdenTrabajoMapeador.ADto(orden));
        }

        return Ok(respuesta);
    }

    [HttpGet("{id:long}")]
    public ActionResult<OrdenTrabajoDto> ObtenerPorId(long id)
    {
        var orden = _ordenTrabajoDao.Seleccionar(id);
        if (orden is null)
        {
            return NotFound();
        }

        return Ok(OrdenTrabajoMapeador.ADto(orden));
    }

    [HttpPost]
    public ActionResult<OrdenTrabajoDto> Crear(OrdenTrabajoCrearDto solicitud)
    {
        try
        {
            ValidarFecha(solicitud.FechaPrevistaEjecucion);
            var orden = new OrdenTrabajo
            {
                IdCliente = solicitud.ClienteId,
                IdActivo = solicitud.ActivoId,
                IdUsuarioTecnico = solicitud.TecnicoId,
                Tipo = OrdenTrabajoMapeador.ATipoBooleano(solicitud.Tipo),
                Prioridad = OrdenTrabajoMapeador.APrioridadNumero(solicitud.Prioridad),
                Descripcion = solicitud.Descripcion.Trim(),
                Resultado = solicitud.Observaciones
            };

            var ordenCreada = _ordenTrabajoDao.Crear(orden, solicitud.FechaPrevistaEjecucion,
                solicitud.RepuestosIds, solicitud.Sugerencias);
            var respuesta = OrdenTrabajoMapeador.ADto(ordenCreada);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = ordenCreada.IdOrden }, respuesta);
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
    }

    [HttpPut("{id:long}")]
    public ActionResult<OrdenTrabajoDto> Actualizar(long id, OrdenTrabajoActualizarDto solicitud)
    {
        try
        {
            ValidarFecha(solicitud.FechaPrevistaEjecucion);
            var orden = _ordenTrabajoDao.Seleccionar(id);
            if (orden is null)
            {
                return NotFound();
            }

            orden.IdCliente = solicitud.ClienteId;
            orden.IdActivo = solicitud.ActivoId;
            orden.IdUsuarioTecnico = solicitud.TecnicoId;
            orden.Tipo = OrdenTrabajoMapeador.ATipoBooleano(solicitud.Tipo);
            orden.Prioridad = OrdenTrabajoMapeador.APrioridadNumero(solicitud.Prioridad);
            orden.Descripcion = solicitud.Descripcion.Trim();
            orden.Resultado = solicitud.Observaciones;

            var ordenActualizada = _ordenTrabajoDao.Actualizar(orden,
                solicitud.FechaPrevistaEjecucion, solicitud.RepuestosIds,
                solicitud.Sugerencias, solicitud.Estado);
            return Ok(OrdenTrabajoMapeador.ADto(ordenActualizada));
        }
        catch (InvalidOperationException excepcion) when (excepcion.Message == "ORDEN_CERRADA")
        {
            return Conflict(new { codigo = "ORDEN_CERRADA", mensaje = "La orden cerrada no se puede modificar." });
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
    }

    [HttpPost("sugerencia")]
    public async Task<IActionResult> SolicitarSugerencia(SolicitudSugerenciaDto solicitud,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Descripcion))
        {
            return BadRequest(new
            {
                codigo = "DESCRIPCION_OBLIGATORIA",
                mensaje = "La descripción de la tarea es obligatoria."
            });
        }

        try
        {
            var sugerencia = await _servicioSugerencias.BuscarAsync(
                solicitud.Descripcion, solicitud.Numero, cancellationToken);
            if (sugerencia is null)
            {
                return UnprocessableEntity(new
                {
                    codigo = "SIN_CONTEXTO_TECNICO",
                    mensaje = "No hay información suficiente en la documentación técnica para esa tarea."
                });
            }

            return Ok(new { sugerencia });
        }
        catch (Exception)
        {
            return StatusCode(502, new
            {
                codigo = "SUGERENCIA_FALLIDA",
                mensaje = "No se pudo obtener la sugerencia técnica."
            });
        }
    }

    private static void ValidarFecha(DateOnly fecha)
    {
        if (fecha == default)
        {
            throw new ArgumentException("La fecha prevista de ejecución es obligatoria.");
        }
    }
}
