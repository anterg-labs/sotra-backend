using System.Text.Json;
using Api.DTOs.Version;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/version")]
public class VersionController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<VersionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VersionDto>> ObtenerVersion(CancellationToken cancellationToken)
    {
        var rutaArchivoVersion = Path.Combine(AppContext.BaseDirectory, "version.json");
        var contenidoArchivoVersion = await System.IO.File.ReadAllTextAsync(
            rutaArchivoVersion,
            cancellationToken);
        var opcionesJson = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var version = JsonSerializer.Deserialize<VersionDto>(contenidoArchivoVersion, opcionesJson);

        if (version is null || string.IsNullOrWhiteSpace(version.Version))
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Ok(version);
    }
}
