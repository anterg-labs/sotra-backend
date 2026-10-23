using SOTRA_BACKEND.Operaciones;

namespace Api.Seguridad;

// La persona que hace la solicitud, según el token de Keycloak. Sin token
// (AUTH_OBLIGATORIA=false) no hay nadie autenticado.
public interface IUsuarioActual
{
    bool EstaAutenticado { get; }

    string? Correo { get; }
}

// Traduce el token del pedido al usuario de SOTRA (operativo.usuario). La primera vez
// que un usuario de Keycloak entra, se vincula por correo con su usuario de SOTRA.
public class UsuarioActual : IUsuarioActual
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

    public bool EstaAutenticado => _accesoAlContexto.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string? Correo => EstaAutenticado
        ? _accesoAlContexto.HttpContext!.User.FindFirst("email")?.Value
        : null;

    // Devuelve null si el pedido no trae token (por ejemplo, con AUTH_OBLIGATORIA=false)
    // o si el usuario de Keycloak no está registrado en SOTRA.
    public async Task<long?> ObtenerIdUsuarioAsync(CancellationToken cancelacion)
    {
        if (_resuelto)
        {
            return _idUsuario;
        }

        var usuario = _accesoAlContexto.HttpContext?.User;
        if (usuario?.Identity?.IsAuthenticated != true)
        {
            _resuelto = true;
            return null;
        }

        if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var idKeycloak))
        {
            _resuelto = true;
            return null;
        }

        var correo = usuario.FindFirst("email")?.Value;
        _idUsuario = (await _usuarioDao.SeleccionarOVincularPorKeycloakAsync(idKeycloak, correo, cancelacion))?.IdUsuario;
        _resuelto = true;
        return _idUsuario;
    }
}
