using Api.Infraestructura;
using Microsoft.AspNetCore.RateLimiting;
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

    [HttpGet]
    [Authorize(Policy = Politicas.OtConsultar)]
    public async Task<ActionResult<List<EvidenciaDto>>> ObtenerTodas(long ordenTrabajoId, CancellationToken cancelacion)
    {
        var orden = await _ordenTrabajoDao.SeleccionarAsync(ordenTrabajoId, cancelacion);
        if (orden is null)
        {
            return NotFound();
        }

        if (!await User.PuedeVerOrdenAsync(orden, _usuarioActual, cancelacion))
        {
            return Forbid();
        }

        var respuesta = new List<EvidenciaDto>();
        foreach (var evidencia in await _evidenciaDao.ListarDeOrdenAsync(ordenTrabajoId, cancelacion))
        {
            respuesta.Add(ADto(evidencia));
        }

        return Ok(respuesta);
    }

    [HttpPost]
    [Authorize(Policy = Politicas.OtRegistrarAvance)]
    [EnableRateLimiting(PoliticasDeLimite.Subidas)]
    [RequestSizeLimit(DiezMegabytesEnBytes + 1024 * 1024)]
    public async Task<ActionResult<EvidenciaDto>> Crear(long ordenTrabajoId, [FromForm] string tipo,
        [FromForm] string? observacion, [FromForm] DateOnly fechaRegistro, IFormFile? archivo,
        CancellationToken cancellationToken)
    {
        var orden = await _ordenTrabajoDao.SeleccionarAsync(ordenTrabajoId, cancellationToken);
        if (orden is null)
        {
            return NotFound();
        }

        if (!await User.PuedeVerOrdenAsync(orden, _usuarioActual, cancellationToken))
        {
            return Forbid();
        }

        var reglas = await _ordenTrabajoDao.ObtenerReglasAsync(cancellationToken);
        if (reglas.EsFinal(OrdenTrabajoMapeador.ADto(orden, reglas).Estado))
        {
            return this.RespuestaDeError(409, "ORDEN_NO_MODIFICABLE", "La orden cerrada o cancelada no admite nuevas evidencias.");
        }

        if (tipo != "foto" && tipo != "documento" && tipo != "observacion")
        {
            return this.RespuestaDeError(400, "TIPO_EVIDENCIA_INVALIDO", "El tipo de evidencia debe ser foto, documento u observación.");
        }

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        if (fechaRegistro == default || fechaRegistro > hoy.AddDays(1))
        {
            return this.RespuestaDeError(400, "FECHA_REGISTRO_INVALIDA", "Indicá una fecha de registro válida que no sea futura.");
        }

        var textoObservacion = "";
        if (observacion is not null)
        {
            textoObservacion = observacion.Trim();
        }

        if (tipo == "observacion" && textoObservacion == "")
        {
            return this.RespuestaDeError(400, "OBSERVACION_OBLIGATORIA", "Escribí la observación de la evidencia.");
        }

        if (tipo != "observacion" && (archivo is null || archivo.Length == 0))
        {
            return this.RespuestaDeError(400, "ARCHIVO_OBLIGATORIO", "Adjuntá el archivo de la evidencia.");
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
                return this.RespuestaDeError(413, "ARCHIVO_DEMASIADO_GRANDE", "El archivo supera 10 MB.");
            }

            await using var memoria = new MemoryStream();
            await archivo.CopyToAsync(memoria, cancellationToken);
            var contenido = memoria.ToArray();
            var tipoContenido = DetectarTipoContenido(contenido);

            var esFotoValida = tipo == "foto" && (tipoContenido == "image/jpeg" || tipoContenido == "image/png");
            var esDocumentoValido = tipo != "foto" && tipoContenido != "";
            if (!esFotoValida && !esDocumentoValido)
            {
                return this.RespuestaDeError(400, "ARCHIVO_NO_PERMITIDO", "Las fotos deben ser JPG o PNG y los documentos PDF, JPG o PNG.");
            }

            evidencia.NombreArchivo = Path.GetFileName(archivo.FileName);
            evidencia.TipoContenido = tipoContenido;
            evidencia.Archivo = contenido;
        }

        evidencia.IdUsuario = await _usuarioActual.ObtenerIdUsuarioAsync(cancellationToken);
        var evidenciaCreada = await _evidenciaDao.CrearAsync(evidencia, cancellationToken);
        return CreatedAtAction(nameof(ObtenerTodas), new { ordenTrabajoId = ordenTrabajoId }, ADto(evidenciaCreada));
    }

    [HttpGet("{evidenciaId:long}/archivo")]
    [Authorize(Policy = Politicas.OtConsultar)]
    public async Task<IActionResult> Descargar(long ordenTrabajoId, long evidenciaId, CancellationToken cancelacion)
    {
        var orden = await _ordenTrabajoDao.SeleccionarAsync(ordenTrabajoId, cancelacion);
        if (orden is not null && !await User.PuedeVerOrdenAsync(orden, _usuarioActual, cancelacion))
        {
            return Forbid();
        }

        var evidencia = await _evidenciaDao.SeleccionarAsync(ordenTrabajoId, evidenciaId, cancelacion);
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
