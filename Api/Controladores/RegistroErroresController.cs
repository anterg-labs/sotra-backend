using Api.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controladores;

public sealed record FalloSesionDto(string? Codigo, int? EstadoHttp);

/// <summary>Recepción limitada de códigos de fallo de sesión del servidor de Next.</summary>
[ApiController]
[Route("api/v1/registro-errores/sesion")]
public sealed class RegistroErroresController(RegistroDeErrores registro) : ControllerBase
{
    private static readonly HashSet<string> CodigosPermitidos =
    ["SESION_API_NO_DISPONIBLE", "SESION_API_ERROR", "SESION_RESPUESTA_INVALIDA", "AUTH_CALLBACK_ERROR", "TOKEN_RENOVACION_FALLIDA"];

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasDeLimite.Errores)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult Registrar([FromBody] FalloSesionDto datos)
    {
        if (datos.Codigo is null || !CodigosPermitidos.Contains(datos.Codigo) ||
            datos.EstadoHttp is < 100 or > 599)
            return this.RespuestaDeError(400, "REGISTRO_INVALIDO", "El código de error no es válido.");

        registro.Guardar(new ErrorRegistrable(DateTimeOffset.UtcNow, "frontend", "sesion", datos.Codigo,
            null, datos.EstadoHttp, "/api/v1/sesion",
            HttpContext.Items[CorrelacionMiddleware.Cabecera]?.ToString()));
        return NoContent();
    }
}
