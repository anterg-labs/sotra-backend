using System.Security.Claims;
using Api.DTOs.Sesion;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controladores;

// Devuelve los datos del usuario que inició sesión, tal como vienen en el token de Keycloak.
[ApiController]
[Authorize]
[Route("api/v1/sesion")]
public class SesionController : ControllerBase
{
    private static readonly HashSet<string> RolesDeSotra = new()
    {
        "administrador", "jefe_mantenimiento", "tecnico", "panolero"
    };

    private readonly UsuarioActual _usuarioActual;

    public SesionController(UsuarioActual usuarioActual)
    {
        _usuarioActual = usuarioActual;
    }

    [HttpGet]
    [ProducesResponseType<SesionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SesionDto>> Obtener(CancellationToken cancelacion)
    {
        var idKeycloak = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(idKeycloak))
        {
            return Unauthorized();
        }

        var roles = new List<string>();
        foreach (var reclamo in User.FindAll(ClaimTypes.Role))
        {
            if (RolesDeSotra.Contains(reclamo.Value))
            {
                roles.Add(reclamo.Value);
            }
        }

        return Ok(new SesionDto
        {
            IdKeycloak = idKeycloak,
            IdUsuario = await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion),
            Correo = User.FindFirstValue("email"),
            Nombre = User.FindFirstValue("name"),
            Roles = roles
        });
    }
}
