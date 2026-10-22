using Api.DTOs.OrdenesTrabajo;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/ordenes-trabajo/{ordenTrabajoId:long}/evidencias")]
public class EvidenciasController : ControllerBase
{
    private const int DiezMegabytesEnBytes = 10 * 1024 * 1024;
    private const string FormatoDeFecha = "yyyy-MM-dd";

    private readonly OrdenTrabajoDAO _ordenTrabajoDao;
    private readonly EvidenciaOtDAO _evidenciaDao;
    private readonly UsuarioActual _usuarioActual;

    public EvidenciasController(OrdenTrabajoDAO ordenTrabajoDao, EvidenciaOtDAO evidenciaDao,
        UsuarioActual usuarioActual)
    {
        _ordenTrabajoDao = ordenTrabajoDao;
        _evidenciaDao = evidenciaDao;
        _usuarioActual = usuarioActual;
    }

    [Authorize(Policy = Politicas.OtConsultar)]
    [HttpGet]
    public ActionResult<List<EvidenciaDto>> ObtenerTodas(long ordenTrabajoId)
    {
        var orden = _ordenTrabajoDao.Seleccionar(ordenTrabajoId);
        if (orden is null)
        {
            return NotFound();
        }

        if (!User.PuedeVerOrden(orden, _usuarioActual))
        {
            return Forbid();
        }

        var respuesta = new List<EvidenciaDto>();
        foreach (var evidencia in _evidenciaDao.ListarDeOrden(ordenTrabajoId))
        {
            respuesta.Add(ADto(evidencia));
        }

        return Ok(respuesta);
    }

    [Authorize(Policy = Politicas.OtRegistrarAvance)]
    [HttpPost]
    [RequestSizeLimit(DiezMegabytesEnBytes + 1024 * 1024)]
    public async Task<ActionResult<EvidenciaDto>> Crear(long ordenTrabajoId, [FromForm] string tipo,
        [FromForm] string? observacion, [FromForm] DateOnly fechaRegistro, IFormFile? archivo,
        CancellationToken cancellationToken)
    {
        var orden = _ordenTrabajoDao.Seleccionar(ordenTrabajoId);
        if (orden is null)
        {
            return NotFound();
        }

        if (!User.PuedeVerOrden(orden, _usuarioActual))
        {
            return Forbid();
        }

        var estadoOrden = OrdenTrabajoMapeador.ADto(orden).Estado;
        if (estadoOrden == "cerrada" || estadoOrden == "cancelada")
        {
            return Conflict(new
            {
                codigo = "ORDEN_NO_MODIFICABLE",
                mensaje = "La orden cerrada o cancelada no admite nuevas evidencias."
            });
        }

        if (tipo != "foto" && tipo != "documento" && tipo != "observacion")
        {
            return BadRequest(new
            {
                codigo = "TIPO_EVIDENCIA_INVALIDO",
                mensaje = "El tipo de evidencia debe ser foto, documento u observación."
            });
        }

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        if (fechaRegistro == default || fechaRegistro > hoy.AddDays(1))
        {
            return BadRequest(new
            {
                codigo = "FECHA_REGISTRO_INVALIDA",
                mensaje = "Indicá una fecha de registro válida que no sea futura."
            });
        }

        var textoObservacion = "";
        if (observacion is not null)
        {
            textoObservacion = observacion.Trim();
        }

        if (tipo == "observacion" && textoObservacion == "")
        {
            return BadRequest(new
            {
                codigo = "OBSERVACION_OBLIGATORIA",
                mensaje = "Escribí la observación de la evidencia."
            });
        }

        if (tipo != "observacion" && (archivo is null || archivo.Length == 0))
        {
            return BadRequest(new
            {
                codigo = "ARCHIVO_OBLIGATORIO",
                mensaje = "Adjuntá el archivo de la evidencia."
            });
        }

        var evidencia = new EvidenciaOt
        {
            IdOrden = ordenTrabajoId,
            Tipo = tipo,
            Observacion = textoObservacion,
            FechaRegistro = fechaRegistro
        };

        if (archivo is not null && archivo.Length > 0)
        {
            if (archivo.Length > DiezMegabytesEnBytes)
            {
                return StatusCode(413, new
                {
                    codigo = "ARCHIVO_DEMASIADO_GRANDE",
                    mensaje = "El archivo supera 10 MB."
                });
            }

            await using var memoria = new MemoryStream();
            await archivo.CopyToAsync(memoria, cancellationToken);
            var contenido = memoria.ToArray();
            var tipoContenido = DetectarTipoContenido(contenido);

            var esFotoValida = tipo == "foto" && (tipoContenido == "image/jpeg" || tipoContenido == "image/png");
            var esDocumentoValido = tipo != "foto" && tipoContenido != "";
            if (!esFotoValida && !esDocumentoValido)
            {
                return BadRequest(new
                {
                    codigo = "ARCHIVO_NO_PERMITIDO",
                    mensaje = "Las fotos deben ser JPG o PNG y los documentos PDF, JPG o PNG."
                });
            }

            evidencia.NombreArchivo = Path.GetFileName(archivo.FileName);
            evidencia.TipoContenido = tipoContenido;
            evidencia.Archivo = contenido;
        }

        evidencia.IdUsuario = _usuarioActual.ObtenerIdUsuario();
        var evidenciaCreada = _evidenciaDao.Crear(evidencia);
        return CreatedAtAction(nameof(ObtenerTodas), new { ordenTrabajoId = ordenTrabajoId }, ADto(evidenciaCreada));
    }

