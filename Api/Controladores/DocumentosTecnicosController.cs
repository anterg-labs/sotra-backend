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

    public DocumentosTecnicosController(ServicioDocumentosTecnicos servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentoTecnicoDto>>> ObtenerTodos(
        CancellationToken cancellationToken)
    {
        return Ok(await _servicio.ListarAsync(cancellationToken));
    }

    [HttpPost]
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
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { codigo = "ACTIVO_NO_EXISTE", mensaje = excepcion.Message });
        }
        catch (InvalidDataException excepcion)
        {
            return UnprocessableEntity(new { codigo = excepcion.Message, mensaje = "No se pudo leer el texto del PDF." });
        }
        catch (Exception)
        {
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
        catch (Exception)
        {
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
