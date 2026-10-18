using Api.Infraestructura.Seguridad;
using Api.DTOs;
using Api.Mapeadores;
using Api.DTOs.Usuarios;
using Microsoft.AspNetCore.Mvc;
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
    [Permiso("usuario.consultar")]
    [ProducesResponseType<PaginaDto<UsuarioListadoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<UsuarioListadoDto>>> ObtenerTodos([FromQuery] FiltrosUsuariosDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<UsuarioListadoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<UsuarioListadoDto>.ErrorDePaginacion());
        }

        if (!ConsultasDeTexto.TryTraducirEstado(filtros.Estado, out var estado))
        {
            return Ok(PaginaDto<UsuarioListadoDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var filtro = new FiltroUsuarios(filtros.Buscar, filtros.RolId, estado, filtros.DireccionOrden == "desc");
        var (usuarios, total) = await _usuarioDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var elementos = usuarios.Select(UsuarioMapeador.AListadoDto).ToList();
        return Ok(PaginaDto<UsuarioListadoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("{id:long}")]
    [Permiso("usuario.consultar")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioListadoDto>> ObtenerPorId(long id, CancellationToken cancelacion)
    {
        var usuario = await _usuarioDao.SeleccionarUsuarioAsync(id, cancelacion);
        return usuario is null ? NotFound() : Ok(UsuarioMapeador.AListadoDto(usuario));
    }

    [HttpPost]
    [Permiso("usuario.crear")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioListadoDto>> Crear(UsuarioCrearDto solicitud, CancellationToken cancelacion)
    {
        var usuario = UsuarioMapeador.AEntidad(solicitud);
        var usuarioCreado = await _usuarioDao.InsertarUsuarioAsync(usuario, solicitud.IdsRol, cancelacion);
        var respuesta = UsuarioMapeador.AListadoDto(usuarioCreado);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = usuarioCreado.IdUsuario }, respuesta);
    }

    [HttpPut("{id:long}")]
    [Permiso("usuario.editar")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioListadoDto>> Actualizar(long id, UsuarioActualizarDto solicitud, CancellationToken cancelacion)
    {
        var usuario = await _usuarioDao.SeleccionarUsuarioAsync(id, cancelacion);
        if (usuario is null) return NotFound();

        UsuarioMapeador.AplicarCambios(usuario, solicitud);
        await _usuarioDao.ActualizarUsuarioAsync(usuario, solicitud.IdsRol, solicitud.Contrasena, cancelacion);

        var usuarioActualizado = (await _usuarioDao.SeleccionarUsuarioAsync(id, cancelacion))!;
        return Ok(UsuarioMapeador.AListadoDto(usuarioActualizado));
    }

    [HttpPatch("{id:long}")]
    [Permiso("usuario.editar")]
    [ProducesResponseType<UsuarioListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioListadoDto>> Desactivar(long id, UsuarioEstadoDto solicitud, CancellationToken cancelacion)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { mensaje = "Este endpoint solo acepta el estado 'inactivo'." });

        if (!await _usuarioDao.DarBajaLogicaUsuarioAsync(id, cancelacion)) return NotFound();

        var usuario = (await _usuarioDao.SeleccionarUsuarioAsync(id, cancelacion))!;
        return Ok(UsuarioMapeador.AListadoDto(usuario));
    }
}
