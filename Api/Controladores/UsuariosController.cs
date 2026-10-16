using Api.DTOs;
using Api.Mapeadores;
using Api.DTOs.Usuarios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

        var filtro = TraducirEstado(filtros.Estado, out var estado)
            ? new FiltroUsuarios(filtros.Buscar, filtros.RolId, estado, filtros.DireccionOrden == "desc")
            : null;
        if (filtro is null)
        {
            return Ok(PaginaDto<UsuarioListadoDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var (usuarios, total) = _usuarioDao.ListarPagina(filtro, filtros.Pagina, filtros.TamanoPagina);
        var elementos = usuarios.Select(UsuarioMapeador.AListadoDto).ToList();
        return Ok(PaginaDto<UsuarioListadoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    private static bool TraducirEstado(string? texto, out bool? estado)
    {
        estado = null;
        if (string.IsNullOrEmpty(texto)) return true;
        if (texto == "activo") estado = true;
        else if (texto == "inactivo") estado = false;
        else return false;
        return true;
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
        catch (DbUpdateException excepcion)
        {
            return Conflict(MensajeDeDuplicado(excepcion));
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
        catch (DbUpdateException excepcion)
        {
            return Conflict(MensajeDeDuplicado(excepcion));
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

    private static object MensajeDeDuplicado(DbUpdateException excepcion)
    {
        var restriccion = (excepcion.InnerException as PostgresException)?.ConstraintName;
        return restriccion switch
        {
            "UK_MAIL_USUARIO" => new { codigo = "CORREO_DUPLICADO", mensaje = "El correo electrónico ya está registrado." },
            "UK_TELEFONO_USUARIO" => new { codigo = "TELEFONO_DUPLICADO", mensaje = "El número de teléfono ya está registrado." },
            _ => new { codigo = "USUARIO_DUPLICADO", mensaje = "Ya existe un usuario con el correo o teléfono indicado." },
        };
    }
}
