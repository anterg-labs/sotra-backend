using Api.DTOs.OrdenesTrabajo;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/ordenes-trabajo/{ordenTrabajoId:long}/sugerencias")]
public class RecomendacionesController : ControllerBase
{
    private readonly OrdenTrabajoDAO _ordenTrabajoDao;
    private readonly ValidacionRecomendacionDAO _validacionDao;

    public RecomendacionesController(OrdenTrabajoDAO ordenTrabajoDao, ValidacionRecomendacionDAO validacionDao)
    {
        _ordenTrabajoDao = ordenTrabajoDao;
        _validacionDao = validacionDao;
    }

    [HttpGet]
    public ActionResult<List<RecomendacionDto>> ObtenerTodas(long ordenTrabajoId)
    {
        var orden = _ordenTrabajoDao.Seleccionar(ordenTrabajoId);
        if (orden is null)
        {
            return NotFound();
        }

        var recomendaciones = ListarRecomendacionesActivas(orden);
        return Ok(ArmarRespuesta(recomendaciones));
    }

    [HttpPost("{sugerenciaId:long}/validaciones")]
    public ActionResult<RecomendacionDto> Validar(long ordenTrabajoId, long sugerenciaId,
        SolicitudValidacionDto solicitud)
    {
        var orden = _ordenTrabajoDao.Seleccionar(ordenTrabajoId);
        if (orden is null)
        {
            return NotFound();
        }

        HistorialRecomendacionIa? recomendacion = null;
        foreach (var recomendacionActiva in ListarRecomendacionesActivas(orden))
        {
            if (recomendacionActiva.IdRecomendacionIa == sugerenciaId)
            {
                recomendacion = recomendacionActiva;
            }
        }

        if (recomendacion is null)
        {
            return NotFound();
        }

        var resultado = solicitud.Resultado;
        if (resultado != "aceptada" && resultado != "rechazada" && resultado != "observada")
        {
            return BadRequest(new
            {
                codigo = "RESULTADO_VALIDACION_INVALIDO",
                mensaje = "El resultado debe ser aceptada, rechazada u observada."
            });
        }

        var observacion = "";
        if (solicitud.Observacion is not null)
        {
            observacion = solicitud.Observacion.Trim();
        }

        if (resultado != "aceptada" && observacion == "")
        {
            return BadRequest(new
            {
                codigo = "OBSERVACION_OBLIGATORIA",
                mensaje = "Indicá el motivo del rechazo o la observación sobre la recomendación."
            });
        }

        _validacionDao.Crear(new ValidacionRecomendacionIa
        {
            IdRecomendacionIa = recomendacion.IdRecomendacionIa,
            Resultado = resultado,
            Observacion = observacion
        });

        var lista = new List<HistorialRecomendacionIa> { recomendacion };
        var respuesta = ArmarRespuesta(lista)[0];
        return CreatedAtAction(nameof(ObtenerTodas), new { ordenTrabajoId = ordenTrabajoId }, respuesta);
    }

    private static List<HistorialRecomendacionIa> ListarRecomendacionesActivas(OrdenTrabajo orden)
    {
        var recomendaciones = new List<HistorialRecomendacionIa>();
        foreach (var recomendacion in orden.HistorialRecomendacionesIa.OrderBy(item => item.IdRecomendacionIa))
        {
            if (recomendacion.Estado)
            {
                recomendaciones.Add(recomendacion);
            }
        }

        return recomendaciones;
    }

    private List<RecomendacionDto> ArmarRespuesta(List<HistorialRecomendacionIa> recomendaciones)
    {
        var ids = new List<long>();
        foreach (var recomendacion in recomendaciones)
        {
            ids.Add(recomendacion.IdRecomendacionIa);
        }

        var validaciones = _validacionDao.ListarDeRecomendaciones(ids);

        var respuesta = new List<RecomendacionDto>();
        foreach (var recomendacion in recomendaciones)
        {
            var validacionesDeLaRecomendacion = new List<ValidacionRecomendacionDto>();
            var estadoValidacion = "pendiente";
            foreach (var validacion in validaciones)
            {
                if (validacion.IdRecomendacionIa != recomendacion.IdRecomendacionIa)
                {
                    continue;
                }

                var observacion = "";
                if (validacion.Observacion is not null)
                {
                    observacion = validacion.Observacion;
                }

                validacionesDeLaRecomendacion.Add(new ValidacionRecomendacionDto
                {
                    Resultado = validacion.Resultado,
                    Observacion = observacion,
                    Fecha = validacion.Fecha
                });
                estadoValidacion = validacion.Resultado;
            }

            respuesta.Add(new RecomendacionDto
            {
                Id = recomendacion.IdRecomendacionIa,
                Texto = recomendacion.Recomendacion,
                EstadoValidacion = estadoValidacion,
                Validaciones = validacionesDeLaRecomendacion
            });
        }

        return respuesta;
    }
}
