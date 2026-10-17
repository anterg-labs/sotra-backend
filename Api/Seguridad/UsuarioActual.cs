using SOTRA_BACKEND.Operaciones;

namespace Api.Seguridad;

// Traduce el token del pedido al usuario de SOTRA (operativo.usuario). La primera vez
// que un usuario de Keycloak entra, se vincula por correo con su usuario de SOTRA.
public class UsuarioActual
{
    private readonly IHttpContextAccessor _accesoAlContexto;
    private readonly UsuarioDAO _usuarioDao;
    private bool _resuelto;
    private long? _idUsuario;

    public UsuarioActual(IHttpContextAccessor accesoAlContexto, UsuarioDAO usuarioDao)
    {
        _accesoAlContexto = accesoAlContexto;
        _usuarioDao = usuarioDao;
    }

    // Devuelve null si el pedido no trae token (por ejemplo, con AUTH_OBLIGATORIA=false)
    // o si el usuario de Keycloak no está registrado en SOTRA.
    public long? ObtenerIdUsuario()
    {
        if (_resuelto)
        {
            return _idUsuario;
        }

        _resuelto = true;
        var usuario = _accesoAlContexto.HttpContext?.User;
        if (usuario?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var idKeycloak))
        {
            return null;
        }

        var correo = usuario.FindFirst("email")?.Value;
        _idUsuario = _usuarioDao.SeleccionarOVincularPorKeycloak(idKeycloak, correo)?.IdUsuario;
        return _idUsuario;
    }
}
