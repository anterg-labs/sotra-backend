using Api.DTOs.Usuarios;
using Api.Mapeadores;
using Api.Seguridad;
using Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

// «Mi perfil» (RF001-04): cada persona consulta y modifica sus propios datos sin necesitar permisos de gestión de usuarios.
// Solo se pueden cambiar nombre, apellido y teléfono: el correo, los roles, el estado y la contraseña los gobierna Keycloak
// o la administración de usuarios.
[ApiController]
[Authorize]
[Route("api/v1/perfil")]
public class PerfilController : ControllerBase
{
    private readonly UsuarioDAO _usuarioDao;
    private readonly UsuarioActual _usuarioActual;
    private readonly ServicioKeycloakUsuarios _keycloak;
    private readonly ILogger<PerfilController> _registro;

    public PerfilController(UsuarioDAO usuarioDao, UsuarioActual usuarioActual, ServicioKeycloakUsuarios keycloak,
        ILogger<PerfilController> registro)
    {
        _usuarioDao = usuarioDao;
        _usuarioActual = usuarioActual;
        _keycloak = keycloak;
        _registro = registro;
    }

    /// <summary>Devuelve los datos de la persona con sesión (Mi perfil).</summary>
    [HttpGet]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioListadoDto>> Obtener(CancellationToken cancelacion)
    {
        var id = await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion);
        var usuario = id is null ? null : await _usuarioDao.SeleccionarUsuarioAsync(id.Value, cancelacion);
        if (usuario is null) return NotFound();
        return Ok(await CrearRespuestaAsync(usuario, cancelacion));
    }

    /// <summary>Modifica nombre, apellido y teléfono de la persona con sesión.</summary>
    [HttpPut]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UsuarioListadoDto>> Actualizar(PerfilActualizarDto solicitud, CancellationToken cancelacion)
    {
        var id = await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion);
        if (id is null) return NotFound();

        try
        {
            await using var transaccion = await _usuarioDao.IniciarTransaccionAsync(cancelacion);
            var usuario = await _usuarioDao.ActualizarDatosPersonalesAsync(
                id.Value, solicitud.Nombre, solicitud.Apellido, solicitud.Telefono, cancelacion);
            if (usuario is null) return NotFound();

            if (usuario.IdKeycloak is Guid idKeycloak)
            {
                await _keycloak.ActualizarAsync(
                    idKeycloak, new DatosUsuarioKeycloak(usuario.Mail, usuario.Nombre, usuario.Apellido, usuario.Estado), cancelacion);
            }

            await transaccion.CommitAsync(cancelacion);
            return Ok(await CrearRespuestaAsync(usuario, cancelacion));
        }
        catch (ExcepcionKeycloak excepcion)
        {
            _registro.LogError(excepcion, "Error al actualizar el perfil en Keycloak.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                codigo = "KEYCLOAK_NO_DISPONIBLE",
                mensaje = "No se pudo actualizar tu perfil. Intentá de nuevo más tarde."
            });
        }
    }

    private async Task<UsuarioListadoDto> CrearRespuestaAsync(SOTRA_BACKEND.Modelos.Usuario usuario, CancellationToken cancelacion)
    {
        var cantidades = await _usuarioDao.ContarOrdenesAsignadasAsync([usuario.IdUsuario], cancelacion);
        return UsuarioMapeador.AListadoDto(usuario, cantidades.GetValueOrDefault(usuario.IdUsuario));
    }
}
