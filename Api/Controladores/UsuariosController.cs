using Api.DTOs;
using Api.DTOs.Usuarios;
using Api.Infraestructura;
using Api.Mapeadores;
using Api.Seguridad;
using Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioDAO _usuarioDao;
    private readonly ServicioKeycloakUsuarios _keycloak;
    private readonly ILogger<UsuariosController> _registro;
    private readonly RolDAO _rolDao;

    private const string NombreRolAdministrador = "Administrador";

    public UsuariosController(UsuarioDAO usuarioDao, ServicioKeycloakUsuarios keycloak,
        ILogger<UsuariosController> registro, RolDAO rolDao)
    {
        _usuarioDao = usuarioDao;
        _keycloak = keycloak;
        _registro = registro;
        _rolDao = rolDao;
    }

    // El jefe de mantenimiento crea y modifica usuarios, pero no administradores (ver AlcancePorRol).
    private ObjectResult SoloAdministrador()
    {
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            codigo = "SOLO_ADMINISTRADOR",
            mensaje = "Solo un administrador puede asignar el rol Administrador o modificar a un administrador."
        });
    }

    private async Task<bool> IncluyeRolAdministradorAsync(IEnumerable<long> idsRol, CancellationToken cancelacion)
    {
        var idAdministrador = (await _rolDao.ListarTodoAsync(cancelacion))
            .FirstOrDefault(rol => string.Equals(rol.Nombre, NombreRolAdministrador, StringComparison.OrdinalIgnoreCase))
            ?.IdRol;
        return idAdministrador is not null && idsRol.Contains(idAdministrador.Value);
    }

    private static bool EsAdministrador(Usuario usuario)
    {
        return usuario.IdRols.Any(rol => string.Equals(rol.Nombre, NombreRolAdministrador, StringComparison.OrdinalIgnoreCase));
    }

    [HttpGet]
    [Authorize(Policy = Politicas.UsuarioListar)]
    [ProducesResponseType<PaginaDto<UsuarioListadoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<UsuarioListadoDto>>> ObtenerTodos([FromQuery] FiltrosUsuariosDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<UsuarioListadoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<UsuarioListadoDto>.ErrorDePaginacion());
        }

        if (!UsuarioDAO.ColumnasOrdenables.Contiene(filtros.OrdenarPor))
        {
            return this.OrdenInvalido(UsuarioDAO.ColumnasOrdenables.Claves);
        }

        if (!ConsultasDeTexto.TryTraducirEstado(filtros.Estado, out var estado))
        {
            return Ok(PaginaDto<UsuarioListadoDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var filtro = new FiltroUsuarios(filtros.Buscar, filtros.RolId, estado, filtros.DireccionOrden == "desc", filtros.OrdenarPor);
        var (usuarios, total) = await _usuarioDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var cantidades = await _usuarioDao.ContarOrdenesAsignadasAsync(usuarios.Select(u => u.IdUsuario), cancelacion);
        var elementos = usuarios.Select(u => UsuarioMapeador.AListadoDto(u, cantidades.GetValueOrDefault(u.IdUsuario))).ToList();
        return Ok(PaginaDto<UsuarioListadoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = Politicas.UsuarioConsultar)]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioListadoDto>> ObtenerPorId(long id, CancellationToken cancelacion)
    {
        var usuario = await _usuarioDao.SeleccionarUsuarioAsync(id, cancelacion);
        return usuario is null ? NotFound() : Ok(await CrearRespuestaAsync(usuario, cancelacion));
    }

    // Crea el usuario en la base y en Keycloak. En Keycloak queda sin contraseña: le llega
    // un correo para elegirla. Si ya existía en Keycloak un usuario con ese correo, se
    // vincula ese y no se envía el correo. Si Keycloak falla, la base no guarda nada.
    [HttpPost]
    [Authorize(Policy = Politicas.UsuarioCrear)]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UsuarioListadoDto>> Crear(UsuarioCrearDto solicitud, CancellationToken cancelacion)
    {
        if (!User.PuedeGestionarAdministradores() && await IncluyeRolAdministradorAsync(solicitud.IdsRol, cancelacion))
        {
            return SoloAdministrador();
        }

        Guid? idKeycloakCreado = null;
        try
        {
            await using var transaccion = await _usuarioDao.IniciarTransaccionAsync(cancelacion);
            var usuario = UsuarioMapeador.AEntidad(solicitud);
            var usuarioCreado = await _usuarioDao.InsertarUsuarioAsync(usuario, solicitud.IdsRol, cancelacion);

            var (idKeycloak, creado) = await _keycloak.CrearOObtenerAsync(DatosParaKeycloak(usuarioCreado), cancelacion);
            if (creado)
            {
                idKeycloakCreado = idKeycloak;
            }

            await _keycloak.ReemplazarRolesAsync(idKeycloak, RolesParaKeycloak(usuarioCreado), cancelacion);
            await _usuarioDao.GuardarIdKeycloakAsync(usuarioCreado.IdUsuario, idKeycloak, cancelacion);
            await transaccion.CommitAsync(cancelacion);

            bool? correoEnviado = null;
            if (creado)
            {
                correoEnviado = await EnviarCorreoDeContrasenaAsync(idKeycloak, cancelacion);
            }

            var respuesta = UsuarioMapeador.AListadoDto(usuarioCreado, 0, correoEnviado);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = usuarioCreado.IdUsuario }, respuesta);
        }
        catch (ExcepcionKeycloak excepcion)
        {
            await DeshacerAltaEnKeycloakAsync(idKeycloakCreado);
            return ErrorDeKeycloak(excepcion);
        }
        catch
        {
            // Las validaciones y los duplicados los traduce el manejador global de errores;
            // antes se borra de Keycloak el usuario recién creado para no dejarlo huérfano.
            await DeshacerAltaEnKeycloakAsync(idKeycloakCreado);
            throw;
        }
    }

    // Actualiza la base y copia a Keycloak nombre, correo, roles y estado. Un usuario
    // desactivado no puede iniciar sesión y se cierran sus sesiones abiertas.
    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.UsuarioEditar)]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UsuarioListadoDto>> Actualizar(long id, UsuarioActualizarDto solicitud, CancellationToken cancelacion)
    {
        try
        {
            await using var transaccion = await _usuarioDao.IniciarTransaccionAsync(cancelacion);
            var usuario = await _usuarioDao.SeleccionarUsuarioAsync(id, cancelacion);
            if (usuario is null) return NotFound();
            if (!User.PuedeGestionarAdministradores()
                && (EsAdministrador(usuario) || await IncluyeRolAdministradorAsync(solicitud.IdsRol, cancelacion)))
            {
                return SoloAdministrador();
            }

            UsuarioMapeador.AplicarCambios(usuario, solicitud);
            await _usuarioDao.ActualizarUsuarioAsync(usuario, solicitud.IdsRol, cancelacion);

            var usuarioActualizado = (await _usuarioDao.SeleccionarUsuarioAsync(id, cancelacion))!;
            await SincronizarConKeycloakAsync(usuarioActualizado, cancelacion);
            await transaccion.CommitAsync(cancelacion);
            return Ok(await CrearRespuestaAsync(usuarioActualizado, cancelacion));
        }
        catch (ExcepcionKeycloak excepcion)
        {
            return ErrorDeKeycloak(excepcion);
        }
    }

    [HttpPatch("{id:long}")]
    [Authorize(Policy = Politicas.UsuarioEditar)]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UsuarioListadoDto>> CambiarEstado(long id, UsuarioEstadoDto solicitud, CancellationToken cancelacion)
    {
        var baja = string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase);
        if (!baja && !string.Equals(solicitud.Estado, "activo", StringComparison.OrdinalIgnoreCase))
            return this.RespuestaDeError(400, "ESTADO_INVALIDO", "El estado debe ser 'activo' o 'inactivo'.");

        var usuarioACambiar = await _usuarioDao.SeleccionarUsuarioAsync(id, cancelacion);
        if (usuarioACambiar is null) return NotFound();
        if (!User.PuedeGestionarAdministradores() && EsAdministrador(usuarioACambiar)) return SoloAdministrador();

        try
        {
            await using var transaccion = await _usuarioDao.IniciarTransaccionAsync(cancelacion);
            var encontrado = baja
                ? await _usuarioDao.DarBajaLogicaUsuarioAsync(id, cancelacion)
                : await _usuarioDao.ReactivarUsuarioAsync(id, cancelacion);
            if (!encontrado) return NotFound();

            var usuario = (await _usuarioDao.SeleccionarUsuarioAsync(id, cancelacion))!;
            await SincronizarConKeycloakAsync(usuario, cancelacion);
            await transaccion.CommitAsync(cancelacion);
            return Ok(await CrearRespuestaAsync(usuario, cancelacion));
        }
        catch (ExcepcionKeycloak excepcion)
        {
            return ErrorDeKeycloak(excepcion);
        }
    }

    private async Task<UsuarioListadoDto> CrearRespuestaAsync(Usuario usuario, CancellationToken cancelacion)
    {
        var cantidades = await _usuarioDao.ContarOrdenesAsignadasAsync([usuario.IdUsuario], cancelacion);
        return UsuarioMapeador.AListadoDto(usuario, cantidades.GetValueOrDefault(usuario.IdUsuario));
    }

    // El administrador (o el jefe, si el usuario no es administrador) le envía al usuario un correo
    // para elegir una contraseña nueva.
    [HttpPost("{id:long}/restablecer-contrasena")]
    [Authorize(Policy = Politicas.UsuarioEditar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> RestablecerContrasena(long id, CancellationToken cancelacion)
    {
        var usuario = await _usuarioDao.SeleccionarUsuarioAsync(id, cancelacion);
        if (usuario is null) return NotFound();
        if (!User.PuedeGestionarAdministradores() && EsAdministrador(usuario)) return SoloAdministrador();

        if (usuario.IdKeycloak is not Guid idKeycloak)
        {
            return Conflict(new
            {
                codigo = "USUARIO_SIN_ACCESO",
                mensaje = "El usuario no tiene acceso al sistema: no está registrado en Keycloak."
            });
        }

        if (!usuario.Estado)
        {
            return Conflict(new
            {
                codigo = "USUARIO_INACTIVO",
                mensaje = "El usuario está dado de baja. Reactivalo antes de restablecer la contraseña."
            });
        }

        try
        {
            await _keycloak.EnviarCorreoParaElegirContrasenaAsync(idKeycloak, cancelacion);
            return NoContent();
        }
        catch (ExcepcionCorreoNoEnviado excepcion)
        {
            _registro.LogError(excepcion, "No se pudo enviar el correo para modificar la contraseña de {IdKeycloak}.", idKeycloak);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                codigo = "CORREO_NO_ENVIADO",
                mensaje = "No se pudo enviar el correo. Revisá la configuración del correo (SMTP) de Keycloak."
            });
        }
        catch (ExcepcionKeycloak excepcion)
        {
            return ErrorDeKeycloak(excepcion);
        }
    }

    // Los usuarios cargados antes de la integración, sin id_keycloak, no tienen acceso
    // al sistema y solo se actualizan en la base.
    private async Task SincronizarConKeycloakAsync(Usuario usuario, CancellationToken cancelacion)
    {
        if (usuario.IdKeycloak is not Guid idKeycloak)
        {
            return;
        }

        // Primero los roles: es el paso que más puede fallar y, si falla, los datos del
        // usuario en Keycloak quedan sin tocar.
        await _keycloak.ReemplazarRolesAsync(idKeycloak, RolesParaKeycloak(usuario), cancelacion);
        await _keycloak.ActualizarAsync(idKeycloak, DatosParaKeycloak(usuario), cancelacion);
        if (!usuario.Estado)
        {
            await _keycloak.CerrarSesionesAsync(idKeycloak, cancelacion);
        }
    }

    // Devuelve si el correo salió de verdad. Si no, el usuario ya quedó creado y la respuesta lo informa
    // (<c>correoDeContrasenaEnviado = false</c>): se puede reenviar con POST /usuarios/{id}/restablecer-contrasena.
    private async Task<bool> EnviarCorreoDeContrasenaAsync(Guid idKeycloak, CancellationToken cancelacion)
    {
        try
        {
            await _keycloak.EnviarCorreoParaElegirContrasenaAsync(idKeycloak, cancelacion);
            return true;
        }
        catch (ExcepcionKeycloak excepcion)
        {
            _registro.LogWarning(excepcion, "No se pudo enviar el correo para elegir la contraseña a {IdKeycloak}.",
                idKeycloak);
            return false;
        }
    }

    // Sin el token de la solicitud: aunque el cliente haya cancelado, hay que borrar el usuario creado.
    private async Task DeshacerAltaEnKeycloakAsync(Guid? idKeycloakCreado)
    {
        if (idKeycloakCreado is Guid idKeycloak)
        {
            await _keycloak.EliminarSinErrorAsync(idKeycloak, CancellationToken.None);
        }
    }

    private ObjectResult ErrorDeKeycloak(ExcepcionKeycloak excepcion)
    {
        _registro.LogError(excepcion, "Error al sincronizar el usuario con Keycloak.");
        return StatusCode(StatusCodes.Status502BadGateway, new
        {
            codigo = "KEYCLOAK_NO_DISPONIBLE",
            mensaje = "No se pudo actualizar el acceso del usuario. Intentá de nuevo más tarde."
        });
    }

    private static DatosUsuarioKeycloak DatosParaKeycloak(Usuario usuario)
    {
        return new DatosUsuarioKeycloak(usuario.Mail, usuario.Nombre, usuario.Apellido, usuario.Estado);
    }

    private static List<string> RolesParaKeycloak(Usuario usuario)
    {
        return Roles.DesdeRolesDeSotra(usuario.IdRols.Where(rol => rol.Activo).Select(rol => rol.Nombre));
    }
}