    [Authorize(Policy = Politicas.OtConsultar)]
    [HttpGet("{evidenciaId:long}/archivo")]
    public IActionResult Descargar(long ordenTrabajoId, long evidenciaId)
    {
        var orden = _ordenTrabajoDao.Seleccionar(ordenTrabajoId);
        if (orden is not null && !User.PuedeVerOrden(orden, _usuarioActual))
        {
            return Forbid();
        }

        var evidencia = _evidenciaDao.Seleccionar(ordenTrabajoId, evidenciaId);
        if (evidencia is null || evidencia.Archivo is null)
        {
            return NotFound();
        }

        var tipoContenido = "application/octet-stream";
        if (evidencia.TipoContenido is not null)
        {
            tipoContenido = evidencia.TipoContenido;
        }

        return File(evidencia.Archivo, tipoContenido, evidencia.NombreArchivo);
    }

    private static EvidenciaDto ADto(EvidenciaOt evidencia)
    {
        var observacion = "";
        if (evidencia.Observacion is not null)
        {
            observacion = evidencia.Observacion;
        }

        return new EvidenciaDto
        {
            Id = evidencia.IdEvidencia,
            OrdenTrabajoId = evidencia.IdOrden,
            Tipo = evidencia.Tipo,
            Observacion = observacion,
            FechaRegistro = evidencia.FechaRegistro.ToString(FormatoDeFecha),
            CreadoEn = evidencia.CreadoEn,
            NombreArchivo = evidencia.NombreArchivo,
            TipoContenido = evidencia.TipoContenido,
            TieneArchivo = evidencia.NombreArchivo is not null
        };
    }

    private static string DetectarTipoContenido(byte[] contenido)
    {
        if (contenido.Length >= 3 && contenido[0] == 0xFF && contenido[1] == 0xD8 && contenido[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (contenido.Length >= 4 && contenido[0] == 0x89 && contenido[1] == 0x50 && contenido[2] == 0x4E
            && contenido[3] == 0x47)
        {
            return "image/png";
        }

        if (contenido.Length >= 5 && contenido[0] == '%' && contenido[1] == 'P' && contenido[2] == 'D'
            && contenido[3] == 'F' && contenido[4] == '-')
        {
            return "application/pdf";
        }

        return "";
    }
}
