using Api.Mapeadores;
using Api.DTOs.Usuarios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioDAO _usuarioDao;

    public UsuariosController(UsuarioDAO usuarioDao)
    {
        _usuarioDao = usuarioDao;
    }


    [HttpGet]
    [ProducesResponseType<List<UsuarioListadoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UsuarioListadoDto>>> ObtenerTodos(
        CancellationToken cancellationToken)
    {
        var respuesta = await _usuarioDao.ListarTodoAsync(
            UsuarioMapeador.Proyeccion, cancellationToken);
        return Ok(respuesta);
    }
        [HttpGet("{id:long}")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<UsuarioListadoDto> ObtenerPorId(long id)
    {
        var usuario = _usuarioDao.seleccionarUsuario(id);
        return usuario is null ? NotFound() : Ok(UsuarioMapeador.AListadoDto(usuario));
    }

    [HttpPost]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<UsuarioListadoDto> Crear(UsuarioCrearDto solicitud)
    {
        try
        {
            var usuario = UsuarioMapeador.AEntidad(solicitud);
            var usuarioCreado = _usuarioDao.insertarUsuario(usuario, solicitud.IdsRol);
            var respuesta = UsuarioMapeador.AListadoDto(usuarioCreado);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = usuarioCreado.IdUsuario }, respuesta);
        }
        catch (ArgumentException excepcion)
        {
            return BadRequest(new { mensaje = excepcion.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "Ya existe un usuario con el correo o teléfono indicado." });
        }
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<UsuarioListadoDto> Actualizar(long id, UsuarioActualizarDto solicitud)
    {
        try
        {
            var usuario = _usuarioDao.seleccionarUsuario(id);
            if (usuario is null) return NotFound();

            UsuarioMapeador.AplicarCambios(usuario, solicitud);

            var idsRol = solicitud.IdsRol is { Count: > 0 }
                ? solicitud.IdsRol
                : usuario.IdRols.Select(rol => rol.IdRol).ToList();
            var nuevaContrasena = string.IsNullOrWhiteSpace(solicitud.Contrasena)
                ? null
                : solicitud.Contrasena;

            _usuarioDao.actualizarUsuario(usuario, idsRol, nuevaContrasena);

            var usuarioActualizado = _usuarioDao.seleccionarUsuario(id)!;
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
    }

    [HttpPatch("{id:long}")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<UsuarioListadoDto> Desactivar(long id, UsuarioEstadoDto solicitud)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { mensaje = "Este endpoint solo acepta el estado 'inactivo'." });

        if (!_usuarioDao.darBajaLogicaUsuario(id)) return NotFound();

        var usuario = _usuarioDao.seleccionarUsuario(id)!;
        return Ok(UsuarioMapeador.AListadoDto(usuario));
    }
}
// prueba
