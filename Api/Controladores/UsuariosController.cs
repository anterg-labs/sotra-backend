using Api.DTOs;
using Api.DTOs.Usuarios;
using Api.Mapeadores;
using Api.Seguridad;
using Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public UsuariosController(UsuarioDAO usuarioDao, ServicioKeycloakUsuarios keycloak,
        ILogger<UsuariosController> registro)
    {
        _usuarioDao = usuarioDao;
        _keycloak = keycloak;
        _registro = registro;
    }

    [Authorize(Policy = Politicas.UsuarioListar)]
    [HttpGet]
    [ProducesResponseType<PaginaDto<UsuarioListadoDto>>(StatusCodes.Status200OK)]
    public ActionResult<PaginaDto<UsuarioListadoDto>> ObtenerTodos([FromQuery] FiltrosUsuariosDto filtros)
    {
        if (!PaginaDto<UsuarioListadoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<UsuarioListadoDto>.ErrorDePaginacion());
        }

        var textoBuscado = "";
        if (!string.IsNullOrWhiteSpace(filtros.Buscar))
        {
            textoBuscado = filtros.Buscar.Trim().ToLower();
        }

        var respuesta = new List<UsuarioListadoDto>();
        foreach (var usuario in _usuarioDao.ListarTodo())
        {
            var usuarioDto = UsuarioMapeador.AListadoDto(usuario);

            if (textoBuscado != "")
            {
                var nombreCompleto = usuarioDto.Nombre + " " + usuarioDto.Apellido;
                var coincide = PaginaDto<UsuarioListadoDto>.Contiene(nombreCompleto, textoBuscado)
                               || PaginaDto<UsuarioListadoDto>.Contiene(usuarioDto.Correo, textoBuscado);
                if (!coincide)
                {
                    continue;
                }
            }

            if (filtros.RolId is not null)
            {
                var tieneElRol = false;
                foreach (var rol in usuarioDto.Roles)
                {
                    if (rol.Id == filtros.RolId)
                    {
                        tieneElRol = true;
                    }
                }

                if (!tieneElRol)
                {
                    continue;
                }
            }

            if (!string.IsNullOrEmpty(filtros.Estado) && usuarioDto.Estado != filtros.Estado)
            {
                continue;
            }

            respuesta.Add(usuarioDto);
        }

        if (filtros.DireccionOrden == "desc")
        {
            respuesta.Reverse();
        }

        return Ok(PaginaDto<UsuarioListadoDto>.Crear(respuesta, filtros.Pagina, filtros.TamanoPagina));
    }

    [Authorize(Policy = Politicas.UsuarioConsultar)]
    [HttpGet("{id:long}")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<UsuarioListadoDto> ObtenerPorId(long id)
    {
        var usuario = _usuarioDao.SeleccionarUsuario(id);
        return usuario is null ? NotFound() : Ok(UsuarioMapeador.AListadoDto(usuario));
    }

    // Crea el usuario en la base y en Keycloak. En Keycloak queda sin contraseña: le llega
    // un correo para elegirla. Si ya existía en Keycloak un usuario con ese correo, se
    // vincula ese y no se envía el correo. Si Keycloak falla, la base no guarda nada.
    [Authorize(Policy = Politicas.UsuarioCrear)]
    [HttpPost]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UsuarioListadoDto>> Crear(UsuarioCrearDto solicitud,
        CancellationToken cancellationToken)
    {
        Guid? idKeycloakCreado = null;
        try
        {
            using var transaccion = _usuarioDao.IniciarTransaccion();
            var usuario = UsuarioMapeador.AEntidad(solicitud);
            var usuarioCreado = _usuarioDao.InsertarUsuario(usuario, solicitud.IdsRol);

            var (idKeycloak, creado) = await _keycloak.CrearOObtenerAsync(DatosParaKeycloak(usuarioCreado),
                cancellationToken);
            if (creado)
            {
                idKeycloakCreado = idKeycloak;
            }

            await _keycloak.ReemplazarRolesAsync(idKeycloak, RolesParaKeycloak(usuarioCreado), cancellationToken);
            _usuarioDao.GuardarIdKeycloak(usuarioCreado.IdUsuario, idKeycloak);
            transaccion.Commit();

            if (creado)
            {
                await EnviarCorreoDeContrasenaSinErrorAsync(idKeycloak, cancellationToken);
            }

            var respuesta = UsuarioMapeador.AListadoDto(usuarioCreado);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = usuarioCreado.IdUsuario }, respuesta);
        }
        catch (ArgumentException excepcion)
        {
            await DeshacerAltaEnKeycloakAsync(idKeycloakCreado, cancellationToken);
            return BadRequest(new { mensaje = excepcion.Message });
        }
        catch (DbUpdateException)
        {
            await DeshacerAltaEnKeycloakAsync(idKeycloakCreado, cancellationToken);
            return Conflict(new { mensaje = "Ya existe un usuario con el correo o teléfono indicado." });
        }
        catch (ExcepcionKeycloak excepcion)
        {
            await DeshacerAltaEnKeycloakAsync(idKeycloakCreado, cancellationToken);
            return ErrorDeKeycloak(excepcion);
        }
    }

    // Actualiza la base y copia a Keycloak nombre, correo, roles y estado. Un usuario
    // desactivado no puede iniciar sesión y se cierran sus sesiones abiertas.
    [Authorize(Policy = Politicas.UsuarioEditar)]
    [HttpPut("{id:long}")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UsuarioListadoDto>> Actualizar(long id, UsuarioActualizarDto solicitud,
        CancellationToken cancellationToken)
    {
        try
        {
            using var transaccion = _usuarioDao.IniciarTransaccion();
            var usuario = _usuarioDao.SeleccionarUsuario(id);
            if (usuario is null) return NotFound();

            UsuarioMapeador.AplicarCambios(usuario, solicitud);
            _usuarioDao.ActualizarUsuario(usuario, solicitud.IdsRol);

            var usuarioActualizado = _usuarioDao.SeleccionarUsuario(id)!;
            await SincronizarConKeycloakAsync(usuarioActualizado, cancellationToken);
            transaccion.Commit();
            return Ok(UsuarioMapeador.AListadoDto(usuarioActualizado));
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "Ya existe un usuario con el correo o teléfono indicado." });
        }
        catch (ExcepcionKeycloak excepcion)
        {
            return ErrorDeKeycloak(excepcion);
        }
    }

    [Authorize(Policy = Politicas.UsuarioEditar)]
    [HttpPatch("{id:long}")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UsuarioListadoDto>> Desactivar(long id, UsuarioEstadoDto solicitud,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { mensaje = "Este endpoint solo acepta el estado 'inactivo'." });

        try
        {
            using var transaccion = _usuarioDao.IniciarTransaccion();
            if (!_usuarioDao.DarBajaLogicaUsuario(id)) return NotFound();

            var usuario = _usuarioDao.SeleccionarUsuario(id)!;
            await SincronizarConKeycloakAsync(usuario, cancellationToken);
            transaccion.Commit();
            return Ok(UsuarioMapeador.AListadoDto(usuario));
        }
        catch (ExcepcionKeycloak excepcion)
        {
            return ErrorDeKeycloak(excepcion);
        }
    }

    // El administrador le envía al usuario un correo para elegir una contraseña nueva.
    [Authorize(Policy = Politicas.UsuarioEditar)]
    [HttpPost("{id:long}/restablecer-contrasena")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> RestablecerContrasena(long id, CancellationToken cancellationToken)
    {
        var usuario = _usuarioDao.SeleccionarUsuario(id);
        if (usuario is null) return NotFound();

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
            await _keycloak.EnviarCorreoParaElegirContrasenaAsync(idKeycloak, cancellationToken);
            return NoContent();
        }
        catch (ExcepcionKeycloak excepcion)
        {
            return ErrorDeKeycloak(excepcion);
        }
    }

    // Los usuarios cargados antes de la integración, sin id_keycloak, no tienen acceso
    // al sistema y solo se actualizan en la base.
    private async Task SincronizarConKeycloakAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        if (usuario.IdKeycloak is not Guid idKeycloak)
        {
            return;
        }

        // Primero los roles: es el paso que más puede fallar y, si falla, los datos del
        // usuario en Keycloak quedan sin tocar.
        await _keycloak.ReemplazarRolesAsync(idKeycloak, RolesParaKeycloak(usuario), cancellationToken);
        await _keycloak.ActualizarAsync(idKeycloak, DatosParaKeycloak(usuario), cancellationToken);
        if (!usuario.Estado)
        {
            await _keycloak.CerrarSesionesAsync(idKeycloak, cancellationToken);
        }
    }

    private async Task EnviarCorreoDeContrasenaSinErrorAsync(Guid idKeycloak, CancellationToken cancellationToken)
    {
        try
        {
            await _keycloak.EnviarCorreoParaElegirContrasenaAsync(idKeycloak, cancellationToken);
        }
        catch (ExcepcionKeycloak excepcion)
        {
            // El usuario ya quedó creado: el administrador puede reenviar el correo con
            // POST /usuarios/{id}/restablecer-contrasena.
            _registro.LogWarning(excepcion, "No se pudo enviar el correo para elegir la contraseña a {IdKeycloak}.",
                idKeycloak);
        }
    }

    private async Task DeshacerAltaEnKeycloakAsync(Guid? idKeycloakCreado, CancellationToken cancellationToken)
    {
        if (idKeycloakCreado is Guid idKeycloak)
        {
            await _keycloak.EliminarSinErrorAsync(idKeycloak, cancellationToken);
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
