using Api.DTOs;
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
    public ActionResult<PaginaDto<OrdenTrabajoDto>> ObtenerTodos([FromQuery] FiltrosOrdenesTrabajoDto filtros)
    {
        if (!PaginaDto<OrdenTrabajoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(new
            {
                codigo = "PAGINACION_INVALIDA",
                mensaje = "La página debe ser 1 o mayor y el tamaño de página debe estar entre 1 y "
                          + PaginaDto<OrdenTrabajoDto>.TamanoPaginaMaximo + "."
            });
        }

        if (filtros.FechaDesde is not null && filtros.FechaHasta is not null
            && filtros.FechaDesde > filtros.FechaHasta)
        {
            return BadRequest(new
            {
                codigo = "RANGO_FECHAS_INVALIDO",
                mensaje = "La fecha desde no puede ser posterior a la fecha hasta."
            });
        }

        var ordenes = new List<OrdenTrabajoDto>();
        foreach (var orden in _ordenTrabajoDao.ListarTodo())
        {
            var ordenDto = OrdenTrabajoMapeador.ADto(orden);
            if (CumpleFiltros(ordenDto, filtros))
            {
                ordenes.Add(ordenDto);
            }
        }

        if (filtros.OrdenarPor == "creadoEn")
        {
            ordenes = ordenes.OrderBy(orden => orden.CreadoEn).ToList();
        }

        if (filtros.DireccionOrden == "desc")
        {
            ordenes.Reverse();
        }

        return Ok(PaginaDto<OrdenTrabajoDto>.Crear(ordenes, filtros.Pagina, filtros.TamanoPagina));
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
                solicitud.Sugerencias, solicitud.Estado, solicitud.MotivoEstado);
            return Ok(OrdenTrabajoMapeador.ADto(ordenActualizada));
        }
        catch (InvalidOperationException excepcion)
        {
            return TraducirErrorDeEstado(excepcion);
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
    }

    [HttpPatch("{id:long}/estado")]
    public ActionResult<OrdenTrabajoDto> CambiarEstado(long id, CambioEstadoOrdenTrabajoDto solicitud)
    {
        var orden = _ordenTrabajoDao.Seleccionar(id);
        if (orden is null)
        {
            return NotFound();
        }

        try
        {
            var motivo = solicitud.Motivo;
            if (motivo is not null)
            {
                motivo = motivo.Trim();
            }

            var ordenActualizada = _ordenTrabajoDao.CambiarEstado(orden, solicitud.Estado, motivo);
            return Ok(OrdenTrabajoMapeador.ADto(ordenActualizada));
        }
        catch (InvalidOperationException excepcion)
        {
            return TraducirErrorDeEstado(excepcion);
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
    }

    [HttpPatch("{id:long}/tecnico")]
    public ActionResult<OrdenTrabajoDto> AsignarTecnico(long id, AsignacionTecnicoDto solicitud)
    {
        var orden = _ordenTrabajoDao.Seleccionar(id);
        if (orden is null)
        {
            return NotFound();
        }

        try
        {
            var ordenActualizada = _ordenTrabajoDao.AsignarTecnico(orden, solicitud.TecnicoId);
            return Ok(OrdenTrabajoMapeador.ADto(ordenActualizada));
        }
        catch (InvalidOperationException excepcion)
        {
            return TraducirErrorDeEstado(excepcion);
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
    }

    private ActionResult TraducirErrorDeEstado(InvalidOperationException excepcion)
    {
        if (excepcion.Message == "ORDEN_CERRADA")
        {
            return Conflict(new { codigo = "ORDEN_CERRADA", mensaje = "La orden cerrada no se puede modificar." });
        }

        if (excepcion.Message == "ORDEN_NO_MODIFICABLE")
        {
            return Conflict(new
            {
                codigo = "ORDEN_NO_MODIFICABLE",
                mensaje = "La orden cerrada o cancelada no se puede modificar."
            });
        }

        if (excepcion.Message == "TRANSICION_INVALIDA")
        {
            return BadRequest(new
            {
                codigo = "TRANSICION_INVALIDA",
                mensaje = "La orden no puede pasar a ese estado desde su estado actual."
            });
        }

        if (excepcion.Message == "TECNICO_OBLIGATORIO")
        {
            return BadRequest(new
            {
                codigo = "TECNICO_OBLIGATORIO",
                mensaje = "Asigná un técnico antes de pasar la orden a ese estado."
            });
        }

        if (excepcion.Message == "MOTIVO_OBLIGATORIO")
        {
            return BadRequest(new
            {
                codigo = "MOTIVO_OBLIGATORIO",
                mensaje = "Indicá el motivo u observación del cambio de estado."
            });
        }

        throw excepcion;
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

    private static bool CumpleFiltros(OrdenTrabajoDto orden, FiltrosOrdenesTrabajoDto filtros)
    {
        if (!string.IsNullOrWhiteSpace(filtros.Buscar))
        {
            var textoBuscado = filtros.Buscar.Trim().ToLower();
            var nombreTecnico = "";
            if (orden.NombreTecnico is not null)
            {
                nombreTecnico = orden.NombreTecnico;
            }

            var coincide = orden.Id.ToString() == textoBuscado
                           || orden.Descripcion.ToLower().Contains(textoBuscado)
                           || orden.NombreCliente.ToLower().Contains(textoBuscado)
                           || orden.NombreActivo.ToLower().Contains(textoBuscado)
                           || nombreTecnico.ToLower().Contains(textoBuscado);
            if (!coincide)
            {
                return false;
            }
        }

        if (filtros.ClienteId is not null && orden.ClienteId != filtros.ClienteId)
        {
            return false;
        }

        if (filtros.ActivoId is not null && orden.ActivoId != filtros.ActivoId)
        {
            return false;
        }

        if (filtros.TecnicoId is not null && orden.TecnicoId != filtros.TecnicoId)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filtros.Estado) && orden.Estado != filtros.Estado)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filtros.Prioridad) && orden.Prioridad != filtros.Prioridad)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filtros.Tipo) && orden.Tipo != filtros.Tipo)
        {
            return false;
        }

        var fechaCreacion = DateOnly.FromDateTime(orden.CreadoEn);
        if (filtros.FechaDesde is not null && fechaCreacion < filtros.FechaDesde)
        {
            return false;
        }

        if (filtros.FechaHasta is not null && fechaCreacion > filtros.FechaHasta)
        {
            return false;
        }

        return true;
    }

    private static void ValidarFecha(DateOnly fecha)
    {
        if (fecha == default)
        {
            throw new ArgumentException("La fecha prevista de ejecución es obligatoria.");
        }
    }
}
