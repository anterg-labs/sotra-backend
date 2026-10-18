using Api.Infraestructura;
using Microsoft.AspNetCore.RateLimiting;
using Api.DTOs;
using Api.DTOs.DocumentosTecnicos;
using Api.Servicios;
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
    public async Task<ActionResult<PaginaDto<DocumentoTecnicoDto>>> ObtenerTodos(
        [FromQuery] FiltrosDocumentosTecnicosDto filtros, CancellationToken cancellationToken)
    {
        if (!PaginaDto<DocumentoTecnicoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<DocumentoTecnicoDto>.ErrorDePaginacion());
        }

        var (elementos, total) = await _servicio.ListarPaginaAsync(
            filtros.Buscar, filtros.ActivoId, filtros.Pagina, filtros.TamanoPagina, cancellationToken);
        return Ok(PaginaDto<DocumentoTecnicoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<DocumentoTecnicoDto>> ObtenerPorId(long id, CancellationToken cancellationToken)
    {
        var documento = await _servicio.ObtenerAsync(id, cancellationToken);
        return documento is null ? NotFound() : Ok(documento);
    }

    [HttpPost]
    [EnableRateLimiting(PoliticasDeLimite.Subidas)]
    [RequestSizeLimit(CincuentaMegabytesEnBytes)]
    public async Task<ActionResult<DocumentoTecnicoDto>> Crear(IFormFile archivo,
        [FromForm] bool usaSugerenciasIA, [FromForm] long? activoId,
        CancellationToken cancellationToken)
    {
        if (archivo.Length == 0)
        {
            return BadRequest(new { codigo = "ARCHIVO_NO_ES_PDF", mensaje = "El archivo está vacío." });
        }

        if (archivo.Length > CincuentaMegabytesEnBytes)
        {
            return StatusCode(413, new { codigo = "ARCHIVO_DEMASIADO_GRANDE", mensaje = "El archivo supera 50 MB." });
        }

        await using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, cancellationToken);
        var contenido = memoria.ToArray();
        if (!EsPdf(contenido))
        {
            return BadRequest(new { codigo = "ARCHIVO_NO_ES_PDF", mensaje = "El archivo no es un PDF válido." });
        }

        try
        {
            var documento = await _servicio.GuardarAsync(archivo.FileName, activoId,
                usaSugerenciasIA, contenido, cancellationToken);
            return CreatedAtAction(nameof(Descargar), new { id = documento.Id }, documento);
        }
        catch (InvalidDataException excepcion)
        {
            return UnprocessableEntity(new { codigo = excepcion.Message, mensaje = "No se pudo leer el texto del PDF." });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(excepcion, "El servicio de IA (Ollama) no respondió al procesar un documento técnico");
            return StatusCode(502, new
            {
                codigo = "SERVICIO_IA_NO_DISPONIBLE",
                mensaje = "El servicio de IA no está disponible en este momento. Intentá de nuevo más tarde."
            });
        }
        catch (Exception excepcion) when (TraduccionDeErroresDeNegocio.Traducir(excepcion) is null)
        {
            _logger.LogError(excepcion, "Falló el procesamiento de un documento técnico");
            return StatusCode(502, new
            {
                codigo = "PROCESAMIENTO_FALLIDO",
                mensaje = "No se pudo procesar el documento técnico."
            });
        }
    }

    [HttpGet("{id:long}/archivo")]
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
            return StatusCode(502, new
            {
                codigo = "BORRADO_FALLIDO",
                mensaje = "No se pudo eliminar el documento técnico."
            });
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
