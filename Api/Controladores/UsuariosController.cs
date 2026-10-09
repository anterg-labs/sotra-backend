using Api.DTOs;
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

    [HttpGet("{id:long}")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<UsuarioListadoDto> ObtenerPorId(long id)
    {
        var usuario = _usuarioDao.SeleccionarUsuario(id);
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
            var usuarioCreado = _usuarioDao.InsertarUsuario(usuario, solicitud.IdsRol);
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
            var usuario = _usuarioDao.SeleccionarUsuario(id);
            if (usuario is null) return NotFound();

            UsuarioMapeador.AplicarCambios(usuario, solicitud);
            _usuarioDao.ActualizarUsuario(usuario, solicitud.IdsRol, solicitud.Contrasena);

            var usuarioActualizado = _usuarioDao.SeleccionarUsuario(id)!;
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

        if (!_usuarioDao.DarBajaLogicaUsuario(id)) return NotFound();

        var usuario = _usuarioDao.SeleccionarUsuario(id)!;
        return Ok(UsuarioMapeador.AListadoDto(usuario));
    }
}
