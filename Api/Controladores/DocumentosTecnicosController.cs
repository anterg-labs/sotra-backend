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

    public DocumentosTecnicosController(ServicioDocumentosTecnicos servicio)
    {
        _servicio = servicio;
    }

    [Authorize(Policy = Politicas.DocumentoConsultar)]
    [HttpGet]
    public async Task<ActionResult<PaginaDto<DocumentoTecnicoDto>>> ObtenerTodos(
        [FromQuery] FiltrosDocumentosTecnicosDto filtros, CancellationToken cancellationToken)
    {
        if (!PaginaDto<DocumentoTecnicoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<DocumentoTecnicoDto>.ErrorDePaginacion());
        }

        var textoBuscado = "";
        if (!string.IsNullOrWhiteSpace(filtros.Buscar))
        {
            textoBuscado = filtros.Buscar.Trim().ToLower();
        }

        var respuesta = new List<DocumentoTecnicoDto>();
        foreach (var documento in await _servicio.ListarAsync(cancellationToken))
        {
            if (textoBuscado != "")
            {
                var coincide = PaginaDto<DocumentoTecnicoDto>.Contiene(documento.NombreArchivo, textoBuscado)
                               || PaginaDto<DocumentoTecnicoDto>.Contiene(documento.NombreActivo, textoBuscado);
                if (!coincide)
                {
                    continue;
                }
            }

            if (filtros.ActivoId is not null && documento.ActivoId != filtros.ActivoId)
            {
                continue;
            }

            respuesta.Add(documento);
        }

        return Ok(PaginaDto<DocumentoTecnicoDto>.Crear(respuesta, filtros.Pagina, filtros.TamanoPagina));
    }

    [Authorize(Policy = Politicas.DocumentoConsultar)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<DocumentoTecnicoDto>> ObtenerPorId(long id, CancellationToken cancellationToken)
    {
        foreach (var documento in await _servicio.ListarAsync(cancellationToken))
        {
            if (documento.Id == id)
            {
                return Ok(documento);
            }
        }

        return NotFound();
    }

    [Authorize(Policy = Politicas.DocumentoGestionar)]
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

    [Authorize(Policy = Politicas.DocumentoConsultar)]
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

    [Authorize(Policy = Politicas.DocumentoGestionar)]
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
