using Api.Infraestructura;
using Microsoft.AspNetCore.RateLimiting;
using Api.DTOs;
using Api.DTOs.DocumentosTecnicos;
using Api.Seguridad;
using Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/documentos-tecnicos")]
public class DocumentosTecnicosController : ControllerBase
{
    private const int CincuentaMegabytesEnBytes = 50 * 1024 * 1024;
    private readonly ServicioDocumentosTecnicos _servicio;
    private readonly ILogger<DocumentosTecnicosController> _logger;

    public DocumentosTecnicosController(ServicioDocumentosTecnicos servicio,
        ILogger<DocumentosTecnicosController> logger)
    {
        _servicio = servicio;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = Politicas.DocumentoConsultar)]
    public async Task<ActionResult<PaginaDto<DocumentoTecnicoDto>>> ObtenerTodos(
        [FromQuery] FiltrosDocumentosTecnicosDto filtros, CancellationToken cancellationToken)
    {
        if (!PaginaDto<DocumentoTecnicoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<DocumentoTecnicoDto>.ErrorDePaginacion());
        }

        if (filtros.Tipo is not null && !TiposDeDocumento.EsValido(filtros.Tipo))
        {
            return this.RespuestaDeError(400, "TIPO_DOCUMENTO_INVALIDO", "El tipo de documento no es válido.");
        }

        if (filtros.OrdenarPor is { Length: > 0 } && !ServicioDocumentosTecnicos.ColumnasOrdenables.ContainsKey(filtros.OrdenarPor))
        {
            return this.OrdenInvalido(ServicioDocumentosTecnicos.ColumnasOrdenables.Keys);
        }

        var (elementos, total) = await _servicio.ListarPaginaAsync(
            filtros.Buscar, filtros.ActivoId, filtros.Tipo, filtros.Vigente, filtros.ModeloMaquinaId,
            filtros.Pagina, filtros.TamanoPagina, cancellationToken, filtros.OrdenarPor, filtros.DireccionOrden == "desc");
        return Ok(PaginaDto<DocumentoTecnicoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = Politicas.DocumentoConsultar)]
    public async Task<ActionResult<DocumentoTecnicoDto>> ObtenerPorId(long id, CancellationToken cancellationToken)
    {
        var documento = await _servicio.ObtenerAsync(id, cancellationToken);
        return documento is null ? NotFound() : Ok(documento);
    }

    [HttpPost]
    [Authorize(Policy = Politicas.DocumentoGestionar)]
    [EnableRateLimiting(PoliticasDeLimite.Subidas)]
    [RequestSizeLimit(CincuentaMegabytesEnBytes)]
    public async Task<ActionResult<DocumentoTecnicoDto>> Crear(IFormFile archivo,
        [FromForm] bool usaSugerenciasIA, [FromForm] long? activoId,
        [FromForm] string? titulo, [FromForm] string? tipo, [FromForm] string? descripcion,
        [FromForm] long? modeloMaquinaId, CancellationToken cancellationToken)
    {
        if (archivo.Length == 0)
        {
            return this.RespuestaDeError(400, "ARCHIVO_NO_ES_PDF", "El archivo está vacío.");
        }

        if (archivo.Length > CincuentaMegabytesEnBytes)
        {
            return this.RespuestaDeError(413, "ARCHIVO_DEMASIADO_GRANDE", "El archivo supera 50 MB.");
        }

        await using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, cancellationToken);
        var contenido = memoria.ToArray();
        if (!EsPdf(contenido))
        {
            return this.RespuestaDeError(400, "ARCHIVO_NO_ES_PDF", "El archivo no es un PDF válido.");
        }

        try
        {
            var datos = new DatosDelDocumento(titulo, tipo ?? TiposDeDocumento.ManualTecnico,
                descripcion, activoId, modeloMaquinaId);
            var documento = await _servicio.GuardarAsync(archivo.FileName, datos,
                usaSugerenciasIA, contenido, cancellationToken);
            return CreatedAtAction(nameof(Descargar), new { id = documento.Id }, documento);
        }
        catch (InvalidDataException excepcion)
        {
            return this.RespuestaDeError(422, excepcion.Message, "No se pudo leer el texto del PDF.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (ModeloLocalNoDisponibleException excepcion)
        {
            _logger.LogError(excepcion, "Faltan los modelos de IA locales al procesar un documento técnico");
            return this.RespuestaDeError(502, "SERVICIO_IA_NO_DISPONIBLE", "El servicio de IA no está disponible en este momento. Intentá de nuevo más tarde.");
        }
        catch (Exception excepcion) when (TraduccionDeErroresDeNegocio.Traducir(excepcion) is null)
        {
            _logger.LogError(excepcion, "Falló el procesamiento de un documento técnico");
            return this.RespuestaDeError(502, "PROCESAMIENTO_FALLIDO", "No se pudo procesar el documento técnico.");
        }
    }

    /// <summary>Vuelve a procesar el PDF guardado con la versión actual, por ejemplo después de mejorar la extracción.</summary>
    [HttpPost("{id:long}/reprocesar")]
    [Authorize(Policy = Politicas.DocumentoGestionar)]
    [EnableRateLimiting(PoliticasDeLimite.Subidas)]
    public async Task<ActionResult<DocumentoTecnicoDto>> Reprocesar(long id, CancellationToken cancellationToken)
    {
        try
        {
            var documento = await _servicio.ReprocesarAsync(id, cancellationToken);
            return documento is null ? NotFound() : Ok(documento);
        }
        catch (InvalidDataException excepcion)
        {
            return this.RespuestaDeError(422, excepcion.Message, "No se pudo leer el texto del PDF.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (ModeloLocalNoDisponibleException excepcion)
        {
            _logger.LogError(excepcion, "Faltan los modelos de IA locales al reprocesar un documento técnico");
            return this.RespuestaDeError(502, "SERVICIO_IA_NO_DISPONIBLE", "El servicio de IA no está disponible en este momento. Intentá de nuevo más tarde.");
        }
        catch (Exception excepcion) when (TraduccionDeErroresDeNegocio.Traducir(excepcion) is null)
        {
            _logger.LogError(excepcion, "Falló el reprocesamiento del documento técnico {DocumentoId}", id);
            return this.RespuestaDeError(502, "PROCESAMIENTO_FALLIDO", "No se pudo procesar el documento técnico.");
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<DocumentoTecnicoDto>> Actualizar(long id,
        DocumentoTecnicoActualizarDto solicitud, CancellationToken cancellationToken)
    {
        var datos = new DatosDelDocumento(solicitud.Titulo, solicitud.Tipo, solicitud.Descripcion,
            solicitud.ActivoId, solicitud.ModeloMaquinaId);
        var documento = await _servicio.ActualizarAsync(id, datos, cancellationToken);
        return documento is null ? NotFound() : Ok(documento);
    }

    [HttpPatch("{id:long}/vigencia")]
    public async Task<ActionResult<DocumentoTecnicoDto>> CambiarVigencia(long id,
        DocumentoTecnicoVigenciaDto solicitud, CancellationToken cancellationToken)
    {
        var documento = await _servicio.CambiarVigenciaAsync(id, solicitud.Vigente, cancellationToken);
        return documento is null ? NotFound() : Ok(documento);
    }

    [HttpGet("{id:long}/archivo")]
    [Authorize(Policy = Politicas.DocumentoConsultar)]
    public async Task<IActionResult> Descargar(long id, CancellationToken cancellationToken)
    {
        var documento = await _servicio.ObtenerArchivoAsync(id, cancellationToken);
        if (documento is null)
        {
            return NotFound();
        }

        return File(documento.Contenido, "application/pdf", documento.NombreArchivo);
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = Politicas.DocumentoGestionar)]
    public async Task<IActionResult> Eliminar(long id, CancellationToken cancellationToken)
    {
        try
        {
            if (!await _servicio.EliminarAsync(id, cancellationToken))
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception excepcion) when (TraduccionDeErroresDeNegocio.Traducir(excepcion) is null)
        {
            _logger.LogError(excepcion, "Falló el borrado del documento técnico {DocumentoId}", id);
            return this.RespuestaDeError(502, "BORRADO_FALLIDO", "No se pudo eliminar el documento técnico.");
        }
    }

    private static bool EsPdf(byte[] contenido)
    {
        if (contenido.Length < 5)
        {
            return false;
        }

        var empiezaComoPdf = contenido[0] == '%' && contenido[1] == 'P' && contenido[2] == 'D' &&
            contenido[3] == 'F' && contenido[4] == '-';
        return empiezaComoPdf;
    }
}
